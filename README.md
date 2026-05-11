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
dotnet build back-template/Host/Host.csproj
dotnet test back-template/Tests/Tests.csproj --verbosity normal
```

---

## Estructura del proyecto

```
back-template/
├── back-template/                         # Solución .NET
│   ├── Domain/                            # Entidades + interfaces de repositorio (sin dependencias)
│   ├── Application/                       # Casos de uso — Request, Handler, Responses, DTOs
│   ├── Infrastructure/                    # PostgreSQL, Dapper, repositorios concretos
│   │   ├── PostgreSql/                    # Fábrica de conexiones + MainDapperDbConnection
│   │   ├── Persistence/SQLDB/Main/        # Clases ...Sql  (un archivo por tabla)
│   │   └── Repositories/                  # Implementaciones de repositorios de dominio
│   ├── WebApi/                            # Controllers + Presenters (class library, no SDK.Web)
│   │   ├── Base/BaseApiController.cs      # Base con IMediator protegido
│   │   └── EndPoints/{Modulo}/            # Controllers, Presenters, RequestBodies
│   ├── Host/                              # Punto de entrada — Program.cs
│   │   ├── Extensions/                    # JWT, CORS, Swagger, Health
│   │   ├── Services/Schema Migration/     # Archivos .sql de migraciones automáticas
│   │   └── appsettings.*.json             # Configuración por entorno
│   ├── Tests/                             # Tests xUnit
│   └── Common/                            # Submódulo Git — https://github.com/Raptor-Dev-Services/Common
├── compose.yaml                           # Docker Compose — producción
├── compose-dev.yaml                       # Docker Compose — desarrollo
├── compose-staging.yaml                   # Docker Compose — staging
└── docs/                                  # Documentación técnica
```

---

## Arquitectura

**Patrón:** Clean Architecture + CQRS + Mediator + Presenter

```
Domain
Application    → Domain
Infrastructure → Domain + Common
WebApi         → Application + Common
Host           → Application + Infrastructure + WebApi + Common
Common         (transversal — submódulo Git, sin lógica del proyecto)
```

**Regla absoluta:** las dependencias apuntan hacia adentro. `Application` nunca importa `Infrastructure`. `WebApi` nunca toca PostgreSQL.

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
| [docs/Back.md](docs/Back.md) | Arquitectura, patrones de caso de uso, presenter, controller, DI completo |
| [docs/DB.md](docs/DB.md) | Acceso a datos, clases Sql, migraciones, transacciones, SQL avanzado |
| [docs/Common.md](docs/Common.md) | Referencia completa del submódulo Common y todas sus APIs |
| [docs/Packages.md](docs/Packages.md) | Explicación explícita de cada paquete NuGet instalado |
| [docs/AddEndpoint.md](docs/AddEndpoint.md) | Guía paso a paso para agregar un módulo + DI lifetimes + checklist pre-build |
| [docs/Auth.md](docs/Auth.md) | Autenticación JWT HS256 — configuración, tokens, claims, roles |
| [docs/Testing.md](docs/Testing.md) | Tests xUnit — handlers, presenters, integración con PostgreSQL real |
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

## Agregar un módulo nuevo

Ver la guía completa en [docs/AddEndpoint.md](docs/AddEndpoint.md).

Resumen del flujo:

1. Migración SQL en `Host/Services/Schema Migration/Tables/`
2. Entidad + interfaz de repositorio en `Domain/`
3. Clase `...Sql` + repositorio concreto en `Infrastructure/` + DI
4. Caso de uso en `Application/UseCases/{Modulo}/` (Request + Handler + Responses)
5. Presenter en `WebApi/EndPoints/{Modulo}/Presenters/` + DI
6. Controller en `WebApi/EndPoints/{Modulo}/`
7. `dotnet build back-template/Host` — **0 errores**

---

## Reglas que no se negocian

1. `Application` nunca referencia `Infrastructure`
2. `WebApi` nunca accede a PostgreSQL ni a repositorios concretos
3. El mediador es `Common.Messaging.IMediator` — **nunca MediatR NuGet**
4. Todo SQL vive en clases `...Sql` — cero SQL inline en repositorios o handlers
5. Toda respuesta HTTP pasa por `ResultViewModel<TController>` — nunca datos directos
6. No secretos en `appsettings*.json` — variables de entorno
7. No editar el submódulo `Common` desde este repositorio
8. `dotnet build` desde `Host` — **0 errores** antes de cualquier commit
