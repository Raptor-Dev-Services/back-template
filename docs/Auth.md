# Autenticacion y autorizacion

Modulo `src/Shared/Authentication/`. JWT de acceso corto + refresh token opaco con rotacion, permisos RBAC como
claims, 2FA TOTP opcional por usuario. No hay registro anonimo: el primer tenant nace por bootstrap y el resto de
las cuentas por invitacion.

## Endpoints

| Ruta | Cuerpo | Notas |
|---|---|---|
| `POST /api/v1/auth/login` | `{ email, password }` | Sin 2FA: tokens. Con 2FA: `twoFactorRequired: true` + `challengeToken` (sin tokens). |
| `POST /api/v1/auth/login/2fa` | `{ challengeToken, code }` | `code` = TOTP de 6 digitos o un codigo de recuperacion. |
| `POST /api/v1/auth/refresh` | `{ refreshToken }` | Rota: revoca el presentado y emite uno nuevo. |
| `POST /api/v1/auth/logout` | `{ refreshToken }` | Revoca; idempotente. |
| `POST /api/v1/auth/password/forgot` | `{ email }` | Respuesta identica exista o no la cuenta. |
| `POST /api/v1/auth/password/reset` | `{ token, newPassword }` | Token de un solo uso; cierra todas las sesiones. |
| `GET /api/v1/account/me` | - | Cuenta, roles, permisos, ultimo login. |
| `POST /api/v1/account/password` | `{ currentPassword, newPassword }` | Exige la actual; revoca las sesiones. |
| `POST /api/v1/account/2fa/setup` / `enable` / `disable` | `{ code }` en enable/disable | Ver 2FA. |
| `POST /api/v1/bootstrap/tenant` | `{ tenantName, tenantSlug, adminEmail, adminPassword, adminFullName }` + header `X-Bootstrap-Secret` | Apagado si `Bootstrap:Secret` esta vacio; no agrega un segundo admin a un tenant con usuarios. |
| `POST /api/v1/accounts` | `{ email, fullName, roleCodes? }` | Invitacion por correo con enlace `/set-password?token=`. `users.manage`. |
| `POST /api/v1/accounts/{id}/lock` / `unlock` | - | Bloquear corta sesiones y login. `users.manage`. |
| `GET /api/v1/roles` | - | Roles del tenant con sus permisos. `users.read`. |

Los anonimos llevan el limite de tasa `auth` (por IP). La respuesta de sesion (`AuthTokensDto` / `LoginResultDto`)
trae `accessToken`, `accessTokenExpiresAtUtc`, `refreshToken`, `refreshTokenExpiresAtUtc`.

## Tokens

- **Access**: JWT HS256 firmado con `Jwt:Key` (32 bytes o mas; la API no arranca con una clave corta o de ejemplo),
  vida `Jwt:AccessTokenMinutes` (15). Validacion con `MapInboundClaims = false` y `ClockSkew` de 30 s.
- **Claims** (`Shared.Kernel/Security/AppClaimTypes.cs`): `sub` (PublicId del usuario), `tenant_id`, `email`,
  `roles` (uno por rol) y `permission` (**uno por codigo de permiso**).
- **Refresh**: opaco, aleatorio; en la base solo su hash SHA-256 (`SecureTokens`). Vida `Jwt:RefreshTokenDays` (14).
  Rotacion en cada uso; presentar uno ya rotado (**reuso**) revoca todas las sesiones de ese usuario.
- **Contrasenas**: BCrypt con work factor 12; minimo 12 caracteres, maximo 72 bytes. El login con un correo
  inexistente tambien calcula un hash (dummy) para no delatar por tiempo.

## RBAC

- Catalogo unico en codigo: `Shared.Kernel/Security/KnownPermissions.cs` (codigos) y
  `Authentication.Domain/Rbac/RbacCatalog.cs` (nombres, roles y concesiones). `RbacCatalogSyncService` lo siembra
  en cada arranque.

| Permiso | Admin | Member |
|---|---|---|
| `users.read` | si | si |
| `users.manage` | si | - |
| `audit.read` | si | - |
| `tasks.manage` | si | - |
| `files.read` | si | si |
| `files.write` | si | si |

- Un endpoint exige su permiso con `[Authorize(Policy = PermissionPolicy.Prefix + KnownPermissions.X)]`
  (`perm:<codigo>`). `PermissionAuthorization.cs` resuelve cualquier policy `perm:*` contra los claims `permission`.
- Nada de `[Authorize(Roles = ...)]`: los roles son agrupaciones de permisos, no la unidad de autorizacion.
- Invitar respeta la **anti-escalada**: solo quien es Admin concede el rol Admin.
- `tasks.manage` lo tiene todo Admin, pero operar tareas exige ademas el tenant operador ([MultiTenancy.md](MultiTenancy.md)).

## 2FA (TOTP)

1. `account/2fa/setup` devuelve el secreto y la URI `otpauth://` (para el QR). El secreto se guarda **cifrado** con
   AES-GCM usando `Totp:EncryptionKey` (obligatoria, 32 bytes o mas, distinta de `Jwt:Key`).
2. `account/2fa/enable` con un codigo valido lo activa y devuelve los codigos de recuperacion (10, guardados
   hasheados, un solo uso).
3. Desde entonces el login devuelve un reto efimero (audiencia `{aud}-2fa`, 5 min) que se canjea en `login/2fa`.
4. Un mismo paso TOTP no se acepta dos veces (anti-replay). `account/2fa/disable` exige un segundo factor, no solo
   la sesion.

## Bitacora

Bootstrap, invitar, bloquear/desbloquear, dar de baja y quitar el 2FA dejan una linea en la bitacora del tenant
(`GET /api/v1/audit-log`, `audit.read`) con el actor.
