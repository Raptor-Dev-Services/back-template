# Backend: vista general

`back-template` es un **monolito modular** en .NET 10 sobre PostgreSQL, multi-tenant, con Clean Architecture por
modulo y el patron **Request / Handler / Responses / Presenter / Controller** de la libreria compartida `Common`.
Esta pagina es el mapa; cada tema tiene su documento.

## Layout

```
back-template/
├── Common/                         submodulo git (Common v2), SOLO LECTURA -> Common.md
├── src/
│   ├── Host/
│   │   ├── Host.Api/               composition root: Program.cs, Extensions/, appsettings*, AppDbContextFactory
│   │   └── HealthProbe/            sonda de /health/ready para el HEALTHCHECK de la imagen chiseled
│   ├── Shared/
│   │   ├── Shared.Kernel/          contratos sin dependencias: errores, resultados, paginado, seguridad,
│   │   │                           contexto (ICurrentUser, IUnitOfWork), auditoria, correo, tareas, archivos
│   │   ├── Shared.Infrastructure/  AppDbContext unico, migraciones, RLS, correo, bitacora, tareas, MinIO
│   │   ├── Shared.Web/             BaseApiController, envelope/errores, autorizacion por permiso,
│   │   │                           middlewares (tenant, tenant suspendido, correlacion, cabeceras), UTC
│   │   └── Authentication/         modulo de identidad: Contracts, Domain, Application, Infrastructure, Presentation
│   └── Modules/
│       ├── Tenancy/                tenant, bitacora (lectura), tareas programadas (operacion), archivos
│       └── Users/                  perfiles de usuario
├── tests/                          5 proyectos -> Testing.md
├── scripts/                        dev-db.sh, provisioning del devstack, validadores
├── Dockerfile, compose-dev.yaml, docker-compose.prod.yml, Caddyfile   -> Deployment.md
└── .github/workflows/              CI, deploy por tag, rollback, dependencias estables
```

## Documentos

| Tema | Documento |
|---|---|
| Conceptos y flujo de una peticion | [Concepts.md](Concepts.md) |
| Modulos, capas y dependencias | [Modules.md](Modules.md) |
| Agregar un endpoint paso a paso | [AddEndpoint.md](AddEndpoint.md) |
| Errores y envelope | [Errors.md](Errors.md) |
| Paginado | [Pagination.md](Pagination.md) |
| Base de datos, migraciones, RLS | [DB.md](DB.md) |
| Multi-tenancy | [MultiTenancy.md](MultiTenancy.md) |
| Autenticacion, RBAC, 2FA | [Auth.md](Auth.md) |
| Usuario actual | [CurrentUser.md](CurrentUser.md) |
| Seguridad (borde HTTP, secretos) | [Security.md](Security.md) |
| Configuracion | [Config.md](Config.md) |
| Program.cs y el orden del pipeline | [ProgramCs.md](ProgramCs.md) |
| Observabilidad y salud | [Observability.md](Observability.md) |
| Paquetes | [Packages.md](Packages.md) |
| Libreria Common | [Common.md](Common.md) |
| Pruebas | [Testing.md](Testing.md) |
| Contenedores y despliegue | [Deployment.md](Deployment.md), [deploy/](deploy/) |

## Superficie de la API (v1)

| Ruta | Que hace | Permiso |
|---|---|---|
| `POST /api/v1/bootstrap/tenant` | Crea el primer tenant y su administrador (header `X-Bootstrap-Secret`) | anonimo + secreto |
| `POST /api/v1/auth/login`, `/login/2fa`, `/refresh`, `/logout`, `/password/forgot`, `/password/reset` | Sesion | anonimo (limite `auth`) |
| `GET /api/v1/account/me`, `POST /account/password`, `/account/2fa/{setup,enable,disable}` | Cuenta propia | autenticado |
| `POST /api/v1/accounts`, `/accounts/{id}/lock`, `/unlock` | Invitar y bloquear | `users.manage` |
| `GET /api/v1/roles` | Roles y sus permisos | `users.read` |
| `GET /api/v1/users`, `GET/PUT/DELETE /api/v1/users/{id}` | Perfiles | `users.read` / `users.manage` |
| `GET /api/v1/audit-log` | Bitacora del tenant | `audit.read` |
| `/api/v1/automated-tasks...` | Operar tareas programadas (solo tenant operador) | `tasks.manage` |
| `POST /api/v1/files`, `GET /api/v1/files/{**key}`, `POST /api/v1/files/urls` | Archivos del tenant | `files.write` / `files.read` |
| `GET /health/live`, `GET /health/ready` | Sondas | anonimo |
