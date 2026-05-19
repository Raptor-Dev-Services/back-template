# Multi-tenancy — cómo fluye el TenantId

El sistema es multi-tenant: cada registro en la base de datos pertenece a un tenant específico y **ningún tenant puede ver ni modificar datos de otro**. Esto se garantiza en cada capa.

---

## El flujo completo

```
Usuario hace login → recibe JWT con claim "tenant_id"
        ↓
Request HTTP con header:  Authorization: Bearer <token>
        ↓
UseAuthentication         → verifica el JWT, llena HttpContext.User con los claims
        ↓
UseAuthorization          → verifica [Authorize], devuelve 401/403 si falla
        ↓
TenantClaimsMiddleware    → lee "tenant_id" de HttpContext.User y lo guarda en ITenantContextAccessor
        ↓
Controller                → lee CurrentTenantId desde User.FindFirstValue("tenant_id")
        ↓
Request                   → TenantId viaja como parámetro del record
        ↓
Handler                   → recibe TenantId del request, lo pasa al repositorio
        ↓
...Sql class              → WHERE TenantId = @tenantId en todos los queries
        ↓
PostgreSQL                → solo devuelve filas de ese tenant
```

---

## Paso 1 — El JWT y sus claims

Al hacer login exitoso, `LoginHandler` genera un JWT con los siguientes claims:

```csharp
// Authentication.Infrastructure/Services/JwtTokenService.cs
_jwt.GenerateAccessToken(
    credential.PublicId,   // "sub" → identificador del usuario
    credential.Email,       // "email"
    credential.Role,        // "role" → "Admin", "Manager", "Operator"
    credential.TenantId,    // "tenant_id"
    credential.BranchId     // "branch_id"
);
```

El JWT decodificado se ve así:
```json
{
  "sub":       "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "email":     "usuario@empresa.com",
  "role":      "Admin",
  "tenant_id": "1",
  "branch_id": "2",
  "exp":       1735689600,
  "iss":       "back-template",
  "aud":       "back-template-clients"
}
```

**Nota:** `tenant_id` y `branch_id` viajan como strings en el JWT (los claims son siempre strings). Se parsean a `long` en el controller.

---

## Paso 2 — TenantClaimsMiddleware

```csharp
// Host.Api/Middleware/TenantClaimsMiddleware.cs
public sealed class TenantClaimsMiddleware
{
    private readonly RequestDelegate _next;
    public TenantClaimsMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, ITenantContextAccessor tenantCtx)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var tenantId = context.User.FindFirst("tenant_id")?.Value;
            if (!string.IsNullOrEmpty(tenantId))
                tenantCtx.Current = new TenantContext(tenantId);
        }

        await _next(context);
    }
}
```

Este middleware está en el pipeline después de `UseAuthentication` y `UseAuthorization`. En ese punto el JWT ya fue verificado y `HttpContext.User` tiene todos los claims.

`ITenantContextAccessor` es un Singleton que enriquece los logs de Serilog y las trazas de OpenTelemetry con el `tenant_id` de cada request. No es la forma en que el handler obtiene el TenantId — eso se hace directamente en el controller.

---

## Paso 3 — Leer TenantId en el Controller

Cada controller que necesite el tenant define esta propiedad:

```csharp
private long CurrentTenantId =>
    long.TryParse(User.FindFirstValue("tenant_id"), out var id) ? id : 0;
```

`User` es la propiedad de `ControllerBase` que apunta a `HttpContext.User` — ya tiene los claims porque `UseAuthentication` los llenó.

Si el JWT no tiene `tenant_id`, `TryParse` devuelve `false` y `CurrentTenantId` es `0`. Esto nunca debería pasar en producción porque el JWT es generado por el propio sistema.

```csharp
// Uso típico en un endpoint
[HttpGet("{id:guid}")]
public async Task<IActionResult> GetById(Guid id, CancellationToken ct = default)
{
    _ = await Mediator.Send(new GetUserProfileRequest(id, CurrentTenantId), ct);
    return _viewModel.IsSuccess ? Ok(_viewModel) : StatusCode(500, _viewModel);
}
```

---

## Paso 4 — El TenantId viaja en el Request

El controller construye el Request incluyendo `CurrentTenantId`. El Request es un record inmutable — el TenantId se captura en el momento de la construcción y no puede cambiar.

```csharp
public sealed record GetUserProfileRequest(Guid PublicId, long TenantId)
    : IRequest<GetUserProfileResponse>;
```

El handler recibe el TenantId como parte del request. Nunca necesita acceder a `HttpContext` — no sabe que existe HTTP.

---

## Paso 5 — El Handler usa TenantId

```csharp
public async Task<GetUserProfileResponse> Handle(
    GetUserProfileRequest request, CancellationToken cancellationToken)
{
    var profile = await _profiles.GetByPublicIdAsync(
        request.PublicId,
        request.TenantId,   // ← viene del request
        cancellationToken);

    if (profile is null)
        return new GetUserProfileNotFoundFailure("Perfil no encontrado.");

    return new GetUserProfileSuccess(new UserProfileDto(profile.PublicId, profile.FullName, profile.IsActive));
}
```

El handler pasa el TenantId al repositorio. El repositorio lo pasa a la clase SQL.

---

## Paso 6 — El SQL filtra por TenantId

**Todos** los queries de lectura y escritura incluyen `TenantId` en el `WHERE`:

```csharp
// En UserProfilesSql
public Task<UserProfile?> GetByPublicIdAsync(Guid publicId, long tenantId, CancellationToken ct = default) =>
    _db.QuerySingleAsync<UserProfile>(
        """
        SELECT Id, PublicId, TenantId, BranchId, FullName, IsActive, CreatedAtUtc, UpdatedAtUtc
        FROM dbo.UserProfiles
        WHERE PublicId = @publicId AND TenantId = @tenantId AND IsActive = TRUE;
        """,
        new { publicId, tenantId },
        cancellationToken: ct);
```

**Regla crítica:** ninguna consulta de lectura retorna datos de múltiples tenants. Si un query no filtra por `TenantId`, es un bug de seguridad.

---

## Endpoints que no necesitan TenantId

Solo `login` y `register` no necesitan TenantId previo — de hecho, son los que lo establecen. Están marcados con `[AllowAnonymous]` en `AuthController`.

```csharp
[AllowAnonymous]
[HttpPost("login")]
public async Task<IActionResult> Login([FromBody] LoginBody body, CancellationToken ct = default)
```

El `CurrentTenantId` en `AuthController` puede existir pero no se usa para estas rutas.

---

## BranchId — funciona igual

`BranchId` sigue el mismo patrón que `TenantId`:

```csharp
private long CurrentBranchId =>
    long.TryParse(User.FindFirstValue("branch_id"), out var id) ? id : 0;
```

Se usa cuando una operación necesita saber en qué sucursal está operando el usuario, además del tenant.

---

## Resumen rápido

| Capa | Cómo obtiene el TenantId |
|------|--------------------------|
| JWT | El login lo pone como claim `"tenant_id"` |
| Controller | `User.FindFirstValue("tenant_id")` → `CurrentTenantId` |
| Request | Parámetro del record: `new GetXRequest(id, CurrentTenantId)` |
| Handler | `request.TenantId` |
| ...Sql | Parámetro en `WHERE TenantId = @tenantId` |
| PostgreSQL | Filtra filas de la tabla |
