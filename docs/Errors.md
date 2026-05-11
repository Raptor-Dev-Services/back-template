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
Global Exception Handler                      ← captura excepciones, loguea, devuelve 500
    ↓
ProblemDetails JSON                           ← respuesta estándar para ambos casos
```

---

## Problem Details — RFC 7807

Estándar HTTP para respuestas de error. En lugar de un JSON ad-hoc, todos los errores siguen la misma estructura:

```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.4",
  "title": "Not Found",
  "status": 404,
  "detail": "Usuario con ID abc-123 no encontrado.",
  "instance": "/api/example/users/abc-123",
  "traceId": "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01"
}
```

### `UseCoreProblemDetails()` — ya está en Program.cs

El proyecto tiene `app.UseCoreProblemDetails()` de `Common.Web`. Este middleware convierte las excepciones no manejadas en ProblemDetails automáticamente.

Si necesitas configurarlo manualmente:

```csharp
// Host/Program.cs
builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = ctx =>
    {
        ctx.ProblemDetails.Extensions["traceId"] =
            Activity.Current?.Id ?? ctx.HttpContext.TraceIdentifier;
        ctx.ProblemDetails.Extensions["environment"] =
            ctx.HttpContext.RequestServices
                .GetRequiredService<IWebHostEnvironment>().EnvironmentName;
    };
});

// app.UseExceptionHandler() antes de UseRouting y UseAuthentication
app.UseExceptionHandler();
app.UseStatusCodePages();
```

---

## Global Exception Handler — `IExceptionHandler`

ASP.NET Core 8+ tiene `IExceptionHandler` para centralizar el manejo de excepciones, reemplazando el `try/catch` en cada controller.

```csharp
// WebApi/Exceptions/GlobalExceptionHandler.cs
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
        // OperationCanceledException = cliente desconectado — no loguear como error
        if (exception is OperationCanceledException)
        {
            httpContext.Response.StatusCode = 499;  // Client Closed Request (nginx convention)
            return true;
        }

        // Loguear el error real con stack trace
        _logger.LogError(exception,
            "Unhandled exception on {Method} {Path}",
            httpContext.Request.Method,
            httpContext.Request.Path);

        // Determinar status code según el tipo de excepción
        var statusCode = exception switch
        {
            ArgumentException        => StatusCodes.Status400BadRequest,
            UnauthorizedAccessException => StatusCodes.Status401Unauthorized,
            KeyNotFoundException     => StatusCodes.Status404NotFound,
            InvalidOperationException => StatusCodes.Status422UnprocessableEntity,
            _                        => StatusCodes.Status500InternalServerError
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

        return true;  // true = excepción manejada, no propagar
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
// WebApi/ServiceCollectionEx.cs
services.AddExceptionHandler<GlobalExceptionHandler>();
services.AddProblemDetails();

// Host/Program.cs — antes de UseAuthentication
app.UseExceptionHandler();
```

### Consecuencia en los controllers

Con el global handler, el `try/catch` de cada controller desaparece para los errores técnicos:

```csharp
// ❌ Antes — try/catch en cada controller
[HttpGet("{id:guid}")]
public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
{
    try
    {
        _ = await Mediator.Send(new GetExampleUserRequest(id), ct);
        return _viewModel.IsSuccess ? Ok(_viewModel) : StatusCode(500, _viewModel);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error en GetById");
        return StatusCode(500, _viewModel.Fail(ex.Message));
    }
}

// ✓ Después — el global handler captura las excepciones técnicas
[HttpGet("{id:guid}")]
public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
{
    _ = await Mediator.Send(new GetExampleUserRequest(id), ct);
    return _viewModel.IsSuccess ? Ok(_viewModel) : StatusCode(500, _viewModel);
    // si lanza excepción → GlobalExceptionHandler la captura → ProblemDetails 500
}
```

---

## Result Pattern vs Excepciones — regla de decisión

```
¿El llamador puede anticipar este resultado y manejarlo?
    SÍ → Result Pattern (IFailure)
    NO → Excepción (fluye al global handler)

Ejemplos:
    "Usuario no encontrado"     → INotFoundFailure     (el caller anticipa que puede no existir)
    "Email ya registrado"       → IConflictFailure      (el caller anticipa duplicados)
    "Contraseña incorrecta"     → IUnauthorizedFailure  (el caller anticipa credenciales malas)
    "DB desconectada"           → NpgsqlException       (nadie anticipa esto en el flujo normal)
    "NullReferenceException"    → Exception             (bug, no debería ocurrir)
    "Token JWT malformado"      → Exception             (middleware lo captura antes del handler)
```

```csharp
// ✓ Result para flujo de negocio
public async Task<LoginResponse> Handle(LoginRequest req, CancellationToken ct)
{
    var user = await _repo.GetByEmailAsync(req.Email, ct);
    if (user is null)
        return new LoginNotFoundFailure("Credenciales incorrectas.");  // no revelar si existe o no

    if (!BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash))
        return new LoginUnauthorizedFailure("Credenciales incorrectas.");

    if (!user.IsActive)
        return new LoginForbiddenFailure("Cuenta inactiva.");

    var token = _jwt.GenerateToken(user.PublicId, user.Email);
    return new LoginSuccess(token);
}

// ✓ Excepción para infraestructura — dejar que suba, el global handler la captura
var conn = await _factory.OpenConnectionAsync(ct);  // NpgsqlException si DB caída → 500 automático
```

---

## Soft Delete

Borrado lógico: en lugar de eliminar la fila, se marca con `DeletedAt`. Los queries excluyen estas filas automáticamente.

### Migración

```sql
-- ALTER en migración nueva (no editar la existente)
ALTER TABLE dbo.ExampleUsers ADD COLUMN IF NOT EXISTS DeletedAt TIMESTAMP(0) NULL;

-- Índice parcial — solo indexa los activos (no eliminados)
CREATE INDEX IF NOT EXISTS ix_example_users_active
    ON dbo.ExampleUsers (PublicId) WHERE DeletedAt IS NULL;
```

### Interfaz

```csharp
// Domain/Shared/ISoftDeletable.cs
public interface ISoftDeletable
{
    DateTime? DeletedAt { get; }
    bool IsDeleted => DeletedAt.HasValue;
}

// En la entidad
public sealed class ExampleUser : ISoftDeletable
{
    public DateTime? DeletedAt { get; private set; }
    public bool IsDeleted => DeletedAt.HasValue;

    public void Delete()
    {
        if (IsDeleted) throw new InvalidOperationException("Ya está eliminado.");
        DeletedAt = DateTime.UtcNow;
    }
}
```

### Queries con filtro automático

```csharp
// Todos los queries excluyen los eliminados con WHERE DeletedAt IS NULL
public Task<ExampleUser?> GetByPublicIdAsync(Guid publicId, CancellationToken ct) =>
    _db.QuerySingleAsync<ExampleUser>(
        """
        SELECT Id, PublicId, FullName, Email, IsActive, DeletedAt, CreatedAtUtc
        FROM dbo.ExampleUsers
        WHERE PublicId = @publicId
          AND DeletedAt IS NULL;    -- ← filtro de soft delete
        """,
        new { publicId }, cancellationToken: ct);

// Handler de delete — marca como eliminado en lugar de borrar
public async Task<DeleteExampleUserResponse> Handle(
    DeleteExampleUserRequest request, CancellationToken ct)
{
    var user = await _repo.GetByPublicIdAsync(request.PublicId, ct);
    if (user is null)
        return new DeleteExampleUserNotFoundFailure("Usuario no encontrado.");

    user.Delete();
    await _repo.SoftDeleteAsync(user.PublicId, ct);
    return new DeleteExampleUserSuccess();
}

// SQL del soft delete
public Task SoftDeleteAsync(Guid publicId, CancellationToken ct) =>
    _db.ExecuteAsync(
        """
        UPDATE dbo.ExampleUsers
        SET DeletedAt    = timezone('utc', now()),
            UpdatedAtUtc = timezone('utc', now())
        WHERE PublicId   = @publicId
          AND DeletedAt IS NULL;
        """,
        new { publicId }, cancellationToken: ct);
```

### Restaurar un elemento eliminado (admin)

```csharp
public Task RestoreAsync(Guid publicId, CancellationToken ct) =>
    _db.ExecuteAsync(
        """
        UPDATE dbo.ExampleUsers
        SET DeletedAt    = NULL,
            UpdatedAtUtc = timezone('utc', now())
        WHERE PublicId = @publicId;
        """,
        new { publicId }, cancellationToken: ct);
```
