# TAGS — Versiones del template

Este repo mantiene tags anotados que representan puntos de partida concretos y distintos.
Cada tag es un template completo y funcional — elige el que corresponda al tipo de proyecto que vas a iniciar.

---

## `monolito-modular-v2.0.0` - chasis generico de SaaS multi-tenant (2026-09-24)

**Commit:** el que marca el tag (`git rev-list -n 1 monolito-modular-v2.0.0`).

Reescritura como chasis de SaaS multi-tenant, portado del de un producto en produccion sin su logica de negocio
(ver README y CHANGELOG). **Rompe** con los tags `v1.0.0` de abajo: cambian rutas (`/api/v1/...`), esquema
(migraciones EF, RLS) y el contrato de autenticacion (bootstrap + invitaciones en vez de `/register`, permisos
en vez de roles). Su companero de front es `front-template` `v0.2.0`, que habla este mismo contrato.

- **Estructura:** `src/Host`, `src/Shared/{Shared.Kernel, Shared.Infrastructure, Shared.Web, Authentication/*}`,
  `src/Modules/{Tenancy, Users}`, `tests/` (6 proyectos).
- **Stack:** .NET 10, EF Core 10 + Npgsql, PostgreSQL 17 con RLS, Common v2.1.3 (submodulo `dace97a`),
  MinIO/S3, Serilog + Seq, imagen chiseled.
- **Incluye:** errores tipados con un solo envelope (validacion 400), auditoria y soft delete por convencion,
  UTC, RLS con guarda de rol, bootstrap con secreto, RBAC por permisos, refresh hasheado con deteccion de reuso,
  2FA TOTP, rate limit, tareas programadas (purga de sesiones y de objetos huerfanos), archivos con propiedad por
  tenant, CI/CD por tag con rollback y acciones fijadas por SHA.
- **Verificado:** 195 pruebas (88 de integracion con Postgres real) y corrida en vivo contra el devstack.
- **Cuando usarlo:** cualquier SaaS multi-tenant nuevo del ecosistema.
- **Clonar:**

```bash
git clone --recurse-submodules --branch monolito-modular-v2.0.0 https://github.com/Raptor-Dev-Services/back-template.git
```

> El nombre no empieza con `v` a proposito: `deploy.yml` se dispara con tags `v*`, que son los de los PRODUCTOS
> que nacen de la plantilla. Un tag de la plantilla no debe desplegar nada.

---

## Tags anteriores (arquitectura previa)

## Resumen comparativo

| | `clean-arch-dapper-single-tenant-v1.0.0` | `monolito-modular-v1.0.0` |
|---|---|---|
| **Arquitectura** | 4 capas planas | Monolito modular — 6 proyectos por módulo |
| **ORM** | Dapper | EF Core 10 |
| **Multi-tenancy** | No | Sí — Global Query Filters por `tenant_id` |
| **Autenticación** | JWT + Refresh Token | JWT + Refresh Token + BCrypt |
| **Migraciones DB** | Scripts SQL numerados | `EnsureCreated` + `EntityTypeConfiguration` |
| **Tests** | Tests de ejemplo básicos | Architecture tests + Unit tests por módulo |
| **Ideal para** | API de un solo cliente | SaaS multi-tenant |

---

## `clean-arch-dapper-single-tenant-v1.0.0`

**Commit:** `a910a45`

Template base con Clean Architecture en 4 capas planas, sin multi-tenancy y usando Dapper como ORM.
Punto de partida para proyectos de un solo cliente o empresa.

### Estructura de proyectos

```
back-template/
├── Domain/              → Entidades + interfaces de repositorio
├── Application/         → Handlers + DTOs + Requests/Responses
├── Infrastructure/      → Repositorios con Dapper + conexiones PostgreSQL
├── WebApi/              → Controllers + Presenters + BaseApiController
└── Host/                → Program.cs + extensiones + appsettings
```

### Stack

| Categoría | Tecnología |
|-----------|-----------|
| Runtime | .NET 10 / C# 13 |
| ORM | Dapper |
| Base de datos | PostgreSQL 17 |
| Conexiones | `MainDapperDbConnection`, `MainDbConnectionFactory` |
| Migraciones | Scripts SQL numerados en `Host/Services/Schema Migration/` |
| Auth | JWT Bearer HS256 + Refresh Token |
| Mediator | Custom — `Common.Messaging` |
| Patrón respuesta | Presenter + `ResultViewModel<TController>` |
| Logging | Serilog → Seq |
| Tracing | OpenTelemetry OTLP → Jaeger |
| Métricas | Prometheus en `/metrics` |
| Health | `/api/health` |
| Deploy | Docker multi-stage (distroless) |

### Caso de uso de ejemplo incluido

`ExampleUser` — CRUD completo listo para borrar y reemplazar con tu entidad:

| Use Case | Endpoint |
|----------|----------|
| `GetExampleUser` | `GET /api/example-users/{id}` |
| `GetExampleUsers` | `GET /api/example-users` |
| `InsertExampleUser` | `POST /api/example-users` |
| `UpdateExampleUser` | `PUT /api/example-users/{id}` |
| `DisableExampleUser` | `DELETE /api/example-users/{id}` |

### Acceso a datos — patrón Dapper

```
Infrastructure/
└── Persistence/SQLDB/Main/Example/
    └── ExampleUsersSql.cs     ← SQL inline como strings en clases estáticas
└── Repositories/Example/
    └── ExampleUserRepository.cs  ← inyecta MainDapperDbConnection
```

### Cuándo usar este tag

- El proyecto tiene un solo cliente o empresa (no SaaS)
- Prefieres control total sobre el SQL — joins complejos, queries optimizadas a mano
- No necesitas fronteras de módulo — el equipo es pequeño o el scope es acotado
- Migración gradual: empezar simple y crecer solo si es necesario

### Cómo clonar en este tag

```bash
git clone --recurse-submodules https://github.com/Raptor-Dev-Services/back-template.git
cd back-template
git checkout clean-arch-dapper-single-tenant-v1.0.0
```

---

## `monolito-modular-v1.0.0`

**Commit:** `6b08610`

Template de arquitectura modular con fronteras de módulo reales en tiempo de compilación,
EF Core 10 con Global Query Filters para multi-tenancy y estructura de 6 proyectos por módulo.

### Estructura de proyectos

```
back-template/
├── Common/                                  ← Submódulo Git — NO editar
├── Shared/
│   ├── Database/                            ← AppDbContext + EntityTypeConfigurations
│   ├── Web/                                 ← BaseApiController
│   └── Authentication/
│       ├── Authentication.Contracts/        ← Eventos de integración públicos
│       ├── Authentication.Domain/           ← UserCredential, RefreshToken
│       ├── Authentication.Application/      ← Register, Login, RefreshToken
│       ├── Authentication.Infrastructure/   ← JwtTokenService, repositorios
│       ├── Authentication.Presentation/     ← AuthController + presenters
│       └── Authentication.Tests/            ← Architecture (7) + Unit (3)
├── Modules/
│   ├── Tenancy/
│   │   ├── Tenancy.Contracts/               ← ITenancyApi, TenantDto
│   │   ├── Tenancy.Domain/
│   │   ├── Tenancy.Application/
│   │   ├── Tenancy.Infrastructure/
│   │   ├── Tenancy.Presentation/
│   │   └── Tenancy.Tests/                   ← Architecture (7) + Unit (2)
│   └── Users/
│       ├── Users.Contracts/
│       ├── Users.Domain/
│       ├── Users.Application/
│       ├── Users.Infrastructure/
│       ├── Users.Presentation/
│       └── Users.Tests/                     ← Architecture (7) + Unit (2)
└── Host.Api/                                ← Composición final — Program.cs
```

### Stack

| Categoría | Tecnología |
|-----------|-----------|
| Runtime | .NET 10 / C# 13 |
| ORM | EF Core 10 (Npgsql 10.0.1) |
| Base de datos | PostgreSQL 17 |
| Esquema DB | `EnsureCreated` + `EntityTypeConfiguration<T>` por entidad |
| Multi-tenancy | `ITenantContextAccessor` + Global Query Filters en `AppDbContext` |
| Auth | JWT Bearer HS256 + BCrypt.Net-Next (workFactor 12) + Refresh Token rotante |
| Mediator | Custom — `Common.Messaging` |
| Patrón respuesta | Presenter + `ResultViewModel<TController>` |
| Logging | Serilog → Seq |
| Tracing | OpenTelemetry OTLP → Jaeger |
| Métricas | Prometheus en `/metrics` |
| Health | `/api/health` |
| Testing | xUnit + NSubstitute + NetArchTest.Rules |
| Deploy | Docker multi-stage (distroless) |

### Módulos incluidos

#### Tenancy
Gestión del tenant (empresa que contrata el SaaS).

| Use Case | Endpoint | Rol |
|----------|----------|-----|
| `RegisterTenant` | `POST /api/tenancy/register` | Público |
| `GetTenant` | `GET /api/tenancy` | Admin |
| `UpdateTenant` | `PUT /api/tenancy` | Admin |

#### Users
Perfiles de usuario dentro de un tenant. Filtro automático por `tenant_id`.

| Use Case | Endpoint | Rol |
|----------|----------|-----|
| `GetUserProfile` | `GET /api/users/{id}` | Autenticado |
| `GetUserProfiles` | `GET /api/users` | Admin |
| `UpdateUserProfile` | `PUT /api/users/{id}` | Admin |
| `DisableUserProfile` | `DELETE /api/users/{id}` | Admin |

#### Authentication (compartido)
Login, registro y refresh token. Sin filtro de tenant — `IgnoreQueryFilters()` en repositorios de auth.

| Use Case | Endpoint |
|----------|----------|
| `Register` | `POST /api/auth/register` |
| `Login` | `POST /api/auth/login` |
| `RefreshToken` | `POST /api/auth/refresh` |

### Multi-tenancy — cómo funciona

```
JWT claim: tenant_id = "42"
    ↓
TenantClaimsMiddleware  →  ITenantContextAccessor.Current = TenantContext("42")
    ↓
AppDbContext (Scoped)   →  CurrentTenantId = 42
    ↓
Global Query Filter     →  WHERE tenant_id = 42  (automático en toda query)
```

Tablas con filtro automático: `dbo.credentials`, `dbo.user_profiles`.
Tablas sin filtro: `dbo.tenants` (no tiene `tenant_id` — es la raíz de la jerarquía).

### Reglas de dependencia entre módulos

```
{Modulo}.Contracts      → sin dependencias externas
{Modulo}.Domain         → Common
{Modulo}.Application    → Common + Domain + Contracts
{Modulo}.Infrastructure → Common + Domain + Shared.Database   (NUNCA Application)
{Modulo}.Presentation   → Common + Application + Shared.Web   (NUNCA Infrastructure)
{Modulo}.Tests          → todos los anteriores

Módulo A → solo puede referenciar Módulo B via B.Contracts
Host.Api  → Application + Infrastructure + Presentation de cada módulo
```

### Tests incluidos — por módulo

Cada módulo tiene 2 tipos de tests:

**Architecture tests** (NetArchTest.Rules) — validan que las reglas de dependencia no se rompan:
- Domain no referencia Application, Infrastructure ni Presentation
- Application no referencia Infrastructure ni Presentation
- Infrastructure no referencia Application ni Presentation
- Presentation no referencia Infrastructure
- Handlers están en Application
- Repositories están en Infrastructure
- Controllers están en Presentation

**Unit tests** (xUnit + NSubstitute) — validan la lógica de negocio sin base de datos:
- Handler retorna `Success` cuando la entidad existe
- Handler retorna `NotFoundFailure` cuando la entidad no existe
- Handler retorna `Failure` cuando el repositorio lanza excepción

### Cuándo usar este tag

- Estás construyendo un SaaS y necesitas aislar datos por empresa (`tenant_id`) desde el primer día
- El proyecto va a crecer — los módulos permiten escalar el equipo sin conflictos
- Quieres fronteras de compilación que impidan dependencias circulares o violaciones de arquitectura
- Prefieres EF Core (LINQ, migrations, type safety) sobre SQL manual

### Cómo clonar en este tag

```bash
git clone --recurse-submodules https://github.com/Raptor-Dev-Services/back-template.git
cd back-template
git checkout monolito-modular-v1.0.0
```

O clonar directamente en el tag:

```bash
git clone --recurse-submodules --branch monolito-modular-v1.0.0 \
  https://github.com/Raptor-Dev-Services/back-template.git
```

---

## Ver el mensaje completo de un tag

```bash
git show clean-arch-dapper-single-tenant-v1.0.0 --no-patch
git show monolito-modular-v1.0.0 --no-patch
```

---

*Rogelio Arriaga Gonzalez — Raptor Dev Services*
