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
│   │   ├── AppDbContext.cs                          ← DbContext central con todos los DbSets
│   │   ├── EntityTypeConfigurations/               ← IEntityTypeConfiguration<T> por entidad
│   │   │   ├── TenantConfiguration.cs
│   │   │   ├── UserCredentialConfiguration.cs
│   │   │   ├── RefreshTokenConfiguration.cs
│   │   │   └── UserProfileConfiguration.cs
│   │   └── ServiceCollectionEx.cs                   → AddMainDatabase(config)
│   └── Web/                                         → Infraestructura web compartida (con ASP.NET)
│       └── BaseApiController.cs                     → namespace Shared.Web
├── Modules/
│   ├── Tenancy/                                     → Módulo Tenancy (solo Tenant)
│   │   ├── Tenancy.Contracts/                       → DTOs + Integration Events (POCO, sin deps)
│   │   ├── Tenancy.Domain/                          → Entidades + interfaces de repositorio
│   │   ├── Tenancy.Application/                     → Use cases (TenancyApi)
│   │   ├── Tenancy.Infrastructure/                  → Repositorios con AppDbContext
│   │   ├── Tenancy.Presentation/                    → Controllers + ServiceCollectionEx
│   │   └── Tenancy.Tests/                           → Tests arquitectura + unitarios
│   └── Users/                                       → Módulo Users (perfiles de usuario)
│       ├── Users.Contracts/                         → UserProfileDto + Integration Events
│       ├── Users.Domain/                            → UserProfile entity + IUserProfileRepository
│       ├── Users.Application/                       → GetUserProfile, GetUserProfiles, Update, Disable
│       ├── Users.Infrastructure/                    → UserProfileRepository con AppDbContext
│       ├── Users.Presentation/                      → UsersController + 4 Presenters
│       └── Users.Tests/                             → Tests arquitectura (7) + unitarios (2)
├── Shared/Authentication/                           → Módulo compartido Auth
│   ├── Authentication.Contracts/                    → TokenDto + UserShouldBeCreatedIntegrationEvent
│   ├── Authentication.Domain/                       → UserCredential + RefreshToken entities
│   ├── Authentication.Application/                  → Login + Register + RefreshToken handlers
│   ├── Authentication.Infrastructure/               → Repositorios + JWT + BCrypt con AppDbContext
│   ├── Authentication.Presentation/                 → AuthController + 3 Presenters
│   └── Authentication.Tests/                        → Tests arquitectura (7) + unitarios (3)
└── Host.Api/                                        → Punto de entrada — Program.cs
    ├── Extensions/                                  → JWT, CORS, Swagger, Health
    ├── Middleware/TenantClaimsMiddleware.cs          → Extrae tenant_id del JWT
    └── appsettings.*.json
```

---

## Stack tecnológico

| Categoría | Tecnología |
|-----------|-----------|
| Runtime | .NET 10 / C# 13 |
| Framework | ASP.NET Core 10 |
| Base de datos | PostgreSQL 17 |
| ORM | EF Core 10 (Npgsql 10.0.1) |
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

## REGLA DE ORO — Acceso a datos: `AppDbContext`

**Todo acceso a datos pasa por `AppDbContext`** de `Shared/Database`.

### Cadena completa

```
ConnectionStrings:MainDbConnection  (appsettings.json)
    ↓
AppDbContext                        (EF Core DbContext — Scoped)
    ↓
{Entidad}Repository                 (inyecta AppDbContext directamente)
    ↓
Handler                             (lógica de negocio)
```

### AppDbContext — DbSets y filtros globales

`AppDbContext` vive en `Shared/Database/AppDbContext.cs` y tiene:

- Un `DbSet<T>` por cada entidad persistida.
- Global query filters de TenantId para `UserCredential` y `UserProfile` — se aplican automáticamente.
- `IgnoreQueryFilters()` en los repositorios de auth (login/refresh no tienen tenant aún).

```csharp
public sealed class AppDbContext : DbContext
{
    private readonly ITenantContextAccessor _tenantAccessor;

    public DbSet<Tenant>         Tenants       { get; set; } = null!;
    public DbSet<UserCredential> Credentials   { get; set; } = null!;
    public DbSet<RefreshToken>   RefreshTokens { get; set; } = null!;
    public DbSet<UserProfile>    UserProfiles  { get; set; } = null!;

    public AppDbContext(DbContextOptions<AppDbContext> options, ITenantContextAccessor tenantAccessor)
        : base(options) { _tenantAccessor = tenantAccessor; }

    private long CurrentTenantId =>
        long.TryParse(_tenantAccessor.Current?.TenantId, out var id) ? id : 0L;

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.HasDefaultSchema("dbo");
        mb.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        mb.Entity<UserCredential>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
        mb.Entity<UserProfile>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
    }
}
```

### Patrones de acceso en repositorios

| Operación | Patrón EF Core |
|-----------|---------------|
| Lectura única | `await _db.UserProfiles.AsNoTracking().FirstOrDefaultAsync(e => e.PublicId == id, ct)` |
| Lectura lista | `await _db.UserProfiles.AsNoTracking().ToListAsync(ct)` |
| Insertar | `_db.UserProfiles.Add(entity); await _db.SaveChangesAsync(ct)` |
| Actualizar (propiedades `init`) | `await _db.UserProfiles.Where(...).ExecuteUpdateAsync(s => s.SetProperty(...), ct)` |
| Soft delete | `await _db.UserProfiles.Where(...).ExecuteUpdateAsync(s => s.SetProperty(e => e.IsActive, false), ct)` |
| Sin filtro tenant (auth) | `_db.Credentials.IgnoreQueryFilters().FirstOrDefaultAsync(...)` |

**Regla absoluta:** los repositorios usan `AppDbContext` directamente — no hay clases intermedias `...Sql`. El repositorio ES la única capa de acceso a datos.

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
        var profile = await _profiles.GetByPublicIdAsync(request.PublicId, cancellationToken);
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
builder.Services.AddMainDatabase(builder.Configuration);
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
| `AppDbContext` | Scoped (registrado por `AddDbContext`) |
| Repositorios, presenters, `ResultViewModel<>` | Scoped |
| `IRequestHandler<,>` | Scoped (auto por `AddMediator`) |
| `ITenantContextAccessor` | Singleton |

---

## Multi-tenancy

Tenant = empresa. Llega al backend vía claims JWT:
- `tenant_id` → BIGINT como string en el claim

El controller extrae el claim con `User.FindFirstValue("tenant_id")`.
`TenantClaimsMiddleware` lo inyecta en `ITenantContextAccessor` para el global query filter de EF Core y para Serilog/OpenTelemetry.

El global query filter en `AppDbContext` aplica `WHERE TenantId = @currentTenantId` automáticamente en todas las consultas sobre `UserCredential` y `UserProfile`. Para los repositorios de auth (login/refresh), usar `IgnoreQueryFilters()` porque no hay tenant en ese momento.

---

## Esquema de BD (EF Core EnsureCreated)

Las tablas se crean al iniciar la aplicación mediante `DatabaseInitializationService` → `db.Database.EnsureCreatedAsync()`. Las configuraciones de columnas/índices viven en `EntityTypeConfigurations/`.

**Tablas actuales:**

| Tabla | Módulo dueño | Descripción |
|-------|-------------|-------------|
| `dbo.tenants` | Tenancy | Empresas SaaS |
| `dbo.credentials` | Authentication | Login (Email, PasswordHash, Role, TenantId) |
| `dbo.user_profiles` | Users | Datos de perfil (PublicId, FullName, TenantId) |
| `dbo.refresh_tokens` | Authentication | Tokens JWT con FK a credentials |

> Para agregar una tabla nueva: crear la entidad en Domain, su `IEntityTypeConfiguration<T>` en `Shared/Database/EntityTypeConfigurations/`, y dejar que `EnsureCreated` la materialice en el próximo arranque.

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
    _repo.GetByPublicIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
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
| Entity config | `{Entidad}Configuration` | `UserProfileConfiguration` |
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
- Claims en el JWT: `sub` (PublicId), `email`, `role`, `tenant_id`. **No hay `branch_id`.**

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
5. Todo acceso a datos pasa por `AppDbContext` inyectado en el repositorio — cero SQL inline en handlers o servicios.
6. Toda respuesta HTTP pasa por `ResultViewModel<TController>` — nunca retornar datos directos.
7. No secretos en `appsettings*.json` — variables de entorno.
8. **PROHIBIDO modificar el submódulo `Common`** desde este repositorio.
9. `BaseApiController` vive en `Shared/Web/` — **no** se crea uno por módulo.
10. Los presenters se registran **manualmente** en `{Modulo}.Presentation/ServiceCollectionEx.cs`.
11. Al terminar cualquier cambio: `dotnet build` desde `Host.Api` con **0 errores**.
12. Todo módulo nuevo debe incluir `{Modulo}.Tests` con tests de arquitectura y unitarios.

---

## Flujo para cambios funcionales

**Antes:** trazar el flujo completo. Identificar módulo y entidad.

**Durante (por capas):**

1. Entidad nueva → `{Modulo}.Domain/Entities/` + interfaz en `{Modulo}.Domain/Repositories/`
2. Configuración EF Core → `Shared/Database/EntityTypeConfigurations/{Entidad}Configuration.cs`
3. `DbSet<T>` → agregar en `AppDbContext` + query filter si aplica
4. DTO público → `{Modulo}.Contracts/Dtos/`
5. Repositorio → `{Modulo}.Infrastructure/Repositories/` usando `AppDbContext` + DI en `{Modulo}.Infrastructure/ServiceCollectionEx.cs`
6. Caso de uso → `{Modulo}.Application/UseCases/{Accion}/` (Request + Handler + Responses/)
7. Presenter → `{Modulo}.Presentation/Presenters/` + registro **manual** en `{Modulo}.Presentation/ServiceCollectionEx.cs`
8. Controller (o endpoint) → `{Modulo}.Presentation/Controllers/{Modulo}Controller.cs` extendiendo `BaseApiController`
9. Test unitario → `{Modulo}.Tests/UseCases/{Accion}HandlerTests.cs`

**Al terminar:** `dotnet build Host.Api/Host.Api.csproj` — 0 errores.
