# Seguridad

Resumen de las defensas y donde viven. Autenticacion y RBAC: [Auth.md](Auth.md). Aislamiento: [MultiTenancy.md](MultiTenancy.md).

## Borde HTTP (`Host.Api/Extensions/HttpEdgeExtensions.cs`)

- **ForwardedHeaders** apagado por omision. Encenderlo exige declarar `KnownProxies` o `KnownNetworks`: sin eso la
  API no arranca (seria confiar en un `X-Forwarded-For` inventado).
- **CORS** por lista de origenes (`Cors:AllowedOrigins`, texto separado por comas o arreglo). Sin comodines.
- **Limite de tasa** nativo: `auth` por IP (10/min: login, refresh, restablecer, bootstrap), `upload` por usuario
  (30/min), global (300/min). El 429 sale con el envelope. Va despues de `UseAuthentication` para particionar por
  `sub`. Los nombres de politica viven en `Shared.Web/RateLimitPolicies.cs`.
- **Cabeceras** (`SecurityHeadersMiddleware`): `nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy: no-referrer`,
  `Content-Security-Policy: default-src 'none'`; HSTS (`UseHsts`) fuera de Development. En produccion Caddy repite las cabeceras para sus propias respuestas.
- **Correlacion**: `X-Correlation-Id` saneado (128 caracteres max), o el TraceId si no llega.

## Autorizacion

- Cada accion declara su permiso. `AuthorizationSurfaceTests` falla si una accion queda solo con `[Authorize]` sin
  justificar, o si cita un permiso fuera del catalogo.
- Recursos de otro tenant responden **404**, no 403: no se confirma que existan.
- Archivos: firmar una URL exige que la clave sea del tenant (`IStoredFileRegistry` + RLS). La subida valida tamano
  (5 MB), tipo admitido y la **firma de bytes** frente al `Content-Type` declarado; la clave es opaca
  (`{tenant}/{yyyy}/{MM}/{guid}{ext}`) y el nombre original solo se guarda saneado para mostrarlo.

## Datos

- SQL siempre parametrizado (EF; el unico SQL crudo usa `FromSqlInterpolated`). El GUC de RLS se fija con
  `set_config` parametrizado.
- El rol de la aplicacion no puede `DELETE` ni saltarse RLS.
- Secretos en la base solo hasheados (refresh, tokens de invitacion/restablecimiento, codigos de recuperacion) o
  cifrados (secreto TOTP).

## Errores y logs

- Una excepcion inesperada responde 500 generico; el detalle solo al log ([Errors.md](Errors.md)).
- Los logs de peticion no incluyen el query string; el pipeline del mediador de Common tapa los campos sensibles de
  request/response. `RequestLoggingSecretsTests` impide que un appsettings versionado suba el log de hosting de
  ASP.NET Core (que escribe la URL completa, query incluido) por encima de Warning.

## Secretos y configuracion

- Ningun secreto en `appsettings*.json`: `.env` en desarrollo (ignorado por git), variables de entorno en
  produccion inyectadas por el pipeline ([deploy/github-secrets.md](deploy/github-secrets.md)).
- Fail-fast al arrancar: `Jwt:Key` y `Totp:EncryptionKey` de 32 bytes o mas y distintas; `Bootstrap:Secret` de 32
  bytes si esta; en Production, `EnsureProductionSettings` exige `Web:BaseUrl` https, CORS, SMTP y credenciales del
  almacenamiento ([Config.md](Config.md)).
- CI: gitleaks bloqueante sobre todo el historial (`.gitleaks.toml`, cada exencion justificada), `NuGetAudit` con
  warnings como error, y la puerta de dependencias estables.

## Contenedor

Imagen `aspnet:10.0-noble-chiseled`: sin shell ni gestor de paquetes, usuario no-root. En produccion solo Caddy
publica puertos.
