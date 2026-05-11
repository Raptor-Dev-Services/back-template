# Packages.md — Guía de Paquetes NuGet

Explicación explícita de cada paquete instalado en el proyecto, qué problema resuelve, y cómo se usa.

---

## Infrastructure

### Dapper `2.1.72`

**Qué es:** Micro-ORM que extiende `IDbConnection` con métodos de mapeo automático de filas SQL a objetos .NET.

**Por qué está aquí:** Permite escribir SQL parametrizado puro y mapear los resultados directamente a entidades de dominio, sin el overhead de un ORM completo como EF Core.

**Cómo se usa en este proyecto:**
- Nunca se usa directamente desde repositorios o handlers
- Solo se usa desde `DapperSqlDbConnectionBase` (en Common), que envuelve todos los métodos de Dapper con logging de performance
- Las clases `...Sql` llaman a `MainDapperDbConnection` (que extiende `DapperSqlDbConnectionBase`), que a su vez llama a Dapper internamente

```csharp
// Lo que ves en ...Sql.cs
_db.QuerySingleAsync<ExampleUser>(sql, new { publicId }, cancellationToken: ct)

// Lo que hace MainDapperDbConnection internamente (en Common)
using var con = await _connections.GetOpenConnectionAsync(ct);
await con.QuerySingleOrDefaultAsync<T>(new CommandDefinition(sql, param, cancellationToken: ct));
```

**Dónde está:** `Infrastructure/Infrastructure.csproj`

---

### Npgsql `10.0.2`

**Qué es:** Driver oficial de .NET para PostgreSQL. Implementa `IDbConnection` sobre el protocolo de PostgreSQL.

**Por qué está aquí:** Es el driver que crea las conexiones físicas a la base de datos. `ConfigurationNpgsqlConnectionFactory<T>` (de Common) lo usa para crear `NpgsqlConnection` cuando se llama `GetOpenConnectionAsync()`.

**Cómo se usa en este proyecto:**
```csharp
// Common.PostgreSql internamente hace:
protected override IDbConnection CreateConnection(string connectionString)
    => new NpgsqlConnection(connectionString);
```

Tipos específicos de PostgreSQL que se benefician de Npgsql:
- `UUID` → `Guid`
- `BOOLEAN` → `bool`
- `TIMESTAMP(0)` → `DateTime`
- `BIGINT` → `long`

**Dónde está:** `Infrastructure/Infrastructure.csproj`

---

### BCrypt.Net-Next `4.1.0`

**Qué es:** Implementación de BCrypt para .NET. BCrypt es un algoritmo de hash de contraseñas diseñado para ser lento (protege contra fuerza bruta).

**Por qué está aquí:** Hashear contraseñas de usuario antes de guardarlas en la base de datos.

**Cómo se usa:**
```csharp
// Crear hash al registrar usuario
string hashedPassword = BCrypt.Net.BCrypt.HashPassword(plainTextPassword);

// Verificar al login
bool isValid = BCrypt.Net.BCrypt.Verify(plainTextPassword, hashedPassword);
```

El work factor por defecto (11) significa ~100ms por hash — suficientemente lento para proteger contra ataques de diccionario.

**Dónde está:** `Infrastructure/Infrastructure.csproj`

---

### System.IdentityModel.Tokens.Jwt `8.18.0`

**Qué es:** Librería de Microsoft para crear y validar JWT (JSON Web Tokens).

**Por qué está aquí:** Para generar tokens JWT al hacer login (en los servicios de autenticación de Infrastructure).

**Cómo se usa** (en un servicio de generación de tokens):
```csharp
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;

var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:Key"]!));
var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

var token = new JwtSecurityToken(
    issuer:   config["Jwt:Issuer"],
    audience: config["Jwt:Audience"],
    claims:   new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) },
    expires:  DateTime.UtcNow.AddMinutes(int.Parse(config["Jwt:ExpirationMinutes"]!)),
    signingCredentials: credentials
);

return new JwtSecurityTokenHandler().WriteToken(token);
```

> La **validación** del JWT en requests entrantes la hace `Microsoft.AspNetCore.Authentication.JwtBearer` (en Host), no este paquete directamente.

**Dónde está:** `Infrastructure/Infrastructure.csproj`

---

## Host

### Microsoft.AspNetCore.Authentication.JwtBearer `10.0.7`

**Qué es:** Middleware de ASP.NET Core para autenticación JWT Bearer.

**Por qué está aquí:** Intercepta cada request HTTP, lee el header `Authorization: Bearer <token>`, lo valida contra las claves configuradas y puebla `HttpContext.User` con los claims del token.

**Cómo se configura** (en `Host/Extensions/JwtAuthExtensions.cs`):
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

**Cómo se activa la protección de un endpoint:**
```csharp
[Authorize]           // requiere JWT válido
[Authorize(Roles = "Admin")]  // requiere JWT + rol específico
[AllowAnonymous]      // endpoint público
```

**Dónde está:** `Host/Host.csproj`

---

### Swashbuckle.AspNetCore `10.1.7`

**Qué es:** Generador de documentación OpenAPI (Swagger) para ASP.NET Core.

**Por qué está aquí:** Genera automáticamente la especificación OpenAPI 3.x a partir de los controllers y modelos del proyecto. Expone Swagger UI para probar endpoints desde el navegador.

**Cómo se configura** (en `Host/Extensions/SwaggerExtensions.cs`):
```csharp
services.AddSwaggerGen(options =>
{
    // Agrega esquema de seguridad Bearer para poder usar JWT desde Swagger UI
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme { ... });
    options.AddSecurityRequirement(...);
});
```

**En Program.cs:**
```csharp
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerWithJwt();
// ...
app.UseSwagger();      // expone /swagger/v1/swagger.json
app.UseSwaggerUI();    // expone /swagger
```

Solo activo en `Local`, `Development` y `Staging`. En `Production` está deshabilitado.

**Dónde está:** `Host/Host.csproj`

---

### Microsoft.OpenApi `2.4.1`

**Qué es:** Modelos de objetos del estándar OpenAPI para .NET. Es una dependencia de Swashbuckle.

**Por qué está aquí:** Requerido por Swashbuckle para construir el objeto `OpenApiSecurityScheme` en `SwaggerExtensions.cs`.

**Cómo se usa:**
```csharp
using Microsoft.OpenApi;

options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
{
    Name         = "Authorization",
    Type         = SecuritySchemeType.Http,
    Scheme       = "bearer",
    BearerFormat = "JWT",
    ...
});
```

**Dónde está:** `Host/Host.csproj`

---

## Tests

### xunit `2.9.3`

**Qué es:** Framework de testing unitario para .NET. Alternativa moderna a NUnit/MSTest.

**Por qué está aquí:** Base de todos los tests del proyecto.

**Cómo se usa:**
```csharp
using Xunit;

public class MiEntityTests
{
    [Fact]
    public void Create_should_initialize_with_valid_state()
    {
        var entity = new MiEntity("nombre");
        Assert.Equal("nombre", entity.Nombre);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Create_should_reject_empty_nombre(string? nombre)
    {
        Assert.Throws<ArgumentException>(() => new MiEntity(nombre!));
    }
}
```

**Dónde está:** `Tests/Tests.csproj`

---

### xunit.runner.visualstudio `3.1.5`

**Qué es:** Adaptador que permite correr tests de xUnit desde Visual Studio Test Explorer y `dotnet test`.

**Por qué está aquí:** Sin este paquete, `dotnet test` no puede descubrir ni ejecutar los tests de xUnit.

**Dónde está:** `Tests/Tests.csproj`

---

### Microsoft.NET.Test.Sdk `18.5.1`

**Qué es:** SDK de testing de .NET. Proporciona la infraestructura base para correr cualquier framework de testing con `dotnet test`.

**Por qué está aquí:** Requerido para que `dotnet test` funcione correctamente.

**Dónde está:** `Tests/Tests.csproj`

---

### coverlet.collector `10.0.0`

**Qué es:** Recolector de cobertura de código para .NET que funciona con `dotnet test`.

**Por qué está aquí:** Permite medir qué porcentaje del código está cubierto por tests.

**Cómo se usa:**
```bash
# Cobertura básica
dotnet test --collect:"XPlat Code Coverage"

# Con reporte en HTML (requiere reportgenerator instalado globalmente)
dotnet test --collect:"XPlat Code Coverage" --results-directory ./coverage
reportgenerator -reports:./coverage/**/*.xml -targetdir:./coverage/report -reporttypes:Html
```

**Dónde está:** `Tests/Tests.csproj`

---

## Common (submódulo — referencia)

Los siguientes paquetes están en `Common/Common.csproj`. No se declaran en los proyectos del template, pero están disponibles a través del submódulo.

| Paquete | Versión | Propósito |
|---------|---------|-----------|
| `Dapper` | 2.1.72 | SQL mapping en DapperSqlDbConnectionBase |
| `Npgsql` | 10.0.2 | Driver PostgreSQL en ConfigurationNpgsqlConnectionFactory |
| `AspNetCore.HealthChecks.NpgSql` | 9.0.0 | Check de conectividad a PostgreSQL en `/api/health` |
| `AspNetCore.HealthChecks.Redis` | 9.0.0 | Check de Redis (disponible pero no configurado en este template) |
| `Serilog` | 4.3.1 | Core del sistema de logging estructurado |
| `Serilog.Extensions.Logging` | 10.0.0 | Puente entre `ILogger<T>` de .NET y Serilog |
| `Serilog.Sinks.Console` | 6.1.1 | Escribe logs en la consola |
| `Serilog.Sinks.Debug` | 3.0.0 | Escribe logs en el debug output (Visual Studio) |
| `Serilog.Sinks.Seq` | 9.0.0 | Envía logs a Seq (dashboard de logs estructurados) |
| `OpenTelemetry.Extensions.Hosting` | 1.15.3 | Integración de OpenTelemetry con el host de .NET |
| `OpenTelemetry.Instrumentation.AspNetCore` | 1.15.2 | Trazas automáticas de requests HTTP entrantes |
| `OpenTelemetry.Instrumentation.Http` | 1.15.1 | Trazas automáticas de llamadas HTTP salientes (HttpClient) |
| `OpenTelemetry.Instrumentation.Runtime` | 1.15.1 | Métricas del runtime: GC, threads, CPU |
| `OpenTelemetry.Exporter.OpenTelemetryProtocol` | 1.15.3 | Exporta trazas y métricas a Jaeger/Grafana vía OTLP gRPC |
| `OpenTelemetry.Exporter.Prometheus.AspNetCore` | 1.15.0-beta.1 | Expone métricas en `/metrics` formato Prometheus |
| `Microsoft.Extensions.Http.Resilience` | 10.5.0 | Políticas de resiliencia para HttpClient (retry, circuit breaker) |

---

## Resumen por proyecto

```
Domain/          — sin paquetes externos
Application/     — sin paquetes externos (usa Common vía ProjectReference)
Infrastructure/  — Dapper, Npgsql, BCrypt.Net-Next, System.IdentityModel.Tokens.Jwt
WebApi/          — sin paquetes externos (FrameworkReference Microsoft.AspNetCore.App)
Host/            — Microsoft.AspNetCore.Authentication.JwtBearer, Swashbuckle.AspNetCore, Microsoft.OpenApi
Tests/           — xunit, xunit.runner.visualstudio, Microsoft.NET.Test.Sdk, coverlet.collector
Common/          — Dapper, Npgsql, Serilog.*, OpenTelemetry.*, AspNetCore.HealthChecks.*
```
