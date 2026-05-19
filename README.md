# back-template

Plantilla de backend .NET 10 con Clean Architecture, CQRS, Mediator y Presenter Pattern.
Lista para producción: JWT, Serilog → Seq, OpenTelemetry → Jaeger, Prometheus, health checks, migraciones automáticas y Docker multi-stage distroless.

---

## Stack

| Categoría | Tecnología |
|-----------|-----------|
| Runtime | .NET 10 / C# 13 |
| Framework | ASP.NET Core 10 |
| Base de datos | PostgreSQL 17 |
| ORM | Dapper (SQL parametrizado) |
| Driver | Npgsql 10 |
| Mediator | Custom — `Common.Messaging` (NO MediatR NuGet) |
| Auth | JWT Bearer HS256 |
| Passwords | BCrypt.Net-Next |
| Logging | Serilog → Seq |
| Tracing | OpenTelemetry OTLP → Jaeger |
| Métricas | Prometheus en `/metrics` |
| Health | `/api/health` |
| Testing | xUnit + coverlet |
| Deploy | Docker multi-stage (distroless) |

---

## Prerrequisitos

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/)
- Git con soporte de submódulos

---

## Inicio rápido

### 1. Clonar con submódulos

```bash
git clone --recurse-submodules https://github.com/tu-org/back-template.git
cd back-template
```

Si ya clonaste sin el flag:

```bash
git submodule update --init --recursive
```

> El submódulo `Common` apunta a https://github.com/Raptor-Dev-Services/Common

### 2. Levantar infraestructura local

```bash
# Solo DB + Seq (recomendado — la API corre fuera de Docker con hot-reload)
docker compose -f compose-db.yaml up -d

# O stack completo en Docker (API + DB + Seq)
docker compose -f compose-dev.yaml up -d --build
```

`compose-db.yaml` levanta:
- **PostgreSQL 17** → `localhost:5432` (usuario/contraseña: `postgres/postgres`)
- **Seq** → `http://localhost:5341`

### 3. Revisar configuración local

`back-template/Host/appsettings.Local.json` ya trae la config lista para desarrollo en máquina:
Seq habilitado, Swagger habilitado, `IncludeSqlText: true` (SQL real en logs).

Copia el `.env` de ejemplo si quieres sobreescribir variables:

```bash
cp back-template/.env.example back-template/.env.development
```

### 4. Correr la API

```bash
dotnet run --project back-template/Host --launch-profile Local
```

| Recurso | URL |
|---------|-----|
| API | `http://localhost:5080` |
| Swagger UI | `http://localhost:5080/swagger` |
| Health check | `http://localhost:5080/api/health` |
| Prometheus metrics | `http://localhost:5080/metrics` |
| Seq (logs) | `http://localhost:5341` |

### 5. Verificar

```bash
# Health check
curl http://localhost:5080/api/health

# Endpoint de ejemplo (lista paginada)
curl "http://localhost:5080/api/example/users?page=1&pageSize=10"
```

### 6. Build y tests

```bash
# Build desde el host (valida toda la solución)
dotnet build back-template/Host.Api/Host.Api.csproj

# Tests por módulo
dotnet test back-template/Modules/Users/Users.Tests/Users.Tests.csproj --verbosity normal
dotnet test back-template/Modules/Tenancy/Tenancy.Tests/Tenancy.Tests.csproj --verbosity normal
dotnet test back-template/Shared/Authentication/Authentication.Tests/Authentication.Tests.csproj --verbosity normal

# O todos a la vez desde la solución
dotnet test back-template/back-template.slnx
```

---

## Estructura del proyecto

```
back-template/
├── back-template/                              # Solución .NET
│   ├── Common/                                 # Submódulo Git — abstracciones base (NO editar)
│   ├── Shared/
│   │   ├── Database/                           # DapperDbConnection<T>, DbConnectionFactory<T>, marcadores
│   │   ├── Web/                                # BaseApiController (Shared.Web.csproj)
│   │   └── Authentication/
│   │       ├── Authentication.Contracts/       # Eventos de integración públicos
│   │       ├── Authentication.Domain/          # UserCredential, RefreshToken, interfaces
│   │       ├── Authentication.Application/     # Register, Login, RefreshToken handlers
│   │       ├── Authentication.Infrastructure/  # CredentialsSql, JwtTokenService, repositorios
│   │       ├── Authentication.Presentation/    # AuthController, presenters
│   │       └── Authentication.Tests/          # Tests unitarios + arquitectura
│   ├── Modules/
│   │   ├── Tenancy/
│   │   │   ├── Tenancy.Contracts/              # ITenancyApi, TenantDto, BranchDto
│   │   │   ├── Tenancy.Domain/
│   │   │   ├── Tenancy.Application/            # TenancyApi + handlers
│   │   │   ├── Tenancy.Infrastructure/
│   │   │   ├── Tenancy.Presentation/
│   │   │   └── Tenancy.Tests/
│   │   └── Users/
│   │       ├── Users.Contracts/
│   │       ├── Users.Domain/
│   │       ├── Users.Application/
│   │       ├── Users.Infrastructure/
│   │       ├── Users.Presentation/
│   │       └── Users.Tests/
│   ├── Host.Api/                               # Punto de entrada — composición final
│   │   ├── Program.cs
│   │   ├── Extensions/                         # JWT, CORS, Swagger, Health
│   │   ├── Middleware/
│   │   ├── Services/Schema Migration/Tables/   # Migraciones SQL automáticas
│   │   └── appsettings.*.json
│   └── Tests/                                  # Tests de integración cross-módulo (opcional)
├── compose.yaml                                # Docker Compose — producción
├── compose-dev.yaml                            # Docker Compose — desarrollo
├── compose-staging.yaml                        # Docker Compose — staging
└── docs/                                       # Documentación técnica
```

---

## Arquitectura

**Patrón:** Monolito Modular + Clean Architecture + CQRS + Mediator + Presenter

Cada módulo tiene **6 proyectos `.csproj`** con límites reales en tiempo de compilación:

```
{Modulo}.Contracts      → (sin dependencias)
{Modulo}.Domain         → Common
{Modulo}.Application    → Common + Domain + Contracts
{Modulo}.Infrastructure → Common + Domain + Shared.Database   (NO referencia Application)
{Modulo}.Presentation   → Common + Application + Shared.Web   (NO referencia Infrastructure)
{Modulo}.Tests          → todos + xUnit + NSubstitute + NetArchTest.Rules

Host.Api  → Application + Infrastructure + Presentation  (de cada módulo)
```

**Regla absoluta:** `Application` nunca importa `Infrastructure`. Un módulo solo puede referenciar `.Contracts` de otro módulo — nunca su Domain, Application, Infrastructure ni Presentation.

### Flujo de una request

```
HTTP Request
    ↓
Controller  →  _ = await Mediator.Send(Request, ct)
                        ↓
               Handler.Handle(request, ct)
                        ↓  return Success | Failure
               InteractorPipeline
                        ↓  await Mediator.Publish(response)
               Presenter.Handle(response, ct)
                        ↓  _viewModel.Set() | OK() | Fail()
Controller  →  IsSuccess ? Ok(_viewModel) : StatusCode(500, _viewModel)
    ↓
HTTP Response  { data, isSuccess, message, utcTimeStamp }
```

---

## Documentación

| Documento | Contenido |
|-----------|-----------|
| [docs/Concepts.md](docs/Concepts.md) | Anatomía del proyecto — qué es cada cosa y dónde va (Entity, DTO, Handler, Presenter, etc.) |
| [docs/Back.md](docs/Back.md) | Arquitectura modular, patrones de caso de uso, presenter, controller, DI completo |
| [docs/ProgramCs.md](docs/ProgramCs.md) | Program.cs línea por línea — por qué existe cada sección y el orden del middleware |
| [docs/Modules.md](docs/Modules.md) | Ciclo de vida de módulos — agregar, acoplar, desacoplar, extraer a repo propio, submodule |
| [docs/AddEndpoint.md](docs/AddEndpoint.md) | Guía paso a paso para agregar un endpoint — desde migración hasta controller |
| [docs/MultiTenancy.md](docs/MultiTenancy.md) | Cómo fluye TenantId desde el JWT hasta el WHERE del SQL — capa por capa |
| [docs/Config.md](docs/Config.md) | appsettings.json, entornos, variables de entorno, Docker .env, secretos |
| [docs/DB.md](docs/DB.md) | DapperDbConnection\<T\>, clases Sql, migraciones, transacciones, SQL avanzado |
| [docs/Testing.md](docs/Testing.md) | Tests de arquitectura (NetArchTest) + tests unitarios (NSubstitute) + integración |
| [docs/Common.md](docs/Common.md) | Referencia completa del submódulo Common — IMediator, InteractorPipeline, ISuccess, etc. |
| [docs/Auth.md](docs/Auth.md) | Autenticación JWT HS256 — configuración, tokens, claims, roles |
| [docs/Packages.md](docs/Packages.md) | Explicación explícita de cada paquete NuGet instalado |
| [docs/Observability.md](docs/Observability.md) | Logging Serilog/Seq, trazas OpenTelemetry/Jaeger, métricas Prometheus |
| [docs/Pagination.md](docs/Pagination.md) | PagedResult\<T\>, queries SQL paginados, refresh tokens, background services |
| [docs/CurrentUser.md](docs/CurrentUser.md) | ICurrentUserService, claims, roles en Application, audit trail, HttpClient+Polly |
| [docs/Errors.md](docs/Errors.md) | Problem Details, global exception handler, Result vs excepciones, soft delete |
| [docs/Security.md](docs/Security.md) | Rate limiting, security headers, HTTPS, CORS producción, FluentValidation, OWASP |
| [docs/Deployment.md](docs/Deployment.md) | VPS, Docker Compose, Nginx, Caddy, SSL, systemd, checklist pre-deploy |
| [CLAUDE.md](CLAUDE.md) | Guía de referencia para Claude Code — reglas de arquitectura y convenciones |

Para documentación pedagógica (C#, patrones de diseño, arquitectura, Docker) ver el repo [`dev-notes`](../dev-notes).

---

## Entornos

| Entorno | Swagger | SQL en logs | Perfil de lanzamiento |
|---------|---------|-------------|----------------------|
| `Local` | ✓ | ✓ | `--launch-profile Local` |
| `Development` | ✓ | ✗ | `--launch-profile Development` |
| `Staging` | ✓ | ✗ | `--launch-profile Staging` |
| `Production` | ✗ | ✗ | `--launch-profile Production` |

`Local` es el perfil de trabajo diario en máquina. Activa el texto SQL en los logs de Serilog para debug.

---

## Docker

```bash
# Desarrollo (API + PostgreSQL + Seq)
docker compose -f compose-dev.yaml up -d

# Staging
docker compose -f compose-staging.yaml up -d

# Producción
docker compose up -d
```

La imagen final usa `mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled` (distroless — sin shell, sin root, superficie de ataque mínima).

---

## Variables de entorno requeridas (producción)

```env
POSTGRES_PASSWORD=<contraseña segura>
JWT_KEY=<clave JWT mínimo 32 caracteres>
```

Ver `.env.example` para la lista completa.

---

## Agregar un módulo o un endpoint nuevo

**Nuevo módulo** — ver la guía completa en [docs/Modules.md](docs/Modules.md):
1. Crear 6 proyectos `.csproj` bajo `Modules/{NombreModulo}/`
2. Registrar en `back-template.slnx`
3. Referenciar Application, Infrastructure y Presentation en `Host.Api.csproj`
4. Agregar a `AddMediator(...)` y llamar los 3 `Add{Modulo}*Services()` en `Program.cs`

**Nuevo endpoint en módulo existente** — ver la guía completa en [docs/AddEndpoint.md](docs/AddEndpoint.md):
1. Migración SQL en `Host.Api/Services/Schema Migration/Tables/` (si es tabla nueva)
2. Entidad + interfaz de repositorio en `{Modulo}.Domain/`
3. Clase `...Sql` + repositorio en `{Modulo}.Infrastructure/` + DI
4. Caso de uso en `{Modulo}.Application/UseCases/` (Request + Handler + Responses)
5. Presenter en `{Modulo}.Presentation/Presenters/` + registro manual en `ServiceCollectionEx`
6. Endpoint en el controller de `{Modulo}.Presentation/Controllers/`
7. `dotnet build back-template/Host.Api/Host.Api.csproj` — **0 errores**

---

## Reglas que no se negocian

1. `Application` nunca referencia `Infrastructure`
2. Un módulo solo puede referenciar `.Contracts` de otro módulo — nunca Domain, Application, Infrastructure ni Presentation
3. `Infrastructure` nunca referencia `Presentation`
4. El mediador es `Common.Messaging.IMediator` — **nunca MediatR NuGet**
5. Todo SQL vive en clases `...Sql` — cero SQL inline en repositorios o handlers
6. Toda respuesta HTTP pasa por `ResultViewModel<TController>` — nunca datos directos
7. Los presenters se registran manualmente en `Presentation/ServiceCollectionEx.cs` — nunca vía scan de AddMediator
8. `AddMediator()` se llama **una sola vez** en `Host.Api/Program.cs` con todos los ensamblados Application
9. No secretos en `appsettings*.json` — variables de entorno
10. No editar el submódulo `Common` desde este repositorio
11. `dotnet build` desde `Host.Api` — **0 errores** antes de cualquier commit
