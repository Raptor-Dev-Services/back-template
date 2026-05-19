# Errors.md — Manejo de Errores, Problem Details y Soft Delete

---

## Estrategia de errores

Dos mecanismos distintos para dos tipos de error distintos:

| Tipo | Mecanismo | Cuándo |
|------|-----------|--------|
| Fallo de negocio esperado | Result Pattern (`IFailure`) | Usuario no encontrado, email duplicado, validación |
| Error técnico inesperado | Excepción → Global Handler | NullRef, DB caída, bug, timeout |

```
Request
    ↓
Handler → return new XxxNotFoundFailure()     ← fallo de negocio: valor de retorno
Handler → throws NpgsqlException              ← error técnico: burbujea
    ↓
UseCoreProblemDetails() / GlobalExceptionHandler  ← captura, loguea, devuelve 500
    ↓
ProblemDetails JSON                           ← respuesta estándar
```

---

## Problem Details — RFC 7807

```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.4",
  "title": "Not Found",
  "status": 404,
  "detail": "Perfil de usuario no encontrado.",
  "instance": "/api/users/abc-123",
  "traceId": "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01"
}
```

### `UseCoreProblemDetails()` — ya está en Program.cs

`Host.Api/Program.cs` tiene `app.UseCoreProblemDetails()` de `Common.Web`. Este middleware convierte las excepciones no manejadas en ProblemDetails automáticamente:

| Excepción | Código HTTP |
|-----------|-------------|
| `BusinessRuleException` | 400 |
| Cualquier otra excepción | 500 |

---

## Global Exception Handler — `IExceptionHandler`

Para centralizar el manejo de excepciones reemplazando el `try/catch` en cada controller.

Ubicación: `{Modulo}.Presentation/` o `Host.Api/Middleware/GlobalExceptionHandler.cs`

```csharp
public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
        => _logger = logger;

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception   exception,
        CancellationToken ct)
    {
        if (exception is OperationCanceledException)
        {
            httpContext.Response.StatusCode = 499;
            return true;
        }

        _logger.LogError(exception,
            "Unhandled exception on {Method} {Path}",
            httpContext.Request.Method,
            httpContext.Request.Path);

        var statusCode = exception switch
        {
            ArgumentException           => StatusCodes.Status400BadRequest,
            UnauthorizedAccessException => StatusCodes.Status401Unauthorized,
            KeyNotFoundException        => StatusCodes.Status404NotFound,
            InvalidOperationException   => StatusCodes.Status422UnprocessableEntity,
            _                           => StatusCodes.Status500InternalServerError
        };

        var problemDetails = new ProblemDetails
        {
            Status   = statusCode,
            Title    = GetTitle(statusCode),
            Detail   = exception.Message,
            Instance = httpContext.Request.Path,
            Type     = $"https://httpstatuses.io/{statusCode}"
        };

        problemDetails.Extensions["traceId"] =
            Activity.Current?.Id ?? httpContext.TraceIdentifier;

        httpContext.Response.StatusCode  = statusCode;
        httpContext.Response.ContentType = "application/problem+json";
        await httpContext.Response.WriteAsJsonAsync(problemDetails, ct);
        return true;
    }

    private static string GetTitle(int statusCode) => statusCode switch
    {
        400 => "Bad Request",
        401 => "Unauthorized",
        403 => "Forbidden",
        404 => "Not Found",
        422 => "Unprocessable Entity",
        _   => "Internal Server Error"
    };
}
```

### Registro

```csharp
// Presentation/ServiceCollectionEx.cs (o Host.Api/Program.cs)
services.AddExceptionHandler<GlobalExceptionHandler>();
services.AddProblemDetails();

// Host.Api/Program.cs — antes de UseAuthentication
app.UseExceptionHandler();
```

---

## Result Pattern vs Excepciones — regla de decisión

```
¿El llamador puede anticipar este resultado y manejarlo?
    SÍ → Result Pattern (IFailure)
    NO → Excepción (fluye al global handler)

Ejemplos:
    "Perfil no encontrado"          → INotFoundFailure    (esperado)
    "Email ya registrado"           → IConflictFailure    (esperado)
    "Credenciales inválidas"        → IFailure            (esperado)
    "DB desconectada"               → NpgsqlException     (inesperado)
    "NullReferenceException"        → Exception           (bug)
```

```csharp
// ✓ Result para flujo de negocio
public async Task<LoginResponse> Handle(LoginRequest req, CancellationToken ct)
{
    var credential = await _credentials.GetForLoginAsync(req.Email, ct);
    if (credential is null || !_hasher.Verify(req.Password, credential.PasswordHash) || !credential.IsActive)
        return new LoginInvalidCredentialsFailure("Credenciales inválidas.");

    var token = _jwt.GenerateAccessToken(credential.PublicId, credential.Email, credential.Role, credential.TenantId, credential.BranchId);
    return new LoginSuccess(new TokenDto(token, /* ... */));
}

// ✓ Excepción para infraestructura — dejar que suba
var conn = await _factory.GetOpenConnectionAsync(ct);  // NpgsqlException si DB caída → 500 automático
```

---

## Soft Delete

Borrado lógico: en lugar de eliminar la fila, se marca con `DeletedAt`.

### Migración

```sql
ALTER TABLE dbo.UserProfiles ADD COLUMN IF NOT EXISTS DeletedAt TIMESTAMP(0) NULL;

CREATE INDEX IF NOT EXISTS IX_UserProfiles_Active
    ON dbo.UserProfiles (PublicId) WHERE DeletedAt IS NULL;
```

### SQL con filtro de soft delete

```csharp
public Task<UserProfile?> GetByPublicIdAsync(Guid publicId, long tenantId, CancellationToken ct = default) =>
    _db.QuerySingleAsync<UserProfile>(
        """
        SELECT Id, PublicId, TenantId, BranchId, FullName, IsActive, CreatedAtUtc, UpdatedAtUtc
        FROM dbo.UserProfiles
        WHERE PublicId = @publicId
          AND TenantId = @tenantId
          AND DeletedAt IS NULL;    -- filtro de soft delete
        """,
        new { publicId, tenantId }, cancellationToken: ct);

public Task SoftDeleteAsync(Guid publicId, long tenantId, CancellationToken ct = default) =>
    _db.ExecuteAsync(
        """
        UPDATE dbo.UserProfiles
        SET DeletedAt    = timezone('utc', now()),
            UpdatedAtUtc = timezone('utc', now())
        WHERE PublicId   = @publicId
          AND TenantId   = @tenantId
          AND DeletedAt IS NULL;
        """,
        new { publicId, tenantId }, cancellationToken: ct);
```

### Handler de disable/soft-delete

```csharp
public async Task<DisableUserProfileResponse> Handle(DisableUserProfileRequest request, CancellationToken ct)
{
    var profile = await _profiles.GetByPublicIdAsync(request.PublicId, request.TenantId, ct);
    if (profile is null)
        return new DisableUserProfileNotFoundFailure("Perfil no encontrado.");

    await _profiles.SoftDeleteAsync(request.PublicId, request.TenantId, ct);
    return new DisableUserProfileSuccess();
}
```

---

## BusinessRuleException

Para violaciones de reglas de negocio que deben retornar HTTP 400:

```csharp
// En un handler o en la entidad de dominio
throw new BusinessRuleException("El stock no puede ser negativo.");
```

El middleware `UseCoreProblemDetails()` lo captura automáticamente y retorna ProblemDetails 400.
