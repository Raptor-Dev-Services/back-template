# Packages.md — Guía de Paquetes NuGet

Explicación explícita de cada paquete instalado en el proyecto, qué problema resuelve, y cómo se usa.

---

## Distribución por proyecto

```
Common/Common/               → Serilog.*, OpenTelemetry.*, AspNetCore.HealthChecks.*
Shared/Database/             → Npgsql.EntityFrameworkCore.PostgreSQL, Microsoft.EntityFrameworkCore.Design
{Modulo}.Domain/             → sin paquetes externos
{Modulo}.Contracts/          → sin paquetes externos
{Modulo}.Application/        → sin paquetes externos (usa Common vía ProjectReference)
{Modulo}.Infrastructure/     → BCrypt.Net-Next, System.IdentityModel.Tokens.Jwt (solo Authentication)
{Modulo}.Presentation/       → FrameworkReference Microsoft.AspNetCore.App
Host.Api/                    → Microsoft.AspNetCore.Authentication.JwtBearer, Swashbuckle.AspNetCore, Microsoft.OpenApi, Microsoft.EntityFrameworkCore.Design
Tests/                       → xunit, xunit.runner.visualstudio, Microsoft.NET.Test.Sdk, coverlet.collector, NSubstitute, NetArchTest.Rules
```

---

## Shared.Database

### Npgsql.EntityFrameworkCore.PostgreSQL `10.0.1`

**Qué es:** Provider de EF Core para PostgreSQL. Traduce las queries LINQ de EF Core a SQL de PostgreSQL via Npgsql.

**Por qué está aquí:** `AppDbContext` usa EF Core para gestionar el esquema y todas las operaciones de datos.

**Cómo se configura** (`Shared/Database/ServiceCollectionEx.cs`):

```csharp
services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(configuration.GetConnectionString("MainDbConnection")));
```

**Características clave que se usan:**
- `HasDefaultValueSql("gen_random_uuid()")` — UUID generado por PostgreSQL
- `HasDefaultValueSql("timezone('utc', now())")` — timestamp UTC por defecto
- `HasColumnType("timestamp(0)")` — sin fracciones de segundo
- `UseIdentityByDefaultColumn()` — BIGINT auto-incremento
- `ExecuteUpdateAsync()` / `ExecuteDeleteAsync()` — bulk operations sin cargar entidades
- `IgnoreQueryFilters()` — bypass del global query filter de tenant

### Microsoft.EntityFrameworkCore.Design `10.0.8` (PrivateAssets=all)

**Qué es:** Herramientas de EF Core en tiempo de diseño — necesario para `dotnet ef migrations` y scaffolding.

**Por qué está aquí:** aunque no usamos migraciones de EF Core (usamos `EnsureCreated`), este paquete es requerido por `dotnet ef` y por la compilación de templates en `Host.Api`.

**`PrivateAssets="all"`** — solo se usa en desarrollo, no se incluye en la imagen de producción.

---

## Authentication.Infrastructure

### BCrypt.Net-Next `4.1.0`

**Qué es:** Implementación de BCrypt para .NET. BCrypt es un algoritmo de hash de contraseñas diseñado para ser lento (protege contra fuerza bruta).

**Por qué está aquí:** Hashear contraseñas de usuario antes de guardarlas en `dbo.credentials`.

**Cómo se usa:**

```csharp
// Authentication.Infrastructure/Services/PasswordHasher.cs
public string Hash(string plainText) =>
    BCrypt.Net.BCrypt.HashPassword(plainText, workFactor: 12);

public bool Verify(string plainText, string hash) =>
    BCrypt.Net.BCrypt.Verify(plainText, hash);
```

Work factor 12 significa ~300ms por hash — suficientemente lento para proteger contra ataques de diccionario.

---

### System.IdentityModel.Tokens.Jwt `8.x`

**Qué es:** Librería de Microsoft para crear y validar JWT (JSON Web Tokens).

**Por qué está aquí:** Para generar access tokens JWT al hacer login.

**Cómo se usa** (en `Authentication.Infrastructure/Services/JwtTokenService.cs`):

```csharp
var key         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:Key"]!));
var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

var claims = new[]
{
    new Claim(JwtRegisteredClaimNames.Sub,   publicId.ToString()),
    new Claim(JwtRegisteredClaimNames.Email, email),
    new Claim(ClaimTypes.Role,               role),
    new Claim("tenant_id",                   tenantId.ToString()),
    new Claim(JwtRegisteredClaimNames.Jti,   Guid.NewGuid().ToString()),
};

var token = new JwtSecurityToken(
    issuer:             config["Jwt:Issuer"],
    audience:           config["Jwt:Audience"],
    claims:             claims,
    expires:            DateTime.UtcNow.AddMinutes(expirationMinutes),
    signingCredentials: credentials
);

return new JwtSecurityTokenHandler().WriteToken(token);
```

> La **validación** del JWT en requests entrantes la hace `Microsoft.AspNetCore.Authentication.JwtBearer` (en Host.Api), no este paquete directamente.

---

## Host.Api

### Microsoft.AspNetCore.Authentication.JwtBearer `10.x`

**Qué es:** Middleware de ASP.NET Core para autenticación JWT Bearer.

**Por qué está aquí:** Intercepta cada request HTTP, lee el header `Authorization: Bearer <token>`, lo valida y puebla `HttpContext.User` con los claims.

**Cómo se configura** (`Host.Api/Extensions/JwtAuthExtensions.cs`):

```csharp
services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
            ValidateIssuer   = true,  ValidIssuer   = issuer,
            ValidateAudience = true,  ValidAudience = audience,
            ValidateLifetime = true,  ClockSkew     = TimeSpan.Zero
        };
    });
```

---

### Swashbuckle.AspNetCore `10.x`

**Qué es:** Generador de documentación OpenAPI (Swagger) para ASP.NET Core.

**Por qué está aquí:** Genera automáticamente la especificación OpenAPI 3.x a partir de los controllers y modelos del proyecto. Expone Swagger UI para probar endpoints desde el navegador.

**Configurado en** `Host.Api/Extensions/SwaggerExtensions.cs`.

Solo activo en `Local`, `Development` y `Staging`. En `Production` está deshabilitado.

---

### Microsoft.OpenApi `2.x`

**Qué es:** Modelos de objetos del estándar OpenAPI para .NET. Dependencia de Swashbuckle.

**Cómo se usa:**

```csharp
options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
{
    Name         = "Authorization",
    Type         = SecuritySchemeType.Http,
    Scheme       = "bearer",
    BearerFormat = "JWT",
    In           = ParameterLocation.Header,
});
```

---

## Tests

### xunit `2.x`

Framework de testing unitario para .NET:

```csharp
[Fact]
public async Task Handle_existing_profile_returns_success() { ... }

[Theory]
[InlineData(0)]
[InlineData(-1)]
public void Page_should_clamp_to_minimum_1(int page) { ... }
```

### xunit.runner.visualstudio `3.x`

Adaptador que permite correr tests de xUnit desde Visual Studio Test Explorer y `dotnet test`.

### Microsoft.NET.Test.Sdk `18.x`

SDK base de testing de .NET. Requerido para que `dotnet test` funcione.

### coverlet.collector `10.x`

Recolector de cobertura de código:

```bash
dotnet test --collect:"XPlat Code Coverage" --results-directory ./coverage
reportgenerator -reports:./coverage/**/*.xml -targetdir:./coverage/report -reporttypes:Html
```

### NSubstitute `5.x`

Framework de mocking para tests unitarios:

```csharp
var repo = Substitute.For<IUserProfileRepository>();
repo.GetByPublicIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((UserProfile?)null);
```

### NetArchTest.Rules `1.3.x`

Librería para tests de arquitectura — verifica que las dependencias entre capas sigan las reglas:

```csharp
var result = Types.InAssembly(ApplicationAssembly)
    .ShouldNot().HaveDependencyOn("Users.Infrastructure")
    .GetResult();
Assert.True(result.IsSuccessful);
```

---

## Common (submódulo — referencia)

Paquetes en `Common/Common/Common.csproj`:

| Paquete | Versión | Propósito |
|---------|---------|-----------|
| `AspNetCore.HealthChecks.NpgSql` | 9.x | Check de conectividad a PostgreSQL en `/api/health` |
| `Serilog` | 4.x | Core del sistema de logging estructurado |
| `Serilog.Extensions.Logging` | 10.x | Puente entre `ILogger<T>` de .NET y Serilog |
| `Serilog.Sinks.Console` | 6.x | Escribe logs en la consola |
| `Serilog.Sinks.Debug` | 3.x | Escribe logs en el debug output (Visual Studio) |
| `Serilog.Sinks.Seq` | 9.x | Envía logs a Seq |
| `OpenTelemetry.Extensions.Hosting` | 1.x | Integración de OpenTelemetry con el host de .NET |
| `OpenTelemetry.Instrumentation.AspNetCore` | 1.x | Trazas automáticas de requests HTTP entrantes |
| `OpenTelemetry.Instrumentation.Http` | 1.x | Trazas automáticas de llamadas HTTP salientes |
| `OpenTelemetry.Instrumentation.Runtime` | 1.x | Métricas del runtime: GC, threads, CPU |
| `OpenTelemetry.Exporter.OpenTelemetryProtocol` | 1.x | Exporta a Jaeger/Grafana vía OTLP gRPC |
| `OpenTelemetry.Exporter.Prometheus.AspNetCore` | 1.x | Expone métricas en `/metrics` |
| `Microsoft.Extensions.Http.Resilience` | 10.x | Políticas de resiliencia para HttpClient |
