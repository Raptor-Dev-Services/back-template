# CLAUDE.md

## Propósito

Guía de referencia para Claude Code al trabajar en `back-template`.
Describe la arquitectura, convenciones y reglas que deben respetarse en todo cambio, generación o revisión de código.

---

## Arquitectura — Monolito Modular + Clean Architecture

**Patrón:** Modular Monolith + Clean Architecture + CQRS + Mediator + Presenter

Cada módulo funcional se compone de **6 proyectos `.csproj` independientes**. Los límites de módulo se refuerzan en tiempo de compilación.

**Dirección de dependencias dentro de un módulo:**

```
Domain           → (sin dependencias externas)
Contracts        → (sin dependencias externas — solo tipos POCO públicos)
Application      → Common + Domain + Contracts
Infrastructure   → Common + Domain + Shared.Database   (NO referencia Application)
Presentation     → Common + Application + Shared.Web   (NO referencia Infrastructure)
Tests            → Domain + Application + Infrastructure + Presentation
Host.Api         → Application + Infrastructure + Presentation (por cada módulo)
```

**Regla absoluta:** `Application` nunca referencia `Infrastructure`. `Infrastructure` nunca referencia `Application`. Los módulos nunca se referencian entre sí — la comunicación pasa por `{Modulo}.Contracts`.

---

## Árbol de directorios

```
back-template/
├── Common/                                          → Submódulo Git (NO editar)
├── Shared/
│   ├── Database/                                    → Infraestructura DB compartida (sin ASP.NET)
│   │   ├── MainDbConnection.cs                      ← Marcador BD principal
│   │   ├── ReadonlyDbConnection.cs                  ← Marcador BD secundaria (ejemplo)
│   │   ├── DbConnectionFactory.cs                   ← Factory genérica open-generic
│   │   ├── DapperDbConnection.cs                    ← Conexión genérica open-generic
│   │   └── ServiceCollectionEx.cs                   → AddMainDatabase()
│   └── Web/                                         → Infraestructura web compartida (con ASP.NET)
│       └── BaseApiController.cs                     → namespace Shared.Web
├── Modules/
│   ├── Tenancy/                                     → Módulo Tenancy (Tenant + Branch)
│   │   ├── Tenancy.Contracts/                       → DTOs + Integration Events (POCO, sin deps)
│   │   ├── Tenancy.Domain/                          → Entidades + interfaces de repositorio
│   │   ├── Tenancy.Application/                     → Use cases (TenancyApi)
│   │   ├── Tenancy.Infrastructure/                  → SQL objects + repositorios
│   │   ├── Tenancy.Presentation/                    → Controllers + ServiceCollectionEx
│   │   └── Tenancy.Tests/                           → Tests arquitectura + unitarios
│   └── Users/                                       → Módulo Users (perfiles de usuario)
│       ├── Users.Contracts/                         → UserProfileDto + Integration Events
│       ├── Users.Domain/                            → UserProfile entity + IUserProfileRepository
│       ├── Users.Application/                       → GetUserProfile, GetUserProfiles, Update, Disable
│       ├── Users.Infrastructure/                    → UserProfilesSql + UserProfileRepository
│       ├── Users.Presentation/                      → UsersController + 4 Presenters
│       └── Users.Tests/                             → Tests arquitectura (7) + unitarios (2)
├── Shared/Authentication/                           → Módulo compartido Auth
│   ├── Authentication.Contracts/                    → TokenDto + UserShouldBeCreatedIntegrationEvent
│   ├── Authentication.Domain/                       → UserCredential + RefreshToken entities
│   ├── Authentication.Application/                  → Login + Register + RefreshToken handlers
│   ├── Authentication.Infrastructure/               → CredentialsSql + RefreshTokensSql + JWT + BCrypt
│   ├── Authentication.Presentation/                 → AuthController + 3 Presenters
│   └── Authentication.Tests/                        → Tests arquitectura (7) + unitarios (3)
└── Host.Api/                                        → Punto de entrada — Program.cs
    ├── Extensions/                                  → JWT, CORS, Swagger, Health
    ├── Middleware/TenantClaimsMiddleware.cs          → Extrae tenant_id del JWT
    ├── Services/Schema Migration/Tables/*.sql        → Migraciones automáticas al startup
    └── appsettings.*.json
```

---

## Stack tecnológico

| Categoría | Tecnología |
|-----------|-----------|
| Runtime | .NET 10 / C# 13 |
| Framework | ASP.NET Core 10 |
| Base de datos | PostgreSQL 17 |
| ORM | Dapper (raw SQL parametrizado) |
| Driver | Npgsql 10 |
| Mediator | Custom — `Common.Messaging` (NO MediatR NuGet) |
| Auth | JWT Bearer HS256 |
| Passwords | BCrypt.Net-Next (workFactor: 12) |
| Logging | Serilog → Seq |
| Tracing | OpenTelemetry OTLP → Jaeger |
| Métricas | Prometheus en `/metrics` |
| Health | `/api/health` |
| Testing | xUnit + NSubstitute + NetArchTest.Rules |
| Deploy | Docker multi-stage (distroless) |

---

## REGLA DE ORO — Acceso a datos: `DapperDbConnection<T>`

**Todo SQL del proyecto pasa obligatoriamente por `DapperDbConnection<T>`** de `Shared/Database`.

### Cadena completa

```
ConnectionStrings:{T.Name}  (appsettings.json)
    ↓
DbConnectionFactory<T>       (lee la cadena por typeof(T).Name — singleton)
    ↓
DapperDbConnection<T>        (ejecuta Dapper + logs de performance — scoped)
    ↓
{Entidad}Sql classes          (inyectan DapperDbConnection<{Marcador}>)
```

### Marcadores de base de datos

| Marcador | Clave appsettings | Base | Acceso |
|----------|-------------------|------|--------|
| `MainDbConnection` | `ConnectionStrings:MainDbConnection` | PostgreSQL principal | Lectura/Escritura |
| `ReadonlyDbConnection` | `ConnectionStrings:ReadonlyDbConnection` | Réplica de lectura | Solo lectura |

Para agregar una nueva BD: crear un archivo marcador `{Nombre}DbConnection.cs` con clase vacía `public sealed class {Nombre}DbConnection;` y agregar la cadena de conexión en `appsettings.json`. No es necesario registrar nada en DI — los open generics lo resuelven automáticamente.

### Clases `...Sql`

Cada tabla tiene una clase `{Entidad}Sql` bajo `{Modulo}.Infrastructure/Persistence/SQLDB/`:

- Recibe `DapperDbConnection<MainDbConnection>` por constructor.
- Agrupa **todos** los queries de esa tabla — ninguno fuera de ella.
- SQL como raw strings `"""..."""`. Nunca concatenación ni interpolación.
- Parámetros siempre como objeto anónimo `new { param }`.
- Retorna entidades de dominio directamente.

```csharp
public sealed class UserProfilesSql
{
    private readonly DapperDbConnection<MainDbConnection> _db;
    public UserProfilesSql(DapperDbConnection<MainDbConnection> db) => _db = db;

    public Task<UserProfile?> GetByPublicIdAsync(Guid publicId, long tenantId, CancellationToken ct = default) =>
        _db.QuerySingleAsync<UserProfile>(
            """
            SELECT Id, PublicId, TenantId, BranchId, FullName, IsActive, CreatedAtUtc, UpdatedAtUtc
            FROM dbo.UserProfiles
            WHERE PublicId = @publicId AND TenantId = @tenantId AND IsActive = TRUE;
            """,
            new { publicId, tenantId },
            cancellationToken: ct);
}
```

**Métodos disponibles en `DapperDbConnection<T>`:**

| Método | Retorno | Uso |
|--------|---------|-----|
| `QueryAsync<T>` | `Task<IEnumerable<T>>` | Múltiples filas |
| `QuerySingleAsync<T>` | `Task<T?>` | 0 o 1 fila |
| `QueryFirstAsync<T>` | `Task<T?>` | Primera fila o null |
| `ExecuteAsync` | `Task<int>` | INSERT / UPDATE / DELETE |
| `ExecuteScalarAsync<T>` | `Task<T>` | COUNT, EXISTS, escalar |

---

## Flujo de una request (OBLIGATORIO)

```
HTTP Request
    ↓
{Modulo}Controller  →  _ = await Mediator.Send(new {Accion}Request(...), ct)
                                ↓
                    {Accion}Handler.Handle(request, ct)
                        return new {Accion}Success(...) | new {Accion}Failure(...)
                                ↓
                    InteractorPipeline  (registrado automáticamente por AddMediator)
                        await Mediator.Publish(response)
                                ↓
                    {Accion}Presenter.Handle(response, ct)
                        _viewModel.Set(success) | _viewModel.OK(data) | _viewModel.Fail(msg)
                                ↓
Controller  →  _viewModel.IsSuccess ? Ok(_viewModel) : StatusCode(...)
                                ↓
HTTP Response  { data, isSuccess, message, utcTimeStamp }
```

- El controller descarta el retorno de `Send` (`_ = await ...`) — los datos llegan al presenter vía Publish.
- **TODA** respuesta HTTP pasa por `ResultViewModel<TController>` — nunca retornar datos directos.
- El mediador es `Common.Messaging.IMediator` — **nunca MediatR NuGet**.

---

## Patrón de caso de uso

### Request

```csharp
public sealed record GetUserProfileRequest(Guid PublicId, long TenantId)
    : IRequest<GetUserProfileResponse>;
```

### Responses

```csharp
public abstract record GetUserProfileResponse : IResponse;

public sealed record GetUserProfileSuccess(UserProfileDto Data)
    : GetUserProfileResponse, ISuccess<UserProfileDto>;

public sealed record GetUserProfileNotFoundFailure(string Message)
    : GetUserProfileResponse, INotFoundFailure;
```

**Interfaces de resultado (`Common.Results`):**

| Interface | HTTP |
|-----------|------|
| `ISuccess` | 200 |
| `ISuccess<T>` | 200 con propiedad `T Data` |
| `IFailure` | 500 |
| `INotFoundFailure` | 404 |
| `IConflictFailure` | 409 |
| `IValidationFailure` | 400 |

### Handler

```csharp
public sealed class GetUserProfileHandler : IRequestHandler<GetUserProfileRequest, GetUserProfileResponse>
{
    private readonly IUserProfileRepository _profiles;
    public GetUserProfileHandler(IUserProfileRepository profiles) => _profiles = profiles;

    public async Task<GetUserProfileResponse> Handle(
        GetUserProfileRequest request, CancellationToken cancellationToken)
    {
        var profile = await _profiles.GetByPublicIdAsync(request.PublicId, request.TenantId, cancellationToken);
        if (profile is null)
            return new GetUserProfileNotFoundFailure("Perfil no encontrado.");
        return new GetUserProfileSuccess(new UserProfileDto(...));
    }
}
```

> `AddMediator(typeof({Modulo}.Application.ServiceCollectionEx).Assembly)` en `Host.Api/Program.cs` descubre y registra este handler automáticamente — **no hay que tocarlo**.

### Presenter

```csharp
public sealed class GetUserProfilePresenter : INotificationHandler<GetUserProfileResponse>
{
    private readonly ResultViewModel<UsersController> _viewModel;
    public GetUserProfilePresenter(ResultViewModel<UsersController> viewModel)
        => _viewModel = viewModel;

    public Task Handle(GetUserProfileResponse notification, CancellationToken cancellationToken)
    {
        if (notification is IFailure failure)
            _viewModel.Fail(failure.Message);
        else if (notification is ISuccess<UserProfileDto> success)
            _viewModel.Set(success);
        return Task.CompletedTask;
    }
}
```

**Presenters se registran MANUALMENTE** en `{Modulo}.Presentation/ServiceCollectionEx.cs` — nunca auto-descubiertos.

**Métodos de `ResultViewModel<T>`:**

| Método | Cuándo |
|--------|--------|
| `_viewModel.Set(ISuccess<TDto> s)` | Éxito con `ISuccess<TDto>` — Data = s.Data |
| `_viewModel.OK(object data)` | Éxito con datos custom (colecciones, paginados) |
| `_viewModel.Fail(string msg)` | Cualquier fallo — IsSuccess = false |

### Controller

```csharp
using Shared.Web;

[Route("api/users")]
[Authorize]
public sealed class UsersController : BaseApiController
{
    private readonly ILogger<UsersController>         _logger;
    private readonly ResultViewModel<UsersController> _viewModel;

    public UsersController(
        IMediator mediator,
        ILogger<UsersController> logger,
        ResultViewModel<UsersController> viewModel) : base(mediator)
    {
        _logger    = logger;
        _viewModel = viewModel;
    }

    private long CurrentTenantId =>
        long.TryParse(User.FindFirstValue("tenant_id"), out var id) ? id : 0;

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct = default)
    {
        try
        {
            var result = await Mediator.Send(new GetUserProfileRequest(id, CurrentTenantId), ct);
            if (_viewModel.IsSuccess) return Ok(_viewModel);
            return result is GetUserProfileNotFoundFailure ? NotFound(_viewModel) : StatusCode(500, _viewModel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en GetById");
            var inner = ex; while (inner.InnerException != null) inner = inner.InnerException!;
            return StatusCode(500, _viewModel.Fail(inner.Message));
        }
    }
}
```

> `BaseApiController` vive en `Shared/Web/` — expone `protected readonly IMediator Mediator`. Nunca crear uno por módulo.

---

## Contracts — comunicación entre módulos

```
{Modulo}.Contracts/
└── Events/
    ├── I{Modulo}Event.cs        → interface base con OccurredOnUtc (si aplica)
    └── {NombreEvento}Event.cs   → sealed record
└── Dtos/
    └── {Entidad}Dto.cs          → DTOs públicos compartidos con otros módulos
└── Interfaces/
    └── I{Modulo}Api.cs          → API pública del módulo (para consumo sin CQRS)
```

Si el módulo B necesita datos del módulo A: referencia `A.Contracts` — **nunca** `A.Application`, `A.Domain` ni `A.Infrastructure`.

Ejemplo real: `Authentication` referencia `Tenancy.Contracts.Interfaces.ITenancyApi` para validar que el tenant existe al registrar un usuario.

---

## Registro DI — 3 ServiceCollectionEx por módulo + Host

```csharp
// {Modulo}.Infrastructure/ServiceCollectionEx.cs
public static IServiceCollection Add{Modulo}InfrastructureServices(this IServiceCollection services)
{
    services.AddScoped<UserProfilesSql>();
    services.AddScoped<IUserProfileRepository, UserProfileRepository>();
    return services;
}

// {Modulo}.Presentation/ServiceCollectionEx.cs
public static IServiceCollection Add{Modulo}WebApiServices(this IServiceCollection services)
{
    services.AddScoped(typeof(ResultViewModel<>));
    services.AddScoped<INotificationHandler<GetUserProfileResponse>, GetUserProfilePresenter>();
    // ... resto de presenters MANUALMENTE
    services.AddControllers().AddApplicationPart(Assembly.GetExecutingAssembly());
    return services;
}

// Host.Api/Program.cs
builder.Services.AddMainDatabase();
builder.Services.AddMediator(
    typeof(Tenancy.Application.ServiceCollectionEx).Assembly,
    typeof(Users.Application.ServiceCollectionEx).Assembly,
    typeof(Authentication.Application.ServiceCollectionEx).Assembly
);
builder.Services.AddTenancyApplicationServices();
builder.Services.AddTenancyInfrastructureServices();
builder.Services.AddTenancyWebApiServices();
builder.Services.AddUsersApplicationServices();
builder.Services.AddUsersInfrastructureServices();
builder.Services.AddUsersWebApiServices();
// ...
```

**Lifetimes:**

| Tipo | Lifetime |
|------|----------|
| `DbConnectionFactory<>` | Singleton (open generic) |
| `DapperDbConnection<>` | Scoped (open generic) |
| `...Sql`, repositorios, presenters, `ResultViewModel<>` | Scoped |
| `IRequestHandler<,>` | Scoped (auto por `AddMediator`) |

---

## Multi-tenancy

Tenant = empresa / Branch = sucursal. Llegan al backend vía claims JWT:
- `tenant_id` → BIGINT como string en el claim
- `branch_id` → BIGINT como string en el claim

El controller extrae los claims con `User.FindFirstValue("tenant_id")`.
`TenantClaimsMiddleware` los inyecta en `ITenantContextAccessor` para Serilog y OpenTelemetry.

---

## Migraciones de esquema (PostgreSQL)

Viven en `Host.Api/Services/Schema Migration/Tables/`. Se ejecutan automáticamente al iniciar.

**Numeración:** bloques de 10 por entidad. `NNN_<tabla>.sql` + `NNN+1_<tabla>_indexes.sql`.

| Bloque | Tabla | Módulo |
|--------|-------|--------|
| 001-002 | `dbo.Tenants` | Tenancy |
| 010-011 | `dbo.Branches` | Tenancy |
| 020-021 | `dbo.Credentials` | Authentication |
| 030-031 | `dbo.UserProfiles` | Users |
| 040-041 | `dbo.RefreshTokens` | Authentication |

**Próxima entidad libre: bloque 050.**

**Reglas:**
- Todos los archivos son idempotentes: `CREATE TABLE IF NOT EXISTS`.
- Nunca editar migraciones ya aplicadas — nueva migración con número mayor.
- Esquema `dbo` para todas las tablas.
- Fechas UTC: `TIMESTAMP(0) NOT NULL DEFAULT (timezone('utc', now()))`.

---

## Tests por módulo

Cada módulo tiene `{Modulo}.Tests/` con:

**Architecture tests** (`Architecture/`) — NetArchTest.Rules:
- Domain no depende de Application / Infrastructure / Presentation
- Application no depende de Infrastructure / Presentation
- Infrastructure no depende de Application / Presentation

**Unit tests** (`UseCases/`) — xUnit + NSubstitute:
- Un archivo por Handler
- Mockear repositorios — sin base de datos

```csharp
private readonly IUserProfileRepository _repo = Substitute.For<IUserProfileRepository>();

[Fact]
public async Task Handle_WhenProfileExists_ReturnsSuccess()
{
    _repo.GetByPublicIdAsync(Arg.Any<Guid>(), Arg.Any<long>(), Arg.Any<CancellationToken>())
        .Returns(new UserProfile { FullName = "John" });

    var result = await new GetUserProfileHandler(_repo)
        .Handle(new GetUserProfileRequest(Guid.NewGuid(), 1), default);

    Assert.IsType<GetUserProfileSuccess>(result);
}
```

---

## Convenciones de nomenclatura

| Tipo | Patrón | Ejemplo |
|------|--------|---------|
| Request | `{Accion}Request` | `GetUserProfileRequest` |
| Handler | `{Accion}Handler` | `GetUserProfileHandler` |
| Response base | `{Accion}Response` | `GetUserProfileResponse` |
| Éxito | `{Accion}Success` | `GetUserProfileSuccess` |
| Fallo | `{Accion}{Tipo}Failure` | `GetUserProfileNotFoundFailure` |
| Presenter | `{Accion}Presenter` | `GetUserProfilePresenter` |
| Request body | `{Accion}Body` | `UpdateUserProfileBody` |
| Controller | `{Modulo}Controller` | `UsersController` |
| SQL object | `{Entidad}Sql` | `UserProfilesSql` |
| Marcador BD | `{Nombre}DbConnection` | `MainDbConnection` |
| Repositorio interfaz | `I{Entidad}Repository` | `IUserProfileRepository` |
| DTO | `{Entidad}Dto` | `UserProfileDto` |
| Evento de integración | `{Accion}IntegrationEvent` | `UserShouldBeCreatedIntegrationEvent` |

---

## Autenticación

- JWT HS256: `Jwt:Key` (≥ 32 chars), `Jwt:Issuer`, `Jwt:Audience` desde config / variables de entorno.
- Login: `POST /api/auth/login` → `{ accessToken, refreshToken, expiresAtUtc }`.
- Refresh: `POST /api/auth/refresh`.
- Roles: `[Authorize(Roles = "Admin")]` para endpoints de escritura.
- Nunca poner secretos JWT en `appsettings*.json`.

---

## Observabilidad

- **Logging:** `ILogger<T>` → Serilog → Seq (`http://localhost:5341` en dev). Nunca `Console.WriteLine`.
- **Tracing:** OpenTelemetry OTLP → Jaeger (`http://localhost:16686` en dev).
- **Métricas:** Prometheus en `/metrics`.
- **Health:** `/api/health`.

---

## Reglas que no se negocian

1. Los módulos no se referencian entre sí — solo a través de `{Modulo}.Contracts`.
2. `Application` nunca referencia `Infrastructure`.
3. `Infrastructure` nunca referencia `Application`.
4. El mediador es `Common.Messaging.IMediator` — **nunca MediatR NuGet**.
5. Todo SQL vive en clases `...Sql` — cero SQL inline en repositorios, handlers o servicios.
6. Toda respuesta HTTP pasa por `ResultViewModel<TController>` — nunca retornar datos directos.
7. No secretos en `appsettings*.json` — variables de entorno.
8. **PROHIBIDO modificar el submódulo `Common`** desde este repositorio.
9. `BaseApiController` vive en `Shared/Web/` — **no** se crea uno por módulo.
10. Los presenters se registran **manualmente** en `{Modulo}.Presentation/ServiceCollectionEx.cs`.
11. Al terminar cualquier cambio: `dotnet build` desde `Host.Api` con **0 errores**.
12. Todo módulo nuevo debe incluir `{Modulo}.Tests` con tests de arquitectura y unitarios.

---

## Flujo para cambios funcionales

**Antes:** trazar el flujo completo. Identificar módulo, tabla y base de datos.

**Durante (por capas):**

1. Nueva tabla → `Host.Api/Services/Schema Migration/Tables/NNN_tabla.sql` + `NNN+1_tabla_indexes.sql`
2. DTO público → `{Modulo}.Contracts/Dtos/`
3. Entidad → `{Modulo}.Domain/Entities/` + interfaz en `{Modulo}.Domain/Repositories/`
4. `...Sql` → `{Modulo}.Infrastructure/Persistence/SQLDB/` inyectando `DapperDbConnection<MainDbConnection>`
5. Repositorio → `{Modulo}.Infrastructure/Repositories/` + DI en `{Modulo}.Infrastructure/ServiceCollectionEx.cs`
6. Caso de uso → `{Modulo}.Application/UseCases/{Accion}/` (Request + Handler + Responses/)
7. Presenter → `{Modulo}.Presentation/Presenters/` + registro **manual** en `{Modulo}.Presentation/ServiceCollectionEx.cs`
8. Controller (o endpoint) → `{Modulo}.Presentation/Controllers/{Modulo}Controller.cs` extendiendo `BaseApiController`
9. Test unitario → `{Modulo}.Tests/UseCases/{Accion}HandlerTests.cs`

**Al terminar:** `dotnet build Host.Api/Host.Api.csproj` — 0 errores.
