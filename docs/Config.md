# Configuracion

## Fuentes y precedencia

1. `appsettings.json` (+ `appsettings.{Entorno}.json`): valores **no secretos** y defaults. Nunca un secreto.
2. Variables de entorno (`Seccion__Clave`), que ganan sobre los appsettings.
3. En **Development** (y solo ahi) se carga el `.env` del repo antes de crear el builder
   (`AppConfigurationExtensions.LoadDotEnvInDevelopment`, DotNetEnv con `NoClobber`: una variable real del shell
   siempre gana). En `Testing` no se carga, para que el `.env` de quien corre las pruebas no las contamine.

Arranque en local: `cp .env.example .env` y ajustar. `.env.example` documenta cada variable.

## Variables principales

| Clave | Obligatoria | Notas |
|---|---|---|
| `ConnectionStrings__DefaultConnection` | si | Rol **`backtemplate_app`**. Sin ella la API no arranca. |
| `ConnectionStrings__Migrations` | para `dotnet ef` | Rol dueno; la usa `AppDbContextFactory`. |
| `Jwt__Key` | si | 32 bytes o mas; se rechaza el placeholder de ejemplo. |
| `Jwt__Issuer`, `Jwt__Audience`, `Jwt__AccessTokenMinutes` (15), `Jwt__RefreshTokenDays` (14) | no | |
| `Auth__InvitationHours` (72), `Auth__PasswordResetMinutes` (60) | no | Vida de los enlaces por correo. |
| `Totp__EncryptionKey` | si | 32 bytes o mas y distinta de `Jwt__Key`. |
| `Bootstrap__Secret` | no | Vacio = bootstrap apagado. 32 bytes o mas si esta. |
| `Web__BaseUrl` | prod | Base de los enlaces de los correos (https en produccion). |
| `Cors__AllowedOrigins` | prod | Separados por comas, o arreglo en appsettings. |
| `Smtp__Host`, `__Port`, `__UseSsl`, `__From`, `__User`, `__Password` | prod | Sin `Smtp__Host`: en Development el correo va al log; fuera, falla. |
| `ObjectStorage__Endpoint`, `__PublicEndpoint`, `__AccessKey`, `__SecretKey`, `__Bucket`, `__UseSsl`, `__Region`, `__PresignedExpiryMinutes`, `__CreateBucketIfMissing` | credenciales en prod | `PublicEndpoint` = host con el que se firman las URLs (vacio = `Endpoint`). |
| `BackgroundJobs__DispatcherEnabled` (true), `__PollSeconds` (60), `__OperatorTenantId` | no | Sin `OperatorTenantId` nadie opera tareas desde la API. |
| `BackgroundJobs__SessionPurge__DryRun` (true), `__RetentionDays` (30) | no | La purga de ejemplo solo cuenta hasta apagar `DryRun`. |
| `ForwardedHeaders__Enabled`, `__KnownProxies`, `__KnownNetworks` | no | Encendido sin proxy/red declarados no arranca. |
| `RateLimiting__AuthPerMinute` (10), `__UploadPerMinute` (30), `__GlobalPerMinute` (300), `RateLimiting__Enabled` | no | |
| `Seq__ServerUrl`, `Serilog__MinimumLevel__Default`, `Observability__OtlpEndpoint` | no | Ver [Observability.md](Observability.md). |
| `Swagger__Enabled` | no | Swagger se sirve en Development o si esto es `true`. |

Puerto en desarrollo: `http://localhost:5060` (`Properties/launchSettings.json`). En contenedor la API escucha en
8080.

## Validacion al arrancar (fail-fast)

- `ValidateScopes` y `ValidateOnBuild` en todos los entornos: un registro faltante truena al arrancar.
- `JwtAuthExtensions`: claves JWT/TOTP/bootstrap.
- `HttpEdgeExtensions`: ForwardedHeaders coherente.
- `EnsureProductionSettings` (solo Production): `Web:BaseUrl` https, `Cors:AllowedOrigins`, `Smtp:Host` y
  `ObjectStorage:AccessKey`/`SecretKey`. Faltando cualquiera, la API se niega a arrancar y lista lo que falta.
- `RlsRoleGuard` (servicio de arranque): el rol de la conexion no puede ser superusuario ni tener `BYPASSRLS`.

`scripts/validar-env-produccion.py` revisa un archivo de variables de produccion antes de subirlo.

## Produccion

Nada de `.env` en el servidor: la config no secreta y los secretos llegan por el pipeline. Inventario completo en
[deploy/github-secrets.md](deploy/github-secrets.md).
