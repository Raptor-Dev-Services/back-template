# Multi-tenancy — cómo fluye el TenantId

El sistema es multi-tenant: cada registro en la base de datos pertenece a un tenant específico y **ningún tenant puede ver ni modificar datos de otro**. Esto se garantiza en cada capa, con EF Core como última línea de defensa a nivel de base de datos.

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
Handler                   → recibe TenantId del request, lo pasa al repositorio (si aplica)
        ↓
AppDbContext              → global query filter aplica WHERE TenantId = @currentTenantId automáticamente
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
    credential.Role,        // "role" → "Admin", "Manager", "User"
    credential.TenantId     // "tenant_id"
);
```

El JWT decodificado se ve así:
```json
{
  "sub":       "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "email":     "usuario@empresa.com",
  "role":      "Admin",
  "tenant_id": "1",
  "exp":       1735689600,
  "iss":       "back-template",
  "aud":       "back-template-clients"
}
```

**Nota:** `tenant_id` viaja como string en el JWT (los claims son siempre strings). Se parsea a `long` en el controller y en `AppDbContext`.

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

`ITenantContextAccessor` (Singleton) sirve a dos propósitos:
1. **Enriquecer logs y trazas** con `tenant_id` en Serilog y OpenTelemetry.
2. **Alimentar el global query filter** de `AppDbContext` — el DbContext (Scoped) lee `_tenantAccessor.Current?.TenantId` al construir cada query.

---

## Paso 3 — Leer TenantId en el Controller

Cada controller que necesite el tenant define esta propiedad:

```csharp
private long CurrentTenantId =>
    long.TryParse(User.FindFirstValue("tenant_id"), out var id) ? id : 0;
```

`User` es la propiedad de `ControllerBase` que apunta a `HttpContext.User` — ya tiene los claims porque `UseAuthentication` los llenó.

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

## Paso 5 — El Handler usa TenantId (si aplica)

```csharp
public async Task<GetUserProfileResponse> Handle(
    GetUserProfileRequest request, CancellationToken cancellationToken)
{
    var profile = await _profiles.GetByPublicIdAsync(
        request.PublicId,
        cancellationToken);   // ← TenantId NO se pasa explícitamente al repo

    if (profile is null)
        return new GetUserProfileNotFoundFailure("Perfil no encontrado.");

    return new GetUserProfileSuccess(new UserProfileDto(profile.PublicId, profile.FullName, profile.IsActive));
}
```

> El handler no necesita pasar el TenantId al repositorio porque el **global query filter de EF Core lo aplica automáticamente** a nivel de `AppDbContext`. El repositorio no recibe el tenant como parámetro.

---

## Paso 6 — EF Core filtra automáticamente

`AppDbContext.OnModelCreating` define el filtro global:

```csharp
mb.Entity<UserCredential>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
mb.Entity<UserProfile>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
```

`CurrentTenantId` es una propiedad del DbContext que lee `_tenantAccessor.Current?.TenantId` en cada consulta:

```csharp
private long CurrentTenantId =>
    long.TryParse(_tenantAccessor.Current?.TenantId, out var id) ? id : 0L;
```

Cualquier query sobre `UserProfile` o `UserCredential` recibirá automáticamente un `WHERE TenantId = @currentTenantId` — sin necesidad de agregarlo manualmente.

**Regla crítica:** ninguna consulta retorna datos de múltiples tenants. El filtro global es la garantía de seguridad. Si un repositorio llama `IgnoreQueryFilters()` sin justificación, es un bug de seguridad.

---

## Endpoints que no necesitan TenantId previo

Solo `login` y `register` no necesitan TenantId previo — de hecho, son los que lo establecen. Están marcados con `[AllowAnonymous]` en `AuthController`.

Los repositorios de credenciales usan `IgnoreQueryFilters()` porque al momento del login el `ITenantContextAccessor` no tiene tenant aún:

```csharp
// UserCredentialRepository.cs
public async Task<UserCredential?> GetForLoginAsync(string email, CancellationToken ct = default) =>
    await _db.Credentials
        .IgnoreQueryFilters()   // ← sin tenant en el JWT aún
        .AsNoTracking()
        .FirstOrDefaultAsync(e => e.Email == email && e.IsActive, ct);
```

---

## Resumen rápido

| Capa | Cómo obtiene el TenantId |
|------|--------------------------|
| JWT | El login lo pone como claim `"tenant_id"` |
| `TenantClaimsMiddleware` | Lo extrae del claim y lo guarda en `ITenantContextAccessor` |
| Controller | `User.FindFirstValue("tenant_id")` → `CurrentTenantId` |
| Request | Parámetro del record (para lógica del handler) |
| `AppDbContext` | Lee `_tenantAccessor.Current?.TenantId` en cada query |
| PostgreSQL | Filtrado automático por EF Core global query filter |
