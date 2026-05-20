# Security.md — Seguridad, Rate Limiting, CORS y FluentValidation

---

## Rate Limiting — built-in desde .NET 7

Limita la cantidad de requests por cliente en una ventana de tiempo. Esencial para endpoints de autenticación.

### Configuración

```csharp
// Host.Api/Extensions/RateLimitExtensions.cs
public static class RateLimitExtensions
{
    public const string LoginPolicy   = "login";
    public const string DefaultPolicy = "default";

    public static IServiceCollection AddRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            // Política para login — muy restrictiva (por IP)
            options.AddPolicy(LoginPolicy, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        Window      = TimeSpan.FromMinutes(1),
                        PermitLimit = 5,
                        QueueLimit  = 0
                    }));

            // Política por defecto — moderada
            options.AddSlidingWindowLimiter(DefaultPolicy, cfg =>
            {
                cfg.Window            = TimeSpan.FromMinutes(1);
                cfg.PermitLimit       = 100;
                cfg.SegmentsPerWindow = 4;
            });
        });

        return services;
    }
}
```

```csharp
// Host.Api/Program.cs
builder.Services.AddRateLimiting();

// Después de UseRouting, antes de UseAuthentication:
app.UseRateLimiter();
```

### Aplicar en controllers

```csharp
// En el endpoint de login
[HttpPost("login")]
[AllowAnonymous]
[EnableRateLimiting(RateLimitExtensions.LoginPolicy)]
public async Task<IActionResult> Login([FromBody] LoginBody body, CancellationToken ct) { ... }

// En el endpoint de register
[HttpPost("register")]
[AllowAnonymous]
[EnableRateLimiting(RateLimitExtensions.LoginPolicy)]
public async Task<IActionResult> Register([FromBody] RegisterBody body, CancellationToken ct) { ... }

// A nivel de controller — aplica a todos
[EnableRateLimiting(RateLimitExtensions.DefaultPolicy)]
public sealed class UsersController : ControllerBase { ... }
```

---

## Security Headers

```csharp
// Host.Api/Extensions/SecurityHeadersExtensions.cs
public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app)
{
    app.Use(async (context, next) =>
    {
        var headers = context.Response.Headers;

        headers["X-Content-Type-Options"] = "nosniff";
        headers["X-Frame-Options"]        = "DENY";
        headers["X-XSS-Protection"]       = "1; mode=block";
        headers["Referrer-Policy"]        = "strict-origin-when-cross-origin";
        headers["Permissions-Policy"]     = "camera=(), microphone=(), geolocation=()";

        headers["Content-Security-Policy"] =
            "default-src 'self'; " +
            "script-src 'self'; " +
            "style-src 'self' 'unsafe-inline'; " +
            "img-src 'self' data:; " +
            "font-src 'self'; " +
            "connect-src 'self'; " +
            "frame-ancestors 'none';";

        if (context.Request.IsHttps)
            headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";

        headers.Remove("Server");
        headers.Remove("X-Powered-By");

        await next();
    });

    return app;
}

// Host.Api/Program.cs — antes de UseAuthentication
app.UseSecurityHeaders();
```

---

## HTTPS Enforcement

```csharp
// Host.Api/Program.cs
app.UseHttpsRedirection();   // habilitar en producción

builder.Services.AddHsts(options =>
{
    options.MaxAge            = TimeSpan.FromDays(365);
    options.IncludeSubDomains = true;
});

if (!app.Environment.IsDevelopment())
    app.UseHsts();
```

---

## CORS en Producción

Para producción, restringir a los orígenes reales del frontend.

```csharp
// Host.Api/Extensions/CorsExtensions.cs — extender con política de producción
public static IServiceCollection AddProductionCors(
    this IServiceCollection services, IConfiguration configuration)
{
    var allowedOrigins = configuration
        .GetSection("Cors:AllowedOrigins")
        .Get<string[]>()
        ?? throw new InvalidOperationException("Cors:AllowedOrigins no configurado.");

    services.AddCors(options =>
    {
        options.AddPolicy("production", policy =>
        {
            policy
                .WithOrigins(allowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
        });
    });

    return services;
}
```

```json
// appsettings.Production.json:
{
  "Cors": {
    "AllowedOrigins": [
      "https://app.tudominio.com",
      "https://admin.tudominio.com"
    ]
  }
}
```

```csharp
// Host.Api/Program.cs — elegir la policy según el entorno
var corsPolicy = app.Environment.IsProduction()
    ? "production"
    : CorsExtensions.PolicyName;   // localhost:*

app.UseCors(corsPolicy);
```

---

## FluentValidation + Pipeline Behavior

Validación en Application antes de que llegue al Handler.

### Instalar en el proyecto Application del módulo

```xml
<PackageReference Include="FluentValidation" Version="11.*" />
<PackageReference Include="FluentValidation.DependencyInjectionExtensions" Version="11.*" />
```

### Validator del Request

```csharp
// {Modulo}.Application/UseCases/Register/RegisterRequestValidator.cs
public sealed class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("El email es requerido.")
            .EmailAddress().WithMessage("Email inválido.")
            .MaximumLength(256);

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("La contraseña es requerida.")
            .MinimumLength(8).WithMessage("Mínimo 8 caracteres.")
            .Matches("[A-Z]").WithMessage("Debe contener al menos una mayúscula.")
            .Matches("[0-9]").WithMessage("Debe contener al menos un número.");

        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("El nombre completo es requerido.")
            .MaximumLength(200);
    }
}
```

### Pipeline Behavior de validación

```csharp
// {Modulo}.Application/Behaviors/ValidationBehavior.cs
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

        throw new ValidationException(failures);
    }
}
```

### Registro en DI

```csharp
// {Modulo}.Application/ServiceCollectionEx.cs
public static IServiceCollection AddAuthenticationApplicationServices(this IServiceCollection services)
{
    services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());
    services.AddScoped(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
    return services;
}
```

---

## OWASP Top 10 — checklist para esta API

| # | Vulnerabilidad | Mitigación en este proyecto |
|---|---------------|---------------------------|
| A01 | Broken Access Control | `[Authorize]` + `[Authorize(Roles = "Admin")]` + query filter global de EF Core por TenantId |
| A02 | Cryptographic Failures | BCrypt workFactor:12 para passwords, JWT HS256 ≥32 chars, HTTPS en prod |
| A03 | Injection | EF Core con LINQ parametrizado — **nunca** `FromSqlRaw` con interpolación de strings |
| A04 | Insecure Design | Clean Architecture — dominio aislado, validators en Application |
| A05 | Security Misconfiguration | Security headers, CORS restrictivo en prod, Swagger deshabilitado en prod |
| A06 | Vulnerable Components | `dotnet list package --vulnerable` en CI |
| A07 | Auth Failures | Rate limiting en login/register, refresh token rotación, revocación |
| A08 | Software Integrity | Verificar hashes de imágenes Docker |
| A09 | Logging Failures | Serilog + Seq — **no loguear** passwords, tokens completos, PII |
| A10 | SSRF | Validar URLs de entrada, no hacer requests a IPs internas |

### Lo más crítico para esta API

```csharp
// A03 — NUNCA FromSqlRaw con interpolación
// ❌
_db.UserProfiles.FromSqlRaw($"SELECT * FROM dbo.user_profiles WHERE email = '{email}'");

// ✓ LINQ parametrizado (EF Core genera SQL parametrizado automáticamente)
_db.UserProfiles.Where(u => u.Email == email).AsNoTracking().ToListAsync();

// ✓ Si necesitas SQL raw: siempre parámetros con FromSqlInterpolated o {0}
_db.UserProfiles.FromSqlInterpolated($"SELECT * FROM dbo.user_profiles WHERE email = {email}");

// A09 — No loguear datos sensibles
// ❌
_logger.LogInformation("Login con Password={Password}", request.Password);

// ✓
_logger.LogInformation("Login intento para Email={Email}", request.Email);

// A01 — EF Core global query filter garantiza el filtro de TenantId automáticamente.
// Para repos de auth (login/refresh) que usan IgnoreQueryFilters(), verificar manualmente:
var credential = await _db.Credentials
    .IgnoreQueryFilters()
    .FirstOrDefaultAsync(c => c.Email == email, ct);
// Estos repos son de solo autenticación — no exponen datos de otros tenants.
```
