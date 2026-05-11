# Security.md — Seguridad, Rate Limiting, CORS y FluentValidation

---

## Rate Limiting — built-in desde .NET 7

Limita la cantidad de requests por cliente en una ventana de tiempo. Esencial para endpoints de autenticación, registro y cualquier operación costosa.

### Configuración

```csharp
// Host/Extensions/RateLimitExtensions.cs
public static class RateLimitExtensions
{
    public const string LoginPolicy    = "login";
    public const string DefaultPolicy  = "default";
    public const string StrictPolicy   = "strict";

    public static IServiceCollection AddRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            // Política para login — muy restrictiva
            options.AddFixedWindowLimiter(LoginPolicy, cfg =>
            {
                cfg.Window            = TimeSpan.FromMinutes(1);
                cfg.PermitLimit       = 5;     // 5 intentos por minuto por IP
                cfg.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                cfg.QueueLimit        = 0;     // sin cola — rechazar inmediatamente
            });

            // Política por defecto — moderada
            options.AddSlidingWindowLimiter(DefaultPolicy, cfg =>
            {
                cfg.Window            = TimeSpan.FromMinutes(1);
                cfg.PermitLimit       = 100;   // 100 requests por minuto
                cfg.SegmentsPerWindow = 4;     // ventana dividida en segmentos de 15s
            });

            // Política estricta para operaciones costosas
            options.AddTokenBucketLimiter(StrictPolicy, cfg =>
            {
                cfg.TokenLimit          = 10;
                cfg.ReplenishmentPeriod = TimeSpan.FromSeconds(30);
                cfg.TokensPerPeriod     = 5;
                cfg.AutoReplenishment   = true;
            });

            // Key personalizada — por IP (por defecto es por IP+User)
            options.AddPolicy(LoginPolicy, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        Window      = TimeSpan.FromMinutes(1),
                        PermitLimit = 5
                    }));
        });

        return services;
    }
}
```

```csharp
// Host/Program.cs
builder.Services.AddRateLimiting();

// Después de UseRouting, antes de UseAuthentication:
app.UseRateLimiter();
```

### Aplicar en controllers

```csharp
// A nivel de endpoint
[HttpPost("login")]
[AllowAnonymous]
[EnableRateLimiting(RateLimitExtensions.LoginPolicy)]
public async Task<IActionResult> Login([FromBody] LoginBody body, CancellationToken ct)
{ ... }

// A nivel de controller — aplica a todos los endpoints
[EnableRateLimiting(RateLimitExtensions.DefaultPolicy)]
public sealed class ExampleUsersController : BaseApiController { ... }

// Deshabilitar para un endpoint específico dentro de un controller con policy
[DisableRateLimiting]
[HttpGet("health-internal")]
public IActionResult HealthInternal() => Ok();
```

### Tipos de limitadores

| Tipo | Comportamiento | Caso de uso |
|------|---------------|-------------|
| `FixedWindow` | Contador resetea cada N segundos | Login, registro |
| `SlidingWindow` | Ventana deslizante — más suave | API general |
| `TokenBucket` | Tokens que se recargan gradualmente | Operaciones costosas |
| `Concurrency` | Máximo N requests simultáneas | Endpoints pesados |

---

## Security Headers

Headers que protegen contra XSS, clickjacking, MIME sniffing y otras vulnerabilidades.

```csharp
// Host/Extensions/SecurityHeadersExtensions.cs
public static class SecurityHeadersExtensions
{
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app)
    {
        app.Use(async (context, next) =>
        {
            var headers = context.Response.Headers;

            // Evita que el browser interprete el content-type incorrecto (MIME sniffing)
            headers["X-Content-Type-Options"] = "nosniff";

            // Evita que la página se cargue en un iframe (clickjacking)
            headers["X-Frame-Options"] = "DENY";

            // Habilita protección XSS del browser (legacy, pero útil)
            headers["X-XSS-Protection"] = "1; mode=block";

            // Solo enviar el referrer al mismo origen
            headers["Referrer-Policy"] = "strict-origin-when-cross-origin";

            // Deshabilitar características del browser no usadas
            headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";

            // Content Security Policy — ajustar según el frontend
            headers["Content-Security-Policy"] =
                "default-src 'self'; " +
                "script-src 'self'; " +
                "style-src 'self' 'unsafe-inline'; " +
                "img-src 'self' data:; " +
                "font-src 'self'; " +
                "connect-src 'self'; " +
                "frame-ancestors 'none';";

            // HSTS — solo en producción con HTTPS real
            if (!context.Request.IsHttps) { }
            else headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";

            // Ocultar que es ASP.NET Core
            headers.Remove("Server");
            headers.Remove("X-Powered-By");

            await next();
        });

        return app;
    }
}

// Host/Program.cs — antes de UseAuthentication
app.UseSecurityHeaders();
```

---

## HTTPS Enforcement

```csharp
// Host/Program.cs

// 1. Redirigir HTTP → HTTPS (habilitar en producción)
app.UseHttpsRedirection();   // estaba comentado — descomentar en prod

// 2. HSTS — decirle al browser que SIEMPRE use HTTPS para este dominio
builder.Services.AddHsts(options =>
{
    options.MaxAge            = TimeSpan.FromDays(365);
    options.IncludeSubDomains = true;
    options.Preload           = false;  // true solo si se registra en la preload list
});

// Solo activar HSTS en producción
if (!app.Environment.IsDevelopment())
    app.UseHsts();
```

```csharp
// Forzar HTTPS en el modelo de hosting (Kestrel)
// appsettings.Production.json:
{
  "Kestrel": {
    "Endpoints": {
      "Http": {
        "Url": "http://*:8080"
      },
      "Https": {
        "Url": "https://*:8443",
        "Certificate": {
          "Path": "/certs/api.pfx",
          "Password": "${CERT_PASSWORD}"
        }
      }
    }
  }
}
```

---

## CORS en Producción

Para producción se restringe a los orígenes reales del frontend.

```csharp
// Host/Extensions/CorsExtensions.cs — extender con política de producción
public static IServiceCollection AddProductionCors(
    this IServiceCollection services, IConfiguration configuration)
{
    var allowedOrigins = configuration
        .GetSection("Cors:AllowedOrigins")
        .Get<string[]>()
        ?? throw new InvalidOperationException("Cors:AllowedOrigins no configurado.");

    services.AddCors(options =>
    {
        options.AddPolicy(ProductionPolicyName, policy =>
        {
            policy
                .WithOrigins(allowedOrigins)   // solo los orígenes listados
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
        });
    });

    return services;
}

// appsettings.Production.json:
{
  "Cors": {
    "AllowedOrigins": [
      "https://app.tudominio.com",
      "https://admin.tudominio.com"
    ]
  }
}

// Host/Program.cs — elegir la policy según el entorno
var corsPolicy = app.Environment.IsProduction()
    ? CorsExtensions.ProductionPolicyName
    : CorsExtensions.PolicyName;   // localhost

app.UseCors(corsPolicy);
```

---

## FluentValidation + Pipeline Behavior

Validación en Application antes de que llegue al Handler. Centralizada, componible, sin `ModelState`.

### Instalar

```xml
<PackageReference Include="FluentValidation"                   Version="11.*" />
<PackageReference Include="FluentValidation.DependencyInjectionExtensions" Version="11.*" />
```

### Validator del Request

```csharp
// Application/UseCases/ExampleUsers/Insert/InsertExampleUserValidator.cs
public sealed class InsertExampleUserRequestValidator
    : AbstractValidator<InsertExampleUserRequest>
{
    public InsertExampleUserRequestValidator()
    {
        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("El nombre es requerido.")
            .MaximumLength(100).WithMessage("Máximo 100 caracteres.");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("El email es requerido.")
            .EmailAddress().WithMessage("Email inválido.")
            .MaximumLength(200);

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("La contraseña es requerida.")
            .MinimumLength(8).WithMessage("Mínimo 8 caracteres.")
            .Matches("[A-Z]").WithMessage("Debe contener al menos una mayúscula.")
            .Matches("[0-9]").WithMessage("Debe contener al menos un número.");
    }
}
```

### Pipeline Behavior de validación

```csharp
// Application/Behaviors/ValidationBehavior.cs
public sealed class ValidationBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest  : IRequest<TResponse>
    where TResponse : IResponse
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
        => _validators = validators;

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken ct)
    {
        if (!_validators.Any())
            return await next();

        var context  = new ValidationContext<TRequest>(request);
        var failures = _validators
            .Select(v => v.Validate(context))
            .SelectMany(r => r.Errors)
            .Where(e => e is not null)
            .ToList();

        if (failures.Count == 0)
            return await next();

        // Construir mensaje de error con todos los campos inválidos
        var errors  = failures
            .GroupBy(f => f.PropertyName)
            .ToDictionary(
                g => g.Key,
                g => g.Select(e => e.ErrorMessage).ToArray());

        var message = string.Join(" | ", failures.Select(f => f.ErrorMessage));

        // Lanzar excepción → GlobalExceptionHandler devuelve 400 ProblemDetails
        throw new ValidationException(failures);

        // Alternativa si quieres evitar excepciones:
        // Retornar un IValidationFailure si TResponse lo soporta
    }
}
```

### Registro en DI

```csharp
// Application/ServiceCollectionEx.cs
public static IServiceCollection AddApplicationServices(this IServiceCollection services)
{
    // Registrar todos los validators del assembly
    services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());

    // Pipeline behavior — antes del handler
    services.AddScoped(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

    // ... AddMediator, handlers, etc.
    return services;
}
```

### Manejar ValidationException en el Global Handler

```csharp
// WebApi/Exceptions/GlobalExceptionHandler.cs
public async ValueTask<bool> TryHandleAsync(
    HttpContext httpContext, Exception exception, CancellationToken ct)
{
    if (exception is ValidationException validationEx)
    {
        var errors = validationEx.Errors
            .GroupBy(e => e.PropertyName)
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());

        var problemDetails = new ValidationProblemDetails(errors)
        {
            Status   = StatusCodes.Status400BadRequest,
            Title    = "Validation Error",
            Detail   = "Uno o más campos no son válidos.",
            Instance = httpContext.Request.Path
        };
        problemDetails.Extensions["traceId"] =
            Activity.Current?.Id ?? httpContext.TraceIdentifier;

        httpContext.Response.StatusCode  = 400;
        httpContext.Response.ContentType = "application/problem+json";
        await httpContext.Response.WriteAsJsonAsync(problemDetails, ct);
        return true;
    }

    // ... resto de excepciones
}
```

**Respuesta JSON de validación:**
```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.1",
  "title": "Validation Error",
  "status": 400,
  "detail": "Uno o más campos no son válidos.",
  "errors": {
    "Email":    ["Email inválido."],
    "Password": ["Mínimo 8 caracteres.", "Debe contener al menos una mayúscula."]
  },
  "traceId": "00-abc123..."
}
```

---

## OWASP Top 10 — checklist para esta API

| # | Vulnerabilidad | Mitigación en este proyecto |
|---|---------------|---------------------------|
| A01 | Broken Access Control | `[Authorize]` + `ICurrentUserService` para verificar ownership |
| A02 | Cryptographic Failures | BCrypt para passwords, JWT HS256 ≥32 chars, HTTPS en prod |
| A03 | Injection | Dapper con parámetros — **nunca** interpolación de strings en SQL |
| A04 | Insecure Design | Clean Architecture — dominio aislado, validators en Application |
| A05 | Security Misconfiguration | Security headers, CORS restrictivo en prod, no exponer stack traces |
| A06 | Vulnerable Components | `dotnet list package --vulnerable` en CI |
| A07 | Auth Failures | Rate limiting en login, refresh token rotación, revocación |
| A08 | Software Integrity | Verificar hashes de imágenes Docker, firmar commits |
| A09 | Logging Failures | Serilog + Seq — **no loguear** passwords, tokens, PII |
| A10 | SSRF | Validar URLs de entrada, no hacer requests a IPs internas |

### Lo más crítico para esta API

```csharp
// A03 — NUNCA interpolación en SQL
// ❌
_db.QueryAsync<User>($"SELECT * FROM Users WHERE Email = '{email}'");  // SQL injection

// ✓ Siempre parámetros
_db.QueryAsync<User>("SELECT * FROM Users WHERE Email = @email", new { email });

// A09 — No loguear datos sensibles
// ❌
_logger.LogInformation("Login attempt for {Password}", request.Password);

// ✓
_logger.LogInformation("Login attempt for user {Email}", request.Email);

// A01 — Verificar ownership en el handler
if (_currentUser.UserId != resource.OwnerPublicId && !_currentUser.IsInRole("admin"))
    return new ForbiddenFailure("No tienes permiso para acceder a este recurso.");
```
