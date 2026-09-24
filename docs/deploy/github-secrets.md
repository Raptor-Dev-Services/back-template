# Secretos y variables del pipeline

Inventario de todo lo que el pipeline (`deploy.yml`, `rollback.yml`, `_deploy-to-vps.yml`) lee de GitHub
(**Settings -> Secrets and variables -> Actions**). En el servidor no vive ningun `.env`: el compose recibe
estos valores por SSH en cada despliegue y los interpola. Un secreto faltante hace fallar el `up` con su
nombre (`${VAR:?}`), nunca arranca a medias.

## Interruptor

| Variable | Valor | Efecto |
|---|---|---|
| `AUTO_DEPLOY` | `true` | Enciende `migrate` y `deploy` al empujar un tag. Sin ella, el tag solo construye y publica la imagen. |

## Acceso al servidor (secrets)

| Secret | Que es |
|---|---|
| `VPS_HOST` | IP o nombre del servidor. |
| `VPS_USER` | Usuario de despliegue (en el grupo `docker`, sin sudo). |
| `VPS_SSH_KEY` | Clave privada de despliegue; su `.pub` va en `authorized_keys` del servidor. |

## Migraciones (secrets)

| Secret | Que es |
|---|---|
| `PG_OWNER_URI` | `postgresql://backtemplate_owner@host:5432/backtemplate?sslmode=require`, **sin** contrasena. Rol dueno del esquema (DDL). |
| `PG_OWNER_PASSWORD` | La contrasena de ese rol. Va aparte para no aparecer en ninguna linea de comandos del servidor. |

## Aplicacion (secrets)

| Secret | Obligatorio | Que es |
|---|---|---|
| `ConnectionStrings__DefaultConnection` | si | Cadena con el rol **`backtemplate_app`** (NOBYPASSRLS, sin DELETE). Nunca la del dueno: la API se niega a arrancar si el rol puede saltarse RLS. |
| `Jwt__Key` | si | 32 bytes o mas, aleatoria (`openssl rand -base64 48`). |
| `Totp__EncryptionKey` | si | 32 bytes o mas, **distinta** de `Jwt__Key`. Cambiarla deja ilegibles los segundos factores ya enrolados. |
| `ObjectStorage__AccessKey` / `ObjectStorage__SecretKey` | si | Usuario del almacenamiento acotado a SU bucket, no el de administracion. |
| `Bootstrap__Secret` | no | Solo mientras se crea el primer tenant. Vacio = `POST /api/v1/bootstrap/tenant` apagado. Se borra despues. |
| `Smtp__Password` | segun proveedor | Credencial SMTP. |

## Configuracion no secreta (variables)

Si una variable no existe llega vacia y el compose aplica su default. Las marcadas como obligatorias no tienen
default: sin ellas el `up` falla.

| Variable | Obligatoria | Default |
|---|---|---|
| `API_DOMAIN` | si | - (Caddy saca el certificado para este nombre) |
| `Web__BaseUrl` | si | - (https; base de los enlaces de los correos) |
| `Cors__AllowedOrigins` | si | - (origenes separados por comas, https) |
| `ObjectStorage__Endpoint` | si | - |
| `ObjectStorage__Bucket` | si | - |
| `Smtp__Host` / `Smtp__From` | si | - |
| `DEPLOY_DIR` | no | `/opt/back-template` |
| `ASPNETCORE_ENVIRONMENT` | no | `Production` |
| `Jwt__Issuer` / `Jwt__Audience` | no | `back-template` / `back-template-clients` |
| `Jwt__AccessTokenMinutes` / `Jwt__RefreshTokenDays` | no | `15` / `14` |
| `Totp__Issuer` | no | `back-template` |
| `ForwardedHeaders__KnownNetworks` | no | `172.16.0.0/12` (red interna de Docker, donde vive Caddy) |
| `ObjectStorage__PublicEndpoint`, `__UseSsl`, `__Region`, `__PresignedExpiryMinutes` | no | vacio, `true`, `us-east-1`, `15` |
| `Smtp__Port` / `Smtp__User` / `Smtp__UseSsl` | no | `587` / vacio / `true` |
| `Seq__ServerUrl`, `Observability__OtlpEndpoint` | no | vacio (sin exportar) |
| `BackgroundJobs__OperatorTenantId` | no | vacio = nadie opera las tareas programadas desde la API |
| `BackgroundJobs__SessionPurge__DryRun` | no | `true` (la purga solo cuenta) |
| `BackgroundJobs__OrphanObjects__DryRun` | no | `true` (la purga de objetos huerfanos solo cuenta) |

Agregar un valor nuevo toca **tres** lugares: esta tabla, `docker-compose.prod.yml` y la lista `envs:` de
`_deploy-to-vps.yml`. Si falta en `envs:`, llega vacio aunque exista en GitHub.
