# Auth.md — Autenticación JWT

Guía completa de la autenticación JWT HS256 en este proyecto: configuración, generación de tokens, protección de endpoints y lectura de claims.

---

## Cómo funciona JWT en este proyecto

```
Cliente                          API
  |                               |
  |  POST /api/auth/login         |
  |  { email, password }          |
  |------------------------------>|
  |                               | 1. Verifica credenciales (BCrypt)
  |                               | 2. Genera JWT firmado con Jwt:Key (HS256)
  |  200 { token: "eyJ..." }      |
  |<------------------------------|
  |                               |
  |  GET /api/products            |
  |  Authorization: Bearer eyJ..  |
  |------------------------------>|
  |                               | 3. JwtBearerMiddleware valida el token
  |                               | 4. Puebla HttpContext.User con claims
  |                               | 5. [Authorize] permite o rechaza
  |  200 { data: [...] }          |
  |<------------------------------|
```

---

## Configuración

`appsettings.json`:
```json
"Jwt": {
  "Key":               "CHANGE_ME_TO_A_SECURE_SECRET_KEY_AT_LEAST_32_CHARS",
  "Issuer":            "back-template",
  "Audience":          "back-template-clients",
  "ExpirationMinutes": 60
}
```

| Clave | Descripción |
|-------|-------------|
| `Key` | Clave secreta HS256. Mínimo 32 caracteres. **Nunca en producción en appsettings** — usar variable de entorno `Jwt__Key` |
| `Issuer` | Nombre del emisor del token. Debe coincidir al validar |
| `Audience` | Audiencia del token. Debe coincidir al validar |
| `ExpirationMinutes` | Tiempo de vida del token en minutos |

La validación se registra en `Host/Extensions/JwtAuthExtensions.cs`:
- `ValidateIssuerSigningKey = true`
- `ValidateIssuer = true`
- `ValidateAudience = true`
- `ValidateLifetime = true`
- `ClockSkew = TimeSpan.Zero` — sin margen de tiempo extra

---

## Generar un token JWT

Esto ocurre en un handler de Application o en un servicio de Infrastructure que implementa `IJwtTokenService`.

### Servicio de generación de tokens

`Infrastructure/Services/JwtTokenService.cs`:

```csharp
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Infrastructure.Services;

public interface IJwtTokenService
{
    string Generate(Guid userId, string email, string role = "User");
}

public sealed class JwtTokenService : IJwtTokenService
{
    private readonly string _key;
    private readonly string _issuer;
    private readonly string _audience;
    private readonly int    _expirationMinutes;

    public JwtTokenService(IConfiguration configuration)
    {
        _key               = configuration["Jwt:Key"]       ?? throw new InvalidOperationException("Jwt:Key no configurado.");
        _issuer            = configuration["Jwt:Issuer"]    ?? throw new InvalidOperationException("Jwt:Issuer no configurado.");
        _audience          = configuration["Jwt:Audience"]  ?? throw new InvalidOperationException("Jwt:Audience no configurado.");
        _expirationMinutes = configuration.GetValue<int>("Jwt:ExpirationMinutes", 60);
    }

    public string Generate(Guid userId, string email, string role = "User")
    {
        var key         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_key));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub,   userId.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, email),
            new Claim(ClaimTypes.Role,               role),
            new Claim(JwtRegisteredClaimNames.Jti,   Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer:             _issuer,
            audience:           _audience,
            claims:             claims,
            expires:            DateTime.UtcNow.AddMinutes(_expirationMinutes),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
```

Registrar en `Infrastructure/ServiceCollectionEx.cs`:
```csharp
services.AddScoped<IJwtTokenService, JwtTokenService>();
```

### Usarlo en un handler de login

```csharp
public sealed class LoginHandler : IRequestHandler<LoginRequest, LoginResponse>
{
    private readonly IUserRepository    _users;
    private readonly IJwtTokenService   _jwt;

    public LoginHandler(IUserRepository users, IJwtTokenService jwt)
    {
        _users = users;
        _jwt   = jwt;
    }

    public async Task<LoginResponse> Handle(LoginRequest request, CancellationToken cancellationToken)
    {
        var user = await _users.GetByEmailAsync(request.Email, cancellationToken);
        if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            return new LoginUnauthorizedFailure("Credenciales inválidas.");

        var token = _jwt.Generate(user.PublicId, user.Email);
        return new LoginSuccess(new LoginDto(token, user.FullName));
    }
}
```

---

## Proteger endpoints

### Requerir autenticación

```csharp
[Route("api/products")]
[Authorize]   // todo el controller requiere JWT válido
public sealed class ProductsController : BaseApiController
{
    // Todos los endpoints de este controller requieren token
}
```

### Endpoint público dentro de un controller protegido

```csharp
[Route("api/products")]
[Authorize]
public sealed class ProductsController : BaseApiController
{
    [HttpGet("catalog")]
    [AllowAnonymous]   // este endpoint no requiere token
    public async Task<IActionResult> GetCatalog(CancellationToken ct = default)
    {
        _ = await Mediator.Send(new GetProductCatalogRequest(), ct);
        return _viewModel.IsSuccess ? Ok(_viewModel) : StatusCode(500, _viewModel);
    }
}
```

### Requerir un rol específico

```csharp
[HttpDelete("{id:guid}")]
[Authorize(Roles = "Admin")]
public async Task<IActionResult> Disable(Guid id, CancellationToken ct = default)
{
    // Solo usuarios con claim Role = "Admin"
}
```

---

## Leer claims del usuario autenticado

### En un controller

```csharp
// Leer el PublicId del usuario (claim "sub")
var userIdStr = User.FindFirstValue(JwtRegisteredClaimNames.Sub);
var userId    = Guid.Parse(userIdStr!);

// Leer el email
var email = User.FindFirstValue(JwtRegisteredClaimNames.Email);

// Verificar si tiene un rol
bool isAdmin = User.IsInRole("Admin");

// Leer cualquier claim por nombre
var role = User.FindFirstValue(ClaimTypes.Role);
```

### En un handler (vía el request)

Los handlers no tienen acceso a `HttpContext` directamente. La forma correcta es pasar el dato relevante como parte del request:

```csharp
// En el controller
var userId = Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
_ = await Mediator.Send(new GetMyProfileRequest(userId), ct);
```

Si necesitas el userId en muchos handlers, considera un servicio `ICurrentUserService`:

```csharp
// Application/Services/ICurrentUserService.cs
public interface ICurrentUserService
{
    Guid UserId { get; }
    string Email { get; }
    bool IsAuthenticated { get; }
}

// WebApi/Services/CurrentUserService.cs
public sealed class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
        => _httpContextAccessor = httpContextAccessor;

    public Guid UserId =>
        Guid.Parse(_httpContextAccessor.HttpContext!.User
            .FindFirstValue(JwtRegisteredClaimNames.Sub)!);

    public string Email =>
        _httpContextAccessor.HttpContext!.User
            .FindFirstValue(JwtRegisteredClaimNames.Email)!;

    public bool IsAuthenticated =>
        _httpContextAccessor.HttpContext?.User.Identity?.IsAuthenticated == true;
}
```

Registrar:
```csharp
// WebApi/ServiceCollectionEx.cs
services.AddHttpContextAccessor();
services.AddScoped<ICurrentUserService, CurrentUserService>();
```

---

## Estructura del token JWT

Un token JWT tiene 3 partes separadas por `.`: `header.payload.signature`

**Header:**
```json
{ "alg": "HS256", "typ": "JWT" }
```

**Payload (claims):**
```json
{
  "sub":   "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "email": "usuario@ejemplo.com",
  "role":  "User",
  "jti":   "d4f7a2b1-...",
  "exp":   1746000000,
  "iss":   "back-template",
  "aud":   "back-template-clients"
}
```

**Signature:** HMAC-SHA256 de `base64(header) + "." + base64(payload)` firmado con `Jwt:Key`.

Para inspeccionar tokens manualmente: [jwt.io](https://jwt.io)

---

## Probar autenticación en Swagger UI

1. Abrir `http://localhost:5080/swagger`
2. Llamar al endpoint de login para obtener el token
3. Hacer clic en el botón **Authorize** (candado) en la esquina superior derecha
4. En el campo `Bearer`, pegar el token **sin el prefijo "Bearer "** — solo `eyJ...`
5. Hacer clic en **Authorize**
6. Ahora todos los endpoints marcados con el candado usarán ese token

---

## Renovar tokens (refresh token)

El template no incluye refresh tokens por defecto. Para agregar este flujo:

1. Al generar el token JWT, también generar un `RefreshToken` (GUID seguro) y guardarlo en la base de datos junto con su expiración
2. Crear endpoint `POST /api/auth/refresh` que reciba el refresh token, lo valide contra la BD y emita un nuevo JWT
3. El refresh token tiene mayor duración (ej. 7 días) mientras el JWT tiene corta duración (1 hora)

---

## Variables de entorno en producción

**Nunca** poner `Jwt:Key` en `appsettings.json` ni en el repositorio. En producción:

```bash
# Docker Compose
Jwt__Key=tu-clave-jwt-secreta-minimo-32-caracteres

# Variables del sistema
export Jwt__Key="tu-clave-jwt-secreta-minimo-32-caracteres"
```

Generar una clave segura:
```bash
# PowerShell
[Convert]::ToBase64String([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(48))

# Linux/Mac
openssl rand -base64 48
```
