# back-template

Plantilla de backend **.NET 10** para un SaaS multi-tenant: monolito modular con Clean Architecture, el patron
de caso de uso de la libreria compartida **Common** (Request / Handler / Responses / Presenter / Controller) y
el chasis que todo producto necesita desde el dia uno: aislamiento entre tenants en dos capas (filtro de EF +
RLS de Postgres), autenticacion completa con RBAC y segundo factor, bitacora de acciones, tareas programadas,
almacenamiento de objetos, salud, observabilidad, imagen Docker endurecida y pipeline de entrega por tag.

No trae logica de negocio: los modulos `Tenancy` y `Users` son el minimo que el chasis necesita para existir.

## Stack

| | |
|---|---|
| Runtime | .NET 10 (SDK fijado en `global.json`), ASP.NET Core |
| Base de datos | PostgreSQL 17, EF Core 10 + Npgsql, migraciones EF (nunca al arrancar) |
| Aislamiento | Filtro global `tenant` + Row-Level Security con `app.tenant_id`, rol de la app sin BYPASSRLS |
| Mediador | `Common.Messaging` (submodulo `Common/`, **no** MediatR) |
| Auth | JWT HS256 (15 min) + refresh token rotado y hasheado, RBAC por permisos, TOTP con codigos de recuperacion |
| Archivos | MinIO / S3, bucket privado, URLs prefirmadas, propiedad por tenant |
| Logs | Serilog JSON + Seq, correlation id, sin query string |
| Pruebas | xUnit, NSubstitute, NetArchTest, Testcontainers (Postgres real) |
| Entrega | Imagen chiseled + sonda .NET, GHCR, deploy por SSH, Caddy |

## Arrancar en local (5 minutos)

Requisitos: .NET 10 SDK, Docker, y el **devstack** compartido arriba (un solo Postgres, MinIO, Mailpit y Seq
para todos los productos de la maquina). Sin devstack, ver el modo `standalone` en [docs/DEV-STACK.md](docs/DEV-STACK.md).

```bash
git clone --recurse-submodules <url> back-template && cd back-template
cp .env.example .env              # y rellena Jwt__Key, Totp__EncryptionKey, Bootstrap__Secret (32+ bytes)
./scripts/dev-db.sh all           # roles + base backtemplate + migraciones + RLS (idempotente)
dotnet run --project src/Host/Host.Api
curl http://localhost:5060/health/ready
```

Primer tenant y su administrador (el registro anonimo no existe):

```bash
curl -X POST http://localhost:5060/api/v1/bootstrap/tenant -H "Content-Type: application/json" \
  -H "X-Bootstrap-Secret: <Bootstrap__Secret>" \
  -d '{"tenantName":"Acme","tenantSlug":"acme","adminEmail":"admin@acme.test","adminPassword":"Una-Contrasena-Larga-1","adminFullName":"Admin"}'
```

`back-template.http` trae el resto de peticiones listas para VS Code / Rider. Swagger en `/swagger` (solo Development).

## Estructura

```
Common/                          submodulo de la libreria compartida (fijado a un commit; NO se edita aqui)
src/Host/Host.Api/               composition root: Program.cs, extensiones, AppDbContextFactory
src/Host/HealthProbe/            sonda de salud de la imagen (la imagen chiseled no trae curl ni shell)
src/Shared/Shared.Kernel/        contratos transversales sin dependencias: errores, resultados, permisos, puertos
src/Shared/Shared.Infrastructure/ AppDbContext unico, migraciones, RLS, correo, bitacora, tareas, almacenamiento
src/Shared/Shared.Web/           BaseApiController, envelope de errores, tenancy, autorizacion, rate limit
src/Shared/Authentication/*      modulo de identidad (5 proyectos): login, sesiones, RBAC, 2FA, invitaciones
src/Modules/Tenancy/*            tenants, bitacora, operacion de tareas, archivos
src/Modules/Users/*              perfiles de usuario
tests/                           Api.IntegrationTests, Architecture.Tests, Authentication.Tests, Shared.Tests, Users.Tests
scripts/                         dev-db.sh, provisioning del devstack, validadores
docs/                            guias, ADRs (docs/adr), despliegue (docs/deploy)
```

## Endpoints

Todas las respuestas usan el envelope `{ data, isSuccess, message, utcTimeStamp }`. Los listados paginan con
`data = { items, page, pageSize, totalCount, totalPages }` (`pageSize` por omision 20, maximo 100).

| Ruta | Acceso |
|---|---|
| `POST /api/v1/auth/login`, `login/2fa`, `refresh`, `logout`, `password/forgot`, `password/reset` | anonimo, limite `auth` por IP |
| `POST /api/v1/bootstrap/tenant` | header `X-Bootstrap-Secret`; apagado si no hay secreto |
| `GET /api/v1/account/me`, `POST password`, `POST 2fa/setup`, `2fa/enable`, `2fa/disable` | sesion |
| `POST /api/v1/accounts` (invitar), `{id}/lock`, `{id}/unlock` | `users.manage` |
| `GET /api/v1/roles` | `users.read` |
| `GET /api/v1/users`, `GET /api/v1/users/{id}` | `users.read` |
| `PUT /api/v1/users/{id}`, `DELETE /api/v1/users/{id}` (baja reversible) | `users.manage` |
| `GET /api/v1/audit-log` | `audit.read` |
| `GET /api/v1/automated-tasks`, `PUT {code}/enabled`, `POST {code}/run`, `GET {code}/runs` | `tasks.manage` **y** tenant operador |
| `POST /api/v1/files`; `GET /api/v1/files/{**key}`, `POST urls` | `files.write`; `files.read` |
| `GET /health/live`, `/health/ready` | anonimo |

Contrato con el front: los permisos viajan como claims `permission` (uno por codigo), el refresh y el logout
leen `refreshToken` del body, y el tenant sale **solo** del claim `tenant_id` del JWT.

## Puertas

```bash
dotnet build back-template.slnx -warnaserror   # 0 errores, 0 warnings
dotnet test  back-template.slnx                # necesita Docker (Testcontainers)
python scripts/check-prerelease-deps.py        # sin dependencias prerelease
```

El CI (`.github/workflows/ci.yml`) corre lo mismo, construye la imagen y escanea secretos con gitleaks.

## Documentacion

- [CLAUDE.md](CLAUDE.md) / [AGENTS.md](AGENTS.md): reglas para trabajar en el repo (personas y agentes).
- [docs/DEV-STACK.md](docs/DEV-STACK.md): devstack, puertos, base de datos local.
- [docs/deploy/](docs/deploy/): secretos del pipeline y runbook de produccion.
- [docs/adr/](docs/adr/): decisiones de arquitectura.
- `docs/*.md`: guias por tema (modulos, errores, seguridad, pruebas, ...).
- [BOARD.md](BOARD.md), [CHANGELOG.md](CHANGELOG.md), [HANDOFF.md](HANDOFF.md), [TAGS.md](TAGS.md).

## Configuracion de Claude Code

`.claude/` se versiona (no esta ignorada) pero la plantilla no la trae poblada: en un repo nuevo corre
`/catalogo install` desde el catalogo para instalar agentes, skills y reglas adaptados al stack.
