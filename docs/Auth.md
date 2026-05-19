# Auth.md — Autenticación JWT

Guía completa de la autenticación JWT HS256 en este proyecto: configuración, generación de tokens, protección de endpoints y lectura de claims.

---

## Módulo Authentication

El módulo de autenticación vive en `Shared/Authentication/` y tiene 6 proyectos:

```
Shared/Authentication/
├── Authentication.Contracts/      → UserShouldBeCreatedIntegrationEvent
├── Authentication.Domain/         → UserCredential, RefreshToken, interfaces de repositorio
├── Authentication.Application/    → LoginHandler, RegisterHandler, RefreshTokenHandler
├── Authentication.Infrastructure/ → CredentialsSql, RefreshTokensSql, JwtTokenService, PasswordHasher
├── Authentication.Presentation/   → AuthController (Controllers/), Presenters, RequestBodies
└── Authentication.Tests/          → tests de arquitectura (NetArchTest) + tests de handlers (NSubstitute)
```

---

## Endpoints disponibles

| Endpoint | Descripción |
|----------|-------------|
| `POST /api/auth/login` | Obtener access token + refresh token |
| `POST /api/auth/register` | Registrar nuevas credenciales (crear tenant+branch previo) |
| `POST /api/auth/refresh` | Renovar el access token con el refresh token |

---

## Cómo funciona JWT en este proyecto

```
Cliente                          API
  |                               |
  |  POST /api/auth/login         |
  |  { email, password }          |
  |------------------------------>|
  |                               | 1. Busca en dbo.Credentials por email
  |                               | 2. Verifica BCrypt hash de password
  |                               | 3. Genera JWT firmado con Jwt:Key (HS256)
  |                               | 4. Genera refresh token aleatorio, guarda en dbo.RefreshTokens
  |  200 { accessToken, refreshToken, expiresAtUtc }
  |<------------------------------|
  |                               |
  |  GET /api/users               |
  |  Authorization: Bearer eyJ..  |
  |------------------------------>|
  |                               | 5. JwtBearerMiddleware valida el token
  |                               | 6. Puebla HttpContext.User con claims
  |                               | 7. [Authorize] permite o rechaza
  |  200 { data: [...] }          |
  |<------------------------------|
```

---

## Configuración

`appsettings.json`:

```json
"Jwt": {
  "Key":                   "CHANGE_ME_TO_A_SECURE_SECRET_KEY_AT_LEAST_32_CHARS",
  "Issuer":                "back-template",
  "Audience":              "back-template-clients",
  "ExpirationMinutes":     60,
  "RefreshTokenExpiryDays": 30
}
```

| Clave | Descripción |
|-------|-------------|
| `Key` | Clave secreta HS256. Mínimo 32 caracteres. **Nunca en producción en appsettings** — usar variable de entorno `Jwt__Key` |
| `Issuer` | Nombre del emisor del token |
| `Audience` | Audiencia del token |
| `ExpirationMinutes` | Duración del access token |
| `RefreshTokenExpiryDays` | Duración del refresh token |

La validación se registra en `Host.Api/Extensions/JwtAuthExtensions.cs`:
- `ValidateIssuerSigningKey = true`
- `ValidateIssuer = true` / `ValidateAudience = true` / `ValidateLifetime = true`
- `ClockSkew = TimeSpan.Zero` — sin margen extra

---

## Claims del JWT

| Claim | Tipo .NET | Valor |
|-------|-----------|-------|
| `sub` | `Guid` (string) | `credential.PublicId` |
| `email` | `string` | `credential.Email` |
| `role` | `string` | `credential.Role` ("Admin" / "User") |
| `tenant_id` | `long` (string) | `credential.TenantId` |
| `branch_id` | `long` (string) | `credential.BranchId` |

---

## Leer claims en un Controller

```csharp
// PublicId del usuario autenticado (claim "sub")
var userPublicId = Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);

// Email
var email = User.FindFirstValue(JwtRegisteredClaimNames.Email);

// Rol
bool isAdmin = User.IsInRole("Admin");

// TenantId (como long)
private long CurrentTenantId =>
    long.TryParse(User.FindFirstValue("tenant_id"), out var id) ? id : 0;

// BranchId (como long)
private long CurrentBranchId =>
    long.TryParse(User.FindFirstValue("branch_id"), out var id) ? id : 0;
```

---

## IJwtTokenService

Interfaz en `Authentication.Application/Services/IJwtTokenService.cs`:

```csharp
public interface IJwtTokenService
{
    string GenerateAccessToken(Guid publicId, string email, string role, long tenantId, long branchId);
    string GenerateRefreshToken();
    DateTime GetRefreshTokenExpiry();
}
```

Implementación en `Authentication.Infrastructure/Services/JwtTokenService.cs`.

Registrado como `Scoped` en `Authentication.Infrastructure/ServiceCollectionEx.cs`.

---

## IPasswordHasher

Interfaz en `Authentication.Application/Services/IPasswordHasher.cs`:

```csharp
public interface IPasswordHasher
{
    string Hash(string plainText);
    bool Verify(string plainText, string hash);
}
```

Implementación BCrypt con `workFactor: 12` en `Authentication.Infrastructure/Services/PasswordHasher.cs`.

---

## Flujo de Login

`LoginHandler.cs` en `Authentication.Application/UseCases/Login/`:

```csharp
public async Task<LoginResponse> Handle(LoginRequest request, CancellationToken cancellationToken)
{
    var credential = await _credentials.GetForLoginAsync(request.Email, cancellationToken);

    if (credential is null || !_hasher.Verify(request.Password, credential.PasswordHash) || !credential.IsActive)
        return new LoginInvalidCredentialsFailure("Credenciales inválidas.");

    var accessToken  = _jwt.GenerateAccessToken(credential.PublicId, credential.Email, credential.Role, credential.TenantId, credential.BranchId);
    var refreshToken = _jwt.GenerateRefreshToken();
    var expiry       = _jwt.GetRefreshTokenExpiry();

    await _refreshTokens.InsertAsync(credential.Id, refreshToken, expiry, cancellationToken);

    return new LoginSuccess(new TokenDto(accessToken, refreshToken, expiry));
}
```

---

## Flujo de Register

`RegisterHandler.cs` en `Authentication.Application/UseCases/Register/`:

1. Verifica que el tenant existe (`ITenantApi.ExistsAsync(tenantId)`)
2. Verifica que el email no está duplicado en `dbo.Credentials`
3. Crea la credencial con password hasheado
4. **Publica `UserShouldBeCreatedIntegrationEvent`** → lo recibe `Users.Application` → crea el `UserProfile` en `dbo.UserProfiles`

```csharp
// Después de crear la credencial:
await _mediator.Publish(new UserShouldBeCreatedIntegrationEvent(
    credential.PublicId, credential.TenantId, credential.BranchId, request.FullName));
```

---

## Flujo de Refresh Token

`RefreshTokenHandler.cs` en `Authentication.Application/UseCases/RefreshToken/`:

1. Busca el token en `dbo.RefreshTokens` por valor exacto
2. Verifica que no está revocado ni expirado
3. Carga las credenciales asociadas por `CredentialId`
4. Revoca el token viejo (`IsRevoked = true`)
5. Genera nuevo access token + refresh token
6. Guarda el nuevo refresh token en BD

---

## Proteger endpoints

### Requerir autenticación

```csharp
[ApiController]
[Route("api/products")]
[Authorize]   // todo el controller requiere JWT válido
public sealed class ProductsController : ControllerBase { }
```

### Endpoint público dentro de un controller protegido

```csharp
[HttpGet("catalog")]
[AllowAnonymous]   // este endpoint no requiere token
public async Task<IActionResult> GetCatalog(CancellationToken ct = default) { ... }
```

### Requerir un rol específico

```csharp
[HttpDelete("{id:guid}")]
[Authorize(Roles = "Admin")]   // solo usuarios con Role = "Admin"
public async Task<IActionResult> Disable(Guid id, CancellationToken ct = default) { ... }
```

---

## Estructura del token JWT

**Payload (claims):**
```json
{
  "sub":       "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "email":     "usuario@empresa.com",
  "role":      "Admin",
  "tenant_id": "1",
  "branch_id": "2",
  "jti":       "d4f7a2b1-...",
  "exp":       1746000000,
  "iss":       "back-template",
  "aud":       "back-template-clients"
}
```

Para inspeccionar tokens: [jwt.io](https://jwt.io)

---

## Probar en Swagger UI

1. Abrir `http://localhost:5080/swagger`
2. `POST /api/auth/login` → obtener `accessToken`
3. Clic en **Authorize** (candado) — pegar solo `eyJ...` (sin "Bearer ")
4. Todos los endpoints protegidos usarán ese token

---

## Variables de entorno en producción

```bash
# Docker Compose
Jwt__Key=tu-clave-jwt-secreta-minimo-32-caracteres

# Variables del sistema
export Jwt__Key="tu-clave-jwt-secreta-minimo-32-caracteres"
```

Generar una clave segura:

```powershell
# PowerShell
[Convert]::ToBase64String([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(48))
```

```bash
# Linux/Mac
openssl rand -base64 48
```
