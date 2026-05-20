# Back.md — Arquitectura Backend

Guía de referencia para agregar funcionalidad al backend. Todo código generado debe respetar estas convenciones sin excepción.

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
| Métricas | Prometheus (`/metrics`) |
| Health | `/api/health` |
| Testing | xUnit + NSubstitute + NetArchTest.Rules |
| Deploy | Docker multi-stage |

---

## Principios obligatorios — SOLID y Clean Code

Estas reglas son tan vinculantes como las reglas arquitectónicas. No son aspiracionales.

### SOLID

**S — Single Responsibility Principle (SRP)**
Cada clase tiene exactamente una razón para cambiar.
- Un handler maneja un solo caso de uso.
- Un repositorio gestiona una sola entidad.
- Un presenter traduce una sola respuesta de dominio a HTTP.
- Si una clase tiene más de una responsabilidad, sepárala.

**O — Open/Closed Principle (OCP)**
Abierto para extensión, cerrado para modificación.
- Extiende comportamiento agregando nuevos handlers, presenters o repositorios, no modificando los existentes.
- Usa interfaces (`IRepository`, `ITokenService`) para puntos de extensión sin romper implementaciones existentes.

**L — Liskov Substitution Principle (LSP)**
Un subtipo debe ser reemplazable por su tipo base sin alterar el comportamiento.
- Toda implementación de `IRequestHandler<TReq, TRes>` debe cumplir el contrato completo.
- Toda implementación de `IUserCredentialRepository` debe satisfacer todos los contratos definidos por la interfaz.

**I — Interface Segregation Principle (ISP)**
Las interfaces deben ser específicas, no genéricas.
- No crear un `IUserService` con 15 métodos. Preferir `IPasswordHasher`, `IJwtTokenService`, `IUserProfileRepository` con responsabilidades acotadas.
- Si un módulo solo necesita leer tenants, exponerlo como `ITenancyApi` con los métodos mínimos necesarios.

**D — Dependency Inversion Principle (DIP)**
Depender de abstracciones, no de implementaciones concretas.
- `Application` y `WebApi` solo ven interfaces: `IRepository`, `ITokenService`, `ITenancyApi`.
- `Infrastructure` provee las implementaciones; `Application` nunca la referencia directamente.
- Los repositorios son detalles de infraestructura: solo `Infrastructure` los conoce.

### Clean Code

**Nombres que explican intención**
- Usa `GetByPublicIdAsync` en lugar de `GetById`, `FindUser`, `Fetch`.
- Usa `InsertAsync`, `UpdateAsync`, `DisableAsync` — verbos explícitos.
- Nombres de variables: `credential`, `userProfile`, `tokenDto` — no `u`, `obj`, `data2`.

**Funciones pequeñas y enfocadas**
- Un handler hace una sola cosa: valida, ejecuta lógica de dominio, retorna resultado.
- Si un método necesita comentarios para explicar secciones internas, cada sección debería ser un método privado.

**Sin código muerto**
- Sin métodos no usados, sin imports innecesarios, sin variables declaradas y nunca leídas.
- Sin código comentado — el historial de git existe para eso.

**Sin números mágicos ni strings literales repetidos**
- Roles: usar constantes (`"Admin"`, `"User"`) definidas en un lugar central.

**Manejo de errores explícito, sin excepciones para control de flujo**
- Retornar `INotFoundFailure`, `IConflictFailure`, `IValidationFailure` desde el handler.
- Nunca lanzar excepciones para representar "usuario no encontrado" o "email ya registrado".
- Las excepciones son para condiciones inesperadas del sistema (fallo de red, DB inaccesible).

**Sin duplicación (DRY)**
- Si una consulta aparece en dos repositorios, abstraer en un método compartido.
- Si una transformación de DTO se repite, es un método de extensión o factory.

---

## Arquitectura — Monolito Modular Explícito

El proyecto usa un **Monolito Modular Explícito**: cada módulo y cada capa tienen su propio `.csproj`. Las fronteras son reales en tiempo de compilación.

### Módulos

```
Modules/
  Tenancy/        ← Empresa (tenant management)
  Users/          ← Perfil de usuario (CRUD, datos de negocio)
Shared/
  Authentication/ ← Identidad, credenciales, JWT (cross-cutting)
  Database/       ← AppDbContext + EntityTypeConfigurations
Common/           ← Submódulo — abstracciones sin lógica de negocio
```

`Authentication` vive en `Shared/` porque es infraestructura transversal que todos los módulos necesitan. Moverlo a `Modules/` crearía un "módulo dios" (todos dependen de él).

### Capas por módulo

Cada módulo tiene 6 proyectos:

| Proyecto | Responsabilidad |
|----------|----------------|
| `{Modulo}.Contracts` | Interfaces públicas (`ITenancyApi`) y eventos de integración — lo único que otros módulos pueden referenciar |
| `{Modulo}.Domain` | Entidades, value objects, interfaces de repositorios |
| `{Modulo}.Application` | Casos de uso: Request, Handler, Responses. Sin referencias a Infrastructure |
| `{Modulo}.Infrastructure` | Implementaciones: repositorios concretos (inyectan `AppDbContext`), servicios externos |
| `{Modulo}.Presentation` | Controllers, Presenters, RequestBodies |
| `{Modulo}.Tests` | Tests unitarios de handlers (xUnit + NSubstitute) + tests de arquitectura (NetArchTest.Rules) |

### Dirección de dependencias (por módulo)

```
Contracts      → (sin dependencias de proyecto)
Domain         → Common
Application    → Common + Domain + Contracts
Infrastructure → Common + Domain + Shared.Database         (NO referencia Application)
Presentation   → Common + Application + Shared.Web         (NO referencia Infrastructure)
Tests          → todos los anteriores + xUnit + NSubstitute + NetArchTest.Rules
```

**Reglas absolutas:**
- Un módulo solo puede referenciar `.Contracts` de otro módulo — nunca `.Domain`, `.Application`, `.Infrastructure` ni `.Presentation`.
- `Application` nunca referencia `Infrastructure`.
- `Infrastructure` nunca referencia `Presentation`.
- `Host.Api` es la única capa que conoce todos los módulos.

### Árbol de directorios

```
back-template/
├── Common/                                 ← submódulo git (NO editar)
├── Shared/
│   ├── Database/
│   │   ├── AppDbContext.cs                 ← DbContext central con todos los DbSets
│   │   ├── EntityTypeConfigurations/       ← IEntityTypeConfiguration<T> por entidad
│   │   └── ServiceCollectionEx.cs          ← AddMainDatabase(config)
│   ├── Web/
│   │   └── BaseApiController.cs            ← base con IMediator protegido
│   └── Authentication/
│       ├── Authentication.Contracts/       ← UserShouldBeCreatedIntegrationEvent
│       ├── Authentication.Domain/          ← UserCredential, RefreshToken, IUserCredentialRepository
│       ├── Authentication.Application/     ← Register/Login/RefreshToken handlers
│       ├── Authentication.Infrastructure/  ← Repositorios (AppDbContext), JwtTokenService
│       ├── Authentication.Presentation/    ← AuthController, presenters
│       └── Authentication.Tests/          ← tests unitarios + arquitectura
├── Modules/
│   ├── Tenancy/
│   │   ├── Tenancy.Contracts/              ← ITenancyApi, TenantDto
│   │   ├── Tenancy.Domain/
│   │   ├── Tenancy.Application/            ← TenancyApi (implementa ITenancyApi)
│   │   ├── Tenancy.Infrastructure/
│   │   ├── Tenancy.Presentation/
│   │   └── Tenancy.Tests/
│   └── Users/
│       ├── Users.Contracts/                ← UserShouldBeCreatedIntegrationEvent
│       ├── Users.Domain/
│       ├── Users.Application/              ← CRUD handlers + UserShouldBeCreatedHandler
│       ├── Users.Infrastructure/
│       ├── Users.Presentation/
│       └── Users.Tests/
├── Host.Api/
│   ├── Program.cs                          ← composición final
│   ├── Extensions/                         ← JwtAuth, Cors, Swagger, Health
│   ├── Middleware/TenantClaimsMiddleware.cs
│   └── appsettings.json
└── Tests/                                  ← tests de integración cross-módulo (opcional)
```

---

## Comunicación entre módulos

Los módulos se comunican exclusivamente por dos mecanismos:

### 1. Contratos síncronos (`ITenancyApi`)

`Authentication.Application` necesita validar que un tenant existe antes de registrar una credencial. No puede referenciar `Tenancy.Application` directamente.

Solución: `Tenancy.Contracts` expone `ITenancyApi`. `Authentication.Application` depende de la interfaz. `Tenancy.Application` provee la implementación.

```csharp
// En Tenancy.Contracts
public interface ITenancyApi
{
    Task<TenantDto?> GetTenantByIdAsync(long id, CancellationToken ct = default);
}
```

### 2. Eventos de integración en proceso (`INotification`)

Tras crear una credencial, `Authentication.Application` necesita que `Users.Application` cree el perfil de usuario. Lo hace publicando un evento de integración — sin referencia directa entre módulos.

```csharp
// En Authentication.Contracts
public sealed record UserShouldBeCreatedIntegrationEvent(
    Guid PublicId, long TenantId,
    string FullName, string Email, string Role) : INotification;

// En Users.Contracts
public sealed record UserRegisteredIntegrationEvent(Guid PublicId) : INotification;
```

El `IMediator.Publish()` de `Common.Messaging` ejecuta todos los `INotificationHandler<T>` registrados de forma síncrona en proceso. No hay bus de mensajes externo.

**Flujo de registro completo:**

```
AuthController.Register(body)
    ↓
RegisterHandler
    1. Valida tenant via ITenancyApi.GetTenantByIdAsync()
    2. Verifica email no tomado (IUserCredentialRepository)
    3. Hashea password, inserta Credential → obtiene Id generado
    4. Publica UserShouldBeCreatedIntegrationEvent
          ↓
          UserShouldBeCreatedHandler (Users.Application)
              1. Re-valida tenant
              2. Inserta UserProfile con el mismo PublicId
              3. Publica UserRegisteredIntegrationEvent
    5. Genera JWT + RefreshToken
    6. Retorna RegisterSuccess(TokenDto)
```

---

## Regla de oro — Acceso a datos

**Todo acceso a datos pasa obligatoriamente por `AppDbContext`** inyectado en el repositorio.

```
ConnectionStrings:MainDbConnection  (appsettings.json)
    ↓
AppDbContext                        (EF Core — Scoped por AddDbContext)
    ↓
{Entidad}Repository                 (inyecta AppDbContext directamente)
```

Los repositorios son la única capa de acceso a datos. Ningún handler, servicio de aplicación ni controller toca `AppDbContext` directamente.

**Patrones de acceso:**

```csharp
// Lectura — siempre AsNoTracking()
await _db.UserProfiles
    .AsNoTracking()
    .FirstOrDefaultAsync(e => e.PublicId == publicId && e.IsActive, ct);

// Insertar — object initializer con propiedades init
var entity = new UserProfile { PublicId = publicId, TenantId = tenantId, FullName = fullName, IsActive = true };
_db.UserProfiles.Add(entity);
await _db.SaveChangesAsync(ct);

// Actualizar — ExecuteUpdateAsync (funciona con propiedades init)
await _db.UserProfiles
    .Where(e => e.PublicId == publicId)
    .ExecuteUpdateAsync(s => s.SetProperty(e => e.FullName, fullName), ct);

// Sin filtro tenant (auth) — IgnoreQueryFilters()
await _db.Credentials.IgnoreQueryFilters().FirstOrDefaultAsync(e => e.Email == email, ct);
```

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
                    InteractorPipeline (registrado automáticamente por AddMediator)
                        await Mediator.Publish(response)
                                ↓
                    {Accion}Presenter.Handle(response, ct)
                        _viewModel.Set(success) | _viewModel.OK(data) | _viewModel.Fail(msg)
                                ↓
Controller  →  _viewModel.IsSuccess ? Ok(_viewModel) : StatusCode(500, _viewModel)
                                ↓
HTTP Response  (siempre ResultViewModel<TController> JSON)
```

- El controller descarta el retorno de `Send` (`_ = await ...`) — la respuesta llega al presenter vía Publish.
- **TODA** respuesta HTTP pasa por `ResultViewModel<TController>` — nunca retornar datos directos.

---

## Patrón de caso de uso

### Request

```csharp
public sealed record GetUserProfileRequest(Guid PublicId, long TenantId)
    : IRequest<GetUserProfileResponse>;
```

### Responses

```csharp
// Base
public abstract record GetUserProfileResponse : IResponse;

// Éxito con DTO único
public sealed record GetUserProfileSuccess(UserProfileDto Data)
    : GetUserProfileResponse, ISuccess<UserProfileDto>;

// Éxito con paginación (nunca ISuccess<TSelf> — referencia circular en JSON)
public sealed record GetUserProfilesSuccess(
    IReadOnlyCollection<UserProfileDto> Users, int Total, int Page, int PageSize)
    : GetUserProfilesResponse, ISuccess;

// Fallo
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
public sealed class GetUserProfileHandler
    : IRequestHandler<GetUserProfileRequest, GetUserProfileResponse>
{
    private readonly IUserProfileRepository _profiles;

    public GetUserProfileHandler(IUserProfileRepository profiles) => _profiles = profiles;

    public async Task<GetUserProfileResponse> Handle(
        GetUserProfileRequest request, CancellationToken cancellationToken)
    {
        var profile = await _profiles.GetByPublicIdAsync(request.PublicId, cancellationToken);
        if (profile is null)
            return new GetUserProfileNotFoundFailure("Perfil no encontrado.");
        return new GetUserProfileSuccess(new UserProfileDto(profile.PublicId, profile.FullName, profile.IsActive));
    }
}
```

### Presenter

```csharp
// Variante A — ISuccess<TDto> → _viewModel.Set(success)
public sealed class GetUserProfilePresenter : IPresenter<GetUserProfileResponse>
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

**Métodos de `ResultViewModel<T>`:**

| Método | Cuándo |
|--------|--------|
| `_viewModel.Set(ISuccess<TDto> s)` | Éxito con `ISuccess<TDto>` — Data = s.Data |
| `_viewModel.OK(object data)` | Éxito con datos custom — Data = el objeto |
| `_viewModel.Fail(string msg)` | Cualquier fallo — IsSuccess = false |

### Controller

Los controllers extienden `BaseApiController` de `Shared.Web`, que expone `protected readonly IMediator Mediator` y lleva `[ApiController]`. No repetir esos atributos en los controllers.

```csharp
using Shared.Web;

[Route("api/users")]
[Authorize]
public sealed class UsersController : BaseApiController
{
    private readonly ILogger<UsersController>         _logger;
    private readonly ResultViewModel<UsersController> _viewModel;

    public UsersController(IMediator mediator, ILogger<UsersController> logger,
        ResultViewModel<UsersController> viewModel) : base(mediator)
    { _logger = logger; _viewModel = viewModel; }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct = default)
    {
        try
        {
            _ = await Mediator.Send(new GetUserProfileRequest(id, CurrentTenantId), ct);
            if (_viewModel.IsSuccess) return Ok(_viewModel);
            return StatusCode(404, _viewModel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en GetById User");
            var inner = ex;
            while (inner.InnerException != null) inner = inner.InnerException!;
            return StatusCode(500, _viewModel.Fail(inner.Message));
        }
    }
}
```

El controller descarta el valor de retorno de `Send` (`_ = await ...`) — la respuesta llega al presenter vía `Mediator.Publish` dentro del pipeline.

---

## Registro DI por módulo

### Application `ServiceCollectionEx`

```csharp
public static IServiceCollection AddUsersApplicationServices(this IServiceCollection services)
{
    // Solo registrar servicios propios de Application si los hay
    // AddMediator se llama UNA SOLA VEZ desde Host.Api/Program.cs
    return services;
}
```

### Infrastructure `ServiceCollectionEx`

```csharp
public static IServiceCollection AddUsersInfrastructureServices(this IServiceCollection services)
{
    services.AddScoped<IUserProfileRepository, UserProfileRepository>();
    return services;
}
```

### Presentation `ServiceCollectionEx`

```csharp
public static IServiceCollection AddUsersWebApiServices(this IServiceCollection services)
{
    services.AddScoped(typeof(ResultViewModel<>));

    // Presenters — manualmente, NO vía AddMediator (evita doble invocación)
    services.AddScoped<INotificationHandler<GetUserProfileResponse>,    GetUserProfilePresenter>();
    services.AddScoped<INotificationHandler<GetUserProfilesResponse>,   GetUserProfilesPresenter>();
    services.AddScoped<INotificationHandler<UpdateUserProfileResponse>,  UpdateUserProfilePresenter>();
    services.AddScoped<INotificationHandler<DisableUserProfileResponse>, DisableUserProfilePresenter>();

    services.AddControllers().AddApplicationPart(Assembly.GetExecutingAssembly());
    return services;
}
```

### Host.Api `Program.cs` — composición final

```csharp
// AppDbContext + DatabaseInitializationService
builder.Services.AddMainDatabase(builder.Configuration);

// Una sola llamada a AddMediator con TODOS los ensamblados Application
builder.Services.AddMediator(
    typeof(Tenancy.Application.ServiceCollectionEx).Assembly,
    typeof(Users.Application.ServiceCollectionEx).Assembly,
    typeof(Authentication.Application.ServiceCollectionEx).Assembly
);

// Por cada módulo: Application + Infrastructure + Presentation
builder.Services.AddTenancyApplicationServices();
builder.Services.AddTenancyInfrastructureServices();
builder.Services.AddTenancyWebApiServices();

builder.Services.AddUsersApplicationServices();
builder.Services.AddUsersInfrastructureServices();
builder.Services.AddUsersWebApiServices();

builder.Services.AddAuthenticationApplicationServices();
builder.Services.AddAuthenticationInfrastructureServices(builder.Configuration);
builder.Services.AddAuthenticationWebApiServices();
```

**Por qué una sola llamada:** `AddMediator` registra `IPipelineBehavior<,> → InteractorPipeline<,>`. Si se llama N veces, el pipeline se encadena N veces y cada handler se ejecuta N veces.

**Por qué los Presenters se registran manualmente:** `AddMediator` escanea assemblies en busca de `INotificationHandler<T>`. Si Presentation estuviese en la lista, cada Presenter quedaría registrado dos veces y `Mediator.Publish` lo invocaría dos veces.

---

## Lifetimes de DI

| Clase | Lifetime |
|-------|----------|
| `AppDbContext` | Scoped (AddDbContext) |
| Repositorios | Scoped |
| Presenters | Scoped |
| `ResultViewModel<>` | Scoped |
| `ITenantContextAccessor` | Singleton |

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
| Evento de integración | `{Evento}IntegrationEvent` | `UserShouldBeCreatedIntegrationEvent` |

---

## Reglas que no se negocian

1. `Application` nunca referencia `Infrastructure`.
2. Un módulo solo puede referenciar `.Contracts` de otro módulo.
3. `Infrastructure` nunca referencia `Presentation`.
4. El mediador es `Common.Messaging.IMediator` — **nunca MediatR NuGet**.
5. Todo acceso a datos pasa por `AppDbContext` inyectado en el repositorio — cero SQL inline en handlers o servicios.
6. Toda respuesta HTTP pasa por `ResultViewModel<TController>` — nunca retornar datos directos.
7. No secretos en `appsettings*.json` — variables de entorno.
8. No editar el submódulo `Common` desde este repositorio.
9. `AddMediator()` se llama **una sola vez** en `Host.Api/Program.cs`.
10. Presenters se registran manualmente en `Presentation/ServiceCollectionEx.cs` — nunca vía scan de AddMediator.
11. Al terminar cualquier cambio: `dotnet build` desde `Host.Api` con **0 errores**.

---

## Checklist para un módulo nuevo

- [ ] Crear `{Modulo}.Contracts` — interfaces públicas y eventos de integración
- [ ] Crear `{Modulo}.Domain` — entidades + interfaces de repositorios
- [ ] Crear `{Modulo}.Application` — Request + Handler + Responses por acción; `ServiceCollectionEx.cs`
- [ ] Crear `{Modulo}.Infrastructure` — repositorios concretos (AppDbContext); `ServiceCollectionEx.cs`
- [ ] Crear configuración EF Core en `Shared/Database/EntityTypeConfigurations/`
- [ ] Agregar `DbSet<T>` en `AppDbContext` + query filter si aplica
- [ ] Crear `{Modulo}.Presentation` — Controllers + Presenters + RequestBodies; `ServiceCollectionEx.cs`
- [ ] Crear `{Modulo}.Tests` — tests de arquitectura (NetArchTest) + tests unitarios por handler (xUnit + NSubstitute)
- [ ] Agregar los 6 `.csproj` al `back-template.slnx` bajo `<Folder Name="/{Modulo}/">`
- [ ] Referenciar Application, Infrastructure y Presentation del módulo en `Host.Api.csproj`
- [ ] Llamar `Add{Modulo}ApplicationServices()`, `Add{Modulo}InfrastructureServices()`, `Add{Modulo}WebApiServices()` en `Program.cs`
- [ ] Pasar el ensamblado `.Application` al `AddMediator(...)` en `Program.cs`
- [ ] `dotnet build Host.Api/Host.Api.csproj` — 0 errores
- [ ] `dotnet test` en `{Modulo}.Tests` — 0 errores

Ver guía detallada en [docs/Modules.md](Modules.md).
