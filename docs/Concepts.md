# Anatomía del proyecto — dónde va cada cosa

Referencia rápida para saber dónde crear cada archivo y qué es cada concepto.

---

## Mapa de directorios completo

```
Modules/{Modulo}/
│
├── {Modulo}.Contracts/              ← lo único que otros módulos pueden ver
│   └── Events/
│       ├── I{Modulo}Event.cs        → interface base del evento del módulo
│       └── {Nombre}IntegrationEvent.cs → evento que otros módulos pueden escuchar
│
├── {Modulo}.Domain/                 ← corazón del módulo — sin deps de infra
│   ├── Entities/
│   │   └── {Entidad}.cs             → clase que representa una cosa del negocio
│   ├── Enums/
│   │   └── {Nombre}.cs              → enumeración de valores fijos del dominio
│   └── Repositories/
│       └── I{Entidad}Repository.cs  → contrato de acceso a datos (solo la interfaz)
│
├── {Modulo}.Application/            ← lógica de casos de uso — sin SQL, sin HTTP
│   ├── Dto/
│   │   └── {Entidad}Dto.cs          → datos que se devuelven al cliente (salida)
│   ├── UseCases/
│   │   └── {Accion}/                → carpeta por acción (GetUser, CreateOrder, etc.)
│   │       ├── {Accion}Request.cs   → qué pide el cliente (input del caso de uso)
│   │       ├── {Accion}Handler.cs   → lógica del caso de uso
│   │       └── Responses/
│   │           ├── {Accion}Response.cs         → abstract record base
│   │           ├── {Accion}Success.cs           → resultado exitoso
│   │           └── {Accion}NotFoundFailure.cs  → (u otro tipo de fallo)
│   └── ServiceCollectionEx.cs       → registro DI de Application
│
├── {Modulo}.Infrastructure/         ← implementaciones concretas — SQL, servicios externos
│   ├── Persistence/
│   │   └── SQLDB/
│   │       └── {Entidad}Sql.cs      → todos los queries SQL de esa tabla
│   ├── Repositories/
│   │   └── {Entidad}Repository.cs   → implementación de I{Entidad}Repository
│   └── ServiceCollectionEx.cs       → registro DI de Infrastructure
│
├── {Modulo}.Presentation/           ← capa HTTP — controllers, presenters, bodies
│   ├── Controllers/
│   │   └── {Modulo}Controller.cs    → endpoints HTTP del módulo
│   ├── Presenters/
│   │   └── {Accion}Presenter.cs     → traduce la response del handler a HTTP
│   ├── RequestBodies/
│   │   └── {Accion}Body.cs          → forma del body JSON que llega en POST/PUT
│   └── ServiceCollectionEx.cs       → registro DI de Presentation
│
└── {Modulo}.Tests/                  ← tests — sin impacto en producción
    ├── Architecture/
    │   └── {Modulo}ArchitectureTests.cs → verifica que nadie viola las reglas de deps
    └── UseCases/
        └── {Accion}HandlerTests.cs  → tests unitarios del handler
```

### Compartido (no es de ningún módulo)

```
Shared/
├── Database/
│   ├── AppDbContext.cs              → DbContext central con todos los DbSets y query filters
│   └── EntityTypeConfigurations/   → IEntityTypeConfiguration<T> por entidad
└── Web/
    └── BaseApiController.cs         → clase base de todos los controllers
```

### Host (único proyecto con acceso a todo)

```
Host.Api/
├── Program.cs                       → composición final — ata todos los módulos
├── Extensions/
│   ├── JwtAuthExtensions.cs         → configuración de JWT
│   ├── CorsExtensions.cs            → configuración de CORS
│   ├── SwaggerExtensions.cs         → configuración de Swagger
│   └── HealthExtensions.cs          → configuración de health checks
├── Middleware/
│   └── TenantClaimsMiddleware.cs    → extrae tenant del token y lo inyecta en el contexto
└── Services/
    └── Schema Migration/
        └── Tables/
            └── *.sql                → migraciones SQL que se ejecutan al iniciar
```

---

## Glosario — qué es cada cosa

---

### Entity (Entidad)

**Qué es:** una clase que representa un objeto real del negocio. Tiene un identificador (`Id`) y estado propio.

**Para qué sirve:** modelar las "cosas" que el sistema maneja — un usuario, un pedido, una sucursal.

**Dónde vive:** `{Modulo}.Domain/Entities/`

**Ejemplo:**
```csharp
public sealed class UserProfile
{
    public long   Id        { get; init; }
    public Guid   PublicId  { get; init; }
    public long   TenantId  { get; init; }
    public string FullName  { get; init; } = default!;
    public bool   IsActive  { get; init; }
}
```

**Regla clave:** una Entity solo tiene datos de dominio — sin lógica de base de datos, sin lógica HTTP.

---

### Enum (Enumeración)

**Qué es:** una lista cerrada de valores posibles para algo del dominio.

**Para qué sirve:** reemplazar strings o números mágicos por nombres claros.

**Dónde vive:** `{Modulo}.Domain/Enums/`

**Ejemplo:**
```csharp
public enum UserRole { Admin, Manager, Operator }
```

---

### Repository Interface (Interfaz de repositorio)

**Qué es:** un contrato que define qué operaciones de datos existen, sin decir cómo se hacen.

**Para qué sirve:** desacoplar la lógica de negocio (Application) de cómo se guarda la data (Infrastructure). Application solo conoce la interfaz, nunca la implementación concreta.

**Dónde vive:** `{Modulo}.Domain/Repositories/`

**Ejemplo:**
```csharp
public interface IUserProfileRepository
{
    Task<UserProfile?> GetByPublicIdAsync(Guid publicId, long tenantId, CancellationToken ct = default);
    Task<long> InsertAsync(UserProfile profile, CancellationToken ct = default);
    Task UpdateAsync(UserProfile profile, CancellationToken ct = default);
}
```

**Regla clave:** la interfaz vive en Domain. La implementación vive en Infrastructure. Application conoce solo la interfaz.

---

### Repository Implementation (Implementación de repositorio)

**Qué es:** la clase concreta que implementa la interfaz de repositorio usando la base de datos real.

**Para qué sirve:** ejecutar las operaciones de datos usando las clases `...Sql`.

**Dónde vive:** `{Modulo}.Infrastructure/Repositories/`

**Ejemplo:**
```csharp
public sealed class UserProfileRepository : IUserProfileRepository
{
    private readonly UserProfilesSql _sql;
    public UserProfileRepository(UserProfilesSql sql) => _sql = sql;

    public Task<UserProfile?> GetByPublicIdAsync(Guid publicId, long tenantId, CancellationToken ct = default) =>
        _sql.GetByPublicIdAsync(publicId, tenantId, ct);
}
```

---

### Repository Implementation (Implementación de repositorio con EF Core)

**Qué es:** la clase concreta que implementa la interfaz de repositorio usando `AppDbContext`.

**Para qué sirve:** ejecutar las operaciones de datos usando EF Core LINQ — lecturas, inserciones, actualizaciones y soft deletes.

**Dónde vive:** `{Modulo}.Infrastructure/Repositories/`

**Ejemplo:**
```csharp
public sealed class UserProfileRepository : IUserProfileRepository
{
    private readonly AppDbContext _db;
    public UserProfileRepository(AppDbContext db) => _db = db;

    public async Task<UserProfile?> GetByPublicIdAsync(Guid publicId, CancellationToken ct = default) =>
        await _db.UserProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.PublicId == publicId && e.IsActive, ct);

    public async Task<long> InsertAsync(Guid publicId, long tenantId, string fullName, CancellationToken ct = default)
    {
        var entity = new UserProfile { PublicId = publicId, TenantId = tenantId, FullName = fullName, IsActive = true };
        _db.UserProfiles.Add(entity);
        await _db.SaveChangesAsync(ct);
        return entity.Id;
    }
}
```

**Reglas clave:**
- Inyecta `AppDbContext` directamente — no hay clases intermedias.
- `AsNoTracking()` en todas las lecturas que no van a modificarse.
- `ExecuteUpdateAsync()` para actualizaciones (funciona con propiedades `init`).
- `IgnoreQueryFilters()` solo cuando no hay tenant en contexto (auth).

---

### DTO (Data Transfer Object)

**Qué es:** un objeto simple con solo los datos que se envían al cliente en la respuesta HTTP.

**Para qué sirve:** evitar exponer la entidad de dominio directamente. El DTO controla exactamente qué campos ve el cliente y con qué nombres.

**Dónde vive:** `{Modulo}.Application/Dto/`

**Ejemplo:**
```csharp
public sealed record UserProfileDto(Guid PublicId, string FullName, bool IsActive);
```

**Diferencia con Entity:** la entidad tiene todo el estado del dominio (incluyendo claves internas, auditoría, etc.). El DTO tiene solo lo que el cliente necesita ver.

---

### Request

**Qué es:** un objeto que encapsula los datos de entrada de un caso de uso.

**Para qué sirve:** es el "comando" o "consulta" que el controller envía al handler. Define exactamente qué información necesita el caso de uso para ejecutarse.

**Dónde vive:** `{Modulo}.Application/UseCases/{Accion}/`

**Ejemplo:**
```csharp
public sealed record GetUserProfileRequest(Guid PublicId, long TenantId)
    : IRequest<GetUserProfileResponse>;
```

**Diferencia con RequestBody:** el Request es interno (Application). El RequestBody es el JSON que llega desde HTTP (Presentation). El controller transforma el Body en un Request.

---

### Handler

**Qué es:** la clase que contiene la lógica de un caso de uso. Recibe un Request y devuelve una Response.

**Para qué sirve:** es donde vive la lógica de negocio. Consulta repositorios, aplica reglas, y retorna si fue exitoso o falló.

**Dónde vive:** `{Modulo}.Application/UseCases/{Accion}/`

**Ejemplo:**
```csharp
public sealed class GetUserProfileHandler
    : IRequestHandler<GetUserProfileRequest, GetUserProfileResponse>
{
    private readonly IUserProfileRepository _profiles;
    public GetUserProfileHandler(IUserProfileRepository profiles) => _profiles = profiles;

    public async Task<GetUserProfileResponse> Handle(
        GetUserProfileRequest request, CancellationToken cancellationToken)
    {
        var profile = await _profiles.GetByPublicIdAsync(request.PublicId, request.TenantId, cancellationToken);
        if (profile is null)
            return new GetUserProfileNotFoundFailure("Perfil no encontrado.");
        return new GetUserProfileSuccess(new UserProfileDto(profile.PublicId, profile.FullName, profile.IsActive));
    }
}
```

**Regla clave:** un handler = un caso de uso. No mezclar lógica de múltiples acciones en el mismo handler.

---

### Response (base abstracta)

**Qué es:** la clase base de todos los resultados posibles de un caso de uso.

**Para qué sirve:** agrupar bajo un mismo tipo tanto el éxito como los distintos tipos de fallo. El presenter y el pipeline del mediator usan este tipo base.

**Dónde vive:** `{Modulo}.Application/UseCases/{Accion}/Responses/`

**Ejemplo:**
```csharp
public abstract record GetUserProfileResponse : IResponse;
```

---

### Success (Éxito)

**Qué es:** el resultado cuando el caso de uso se ejecutó correctamente.

**Para qué sirve:** indica al presenter que debe retornar HTTP 200, y carga los datos del DTO.

**Dónde vive:** `{Modulo}.Application/UseCases/{Accion}/Responses/`

**Ejemplo:**
```csharp
// Con datos (HTTP 200 + body)
public sealed record GetUserProfileSuccess(UserProfileDto Data)
    : GetUserProfileResponse, ISuccess<UserProfileDto>;

// Sin datos (HTTP 200 vacío, o con colección)
public sealed record GetUsersSuccess(IReadOnlyCollection<UserProfileDto> Users)
    : GetUsersResponse, ISuccess;
```

---

### Failure (Fallo)

**Qué es:** el resultado cuando el caso de uso no pudo completarse por una razón de negocio.

**Para qué sirve:** comunica al presenter qué tipo de error ocurrió, para que retorne el código HTTP correcto.

**Dónde vive:** `{Modulo}.Application/UseCases/{Accion}/Responses/`

**Tipos disponibles:**

| Interface | HTTP que genera | Cuándo usarla |
|-----------|----------------|---------------|
| `INotFoundFailure` | 404 | El recurso pedido no existe |
| `IConflictFailure` | 409 | Ya existe un registro con esos datos |
| `IValidationFailure` | 400 | Los datos de entrada son inválidos |
| `IFailure` | 500 | Error inesperado del sistema |

**Ejemplo:**
```csharp
public sealed record GetUserProfileNotFoundFailure(string Message)
    : GetUserProfileResponse, INotFoundFailure;

public sealed record CreateUserConflictFailure(string Message)
    : CreateUserResponse, IConflictFailure;
```

---

### Presenter

**Qué es:** la clase que recibe la response del handler (vía el pipeline del mediator) y decide qué hacer con el `ResultViewModel`.

**Para qué sirve:** traduce el resultado del dominio a términos HTTP. El controller solo pregunta `_viewModel.IsSuccess` — toda la lógica de qué código retornar está en el presenter.

**Dónde vive:** `{Modulo}.Presentation/Presenters/`

**Ejemplo:**
```csharp
public sealed class GetUserProfilePresenter : INotificationHandler<GetUserProfileResponse>
{
    private readonly ResultViewModel<UsersController> _viewModel;
    public GetUserProfilePresenter(ResultViewModel<UsersController> vm) => _viewModel = vm;

    public Task Handle(GetUserProfileResponse notification, CancellationToken ct)
    {
        if (notification is IFailure f)          _viewModel.Fail(f.Message);
        else if (notification is ISuccess<UserProfileDto> s) _viewModel.Set(s);
        return Task.CompletedTask;
    }
}
```

**Métodos de ResultViewModel:**

| Método | Cuándo |
|--------|--------|
| `_viewModel.Set(success)` | Response implementa `ISuccess<TDto>` — carga `Data = success.Data` |
| `_viewModel.OK(objeto)` | Éxito con colección u objeto custom — carga `Data = objeto` |
| `_viewModel.Fail(mensaje)` | Cualquier fallo — pone `IsSuccess = false` |

**Regla clave:** los presenters se registran **manualmente** en `ServiceCollectionEx.cs` de Presentation — nunca dejar que el mediator los auto-descubra.

---

### Controller

**Qué es:** la clase que expone endpoints HTTP y orquesta el flujo request → handler → presenter → respuesta.

**Para qué sirve:** es la puerta de entrada del sistema. Recibe el HTTP request, lo convierte en un Request del dominio, espera a que el pipeline termine, y retorna la respuesta HTTP.

**Dónde vive:** `{Modulo}.Presentation/Controllers/`

**Ejemplo:**
```csharp
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
            return _viewModel.IsSuccess ? Ok(_viewModel) : StatusCode(500, _viewModel);
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

**Reglas clave:**
- Extiende `BaseApiController` (no `ControllerBase` directamente).
- Descarta el retorno de `Send` con `_ = await` — la respuesta llega al presenter por el pipeline.
- No contiene lógica de negocio — solo orquesta.

---

### RequestBody

**Qué es:** un record que modela el JSON que llega en el body de un POST o PUT.

**Para qué sirve:** separar la representación HTTP de entrada del Request de Application. Permite validar y transformar antes de construir el Request.

**Dónde vive:** `{Modulo}.Presentation/RequestBodies/`

**Ejemplo:**
```csharp
public sealed record UpdateUserProfileBody(string FullName, string PhoneNumber);
```

El controller lo recibe con `[FromBody]` y construye el Request:
```csharp
[HttpPut("{id:guid}")]
public async Task<IActionResult> Update(Guid id, [FromBody] UpdateUserProfileBody body, CancellationToken ct = default)
{
    _ = await Mediator.Send(new UpdateUserProfileRequest(id, body.FullName, body.PhoneNumber, CurrentTenantId), ct);
    return _viewModel.IsSuccess ? Ok(_viewModel) : StatusCode(500, _viewModel);
}
```

---

### Integration Event (Evento de integración)

**Qué es:** un mensaje que un módulo publica cuando ocurrió algo importante, para que otros módulos puedan reaccionar.

**Para qué sirve:** comunicar módulos sin crear dependencias directas entre ellos. El módulo A publica el evento; el módulo B lo escucha — ninguno sabe del otro.

**Dónde vive:** `{Modulo}.Contracts/Events/`

**Ejemplo:**
```csharp
// Authentication.Contracts/Events/UserShouldBeCreatedIntegrationEvent.cs
public sealed record UserShouldBeCreatedIntegrationEvent(
    Guid PublicId, long TenantId, string FullName, string Email, string Role) : INotification;

// Users.Application escucha el evento (sin referenciar Authentication.Application)
public sealed class UserShouldBeCreatedHandler
    : INotificationHandler<UserShouldBeCreatedIntegrationEvent>
{
    // ... crea el UserProfile cuando llega el evento
}
```

---

### Contracts (proyecto de contratos)

**Qué es:** el único proyecto de un módulo que otros módulos pueden referenciar.

**Para qué sirve:** define la "API pública" del módulo — qué interfaces expone (`ITenancyApi`) y qué eventos publica. Todo lo demás del módulo es privado.

**Dónde vive:** `{Modulo}.Contracts/`

**Regla clave:** si el módulo B necesita algo del módulo A, referencia `A.Contracts` — nunca `A.Domain`, `A.Application`, ni `A.Infrastructure`.

---

### ResultViewModel\<T\>

**Qué es:** el objeto que envuelve toda respuesta HTTP del sistema.

**Para qué sirve:** garantizar un formato de respuesta consistente en todos los endpoints:
```json
{
  "data": { ... },
  "isSuccess": true,
  "message": null,
  "utcTimeStamp": "2025-01-15T10:30:00Z"
}
```

El controller siempre devuelve `Ok(_viewModel)` o `StatusCode(xxx, _viewModel)` — nunca datos directos.

**Dónde se registra:** como open generic Scoped en `Presentation/ServiceCollectionEx.cs`:
```csharp
services.AddScoped(typeof(ResultViewModel<>));
```

---

### BaseApiController

**Qué es:** la clase base de todos los controllers del sistema.

**Para qué sirve:** proveer el `IMediator` protegido y el atributo `[ApiController]` sin repetirlos en cada controller.

**Dónde vive:** `Shared/Web/BaseApiController.cs`

```csharp
[ApiController]
public abstract class BaseApiController : ControllerBase
{
    protected readonly IMediator Mediator;
    protected BaseApiController(IMediator mediator) => Mediator = mediator;
}
```

---

### EntityTypeConfiguration (Configuración de entidad EF Core)

**Qué es:** una clase que implementa `IEntityTypeConfiguration<T>` y define el mapeo de una entidad a su tabla en PostgreSQL.

**Para qué sirve:** centralizar la configuración de tabla, columnas, índices y FK de cada entidad en un solo lugar.

**Dónde vive:** `Shared/Database/EntityTypeConfigurations/`

**Ejemplo:**
```csharp
public sealed class UserProfileConfiguration : IEntityTypeConfiguration<UserProfile>
{
    public void Configure(EntityTypeBuilder<UserProfile> b)
    {
        b.ToTable("user_profiles");
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).UseIdentityByDefaultColumn();
        b.Property(e => e.PublicId).HasDefaultValueSql("gen_random_uuid()");
        b.Property(e => e.FullName).HasMaxLength(200).IsRequired();
        b.Property(e => e.CreatedAtUtc)
            .HasColumnType("timestamp(0)")
            .HasDefaultValueSql("timezone('utc', now())");
        b.HasIndex(e => e.PublicId).IsUnique();
    }
}
```

La tabla se crea automáticamente al iniciar la API via `EnsureCreatedAsync()` en `DatabaseInitializationService`.

**Regla clave:** nunca definir mapeos directamente en `OnModelCreating` — usar configuraciones separadas. Nunca editar columnas existentes sin considerar los datos en producción.

---

### ServiceCollectionEx

**Qué es:** una clase estática con extension methods sobre `IServiceCollection` que registra los servicios de una capa.

**Para qué sirve:** encapsular el registro de DI de cada capa. `Host.Api/Program.cs` solo llama a estos métodos — no sabe qué clases concretas existen en cada módulo.

**Dónde vive:** una por capa — `Application/ServiceCollectionEx.cs`, `Infrastructure/ServiceCollectionEx.cs`, `Presentation/ServiceCollectionEx.cs`.

**Ejemplo:**
```csharp
// Infrastructure
public static IServiceCollection AddUsersInfrastructureServices(this IServiceCollection services)
{
    services.AddScoped<IUserProfileRepository, UserProfileRepository>();
    return services;
}
```

---

### AppDbContext

**Qué es:** el `DbContext` central de EF Core que contiene todos los `DbSet<T>` del sistema y aplica los global query filters de multi-tenancy.

**Para qué sirve:** proporcionar un único punto de acceso a todos los datos del sistema, con filtros de tenant aplicados automáticamente.

**Dónde vive:** `Shared/Database/AppDbContext.cs`

**Ejemplo de uso en repositorio:**
```csharp
public sealed class UserProfileRepository : IUserProfileRepository
{
    private readonly AppDbContext _db;
    public UserProfileRepository(AppDbContext db) => _db = db;

    public async Task<UserProfile?> GetByPublicIdAsync(Guid publicId, CancellationToken ct = default) =>
        await _db.UserProfiles.AsNoTracking()
            .FirstOrDefaultAsync(e => e.PublicId == publicId && e.IsActive, ct);
}
```

El global query filter agrega automáticamente `WHERE TenantId = @currentTenantId` a todas las consultas sobre entidades con filtro — sin necesidad de pasarlo como parámetro.

---

## Resumen visual — flujo de una request

```
HTTP POST /api/users/profile
         │
         ▼
  UsersController          ← Presentation — recibe HTTP, construye Request
         │
         │  Mediator.Send(new GetUserProfileRequest(...))
         ▼
  GetUserProfileHandler    ← Application — ejecuta lógica de negocio
         │  usa IUserProfileRepository (interfaz)
         ▼
  UserProfileRepository    ← Infrastructure — implementa la interfaz
         │  usa UserProfilesSql
         ▼
  AppDbContext             ← Shared/Database — EF Core query con global filter
         ▼
  PostgreSQL               ← base de datos real
         │
         │  retorna UserProfile (Entity)
         │
  Handler retorna GetUserProfileSuccess(dto) o GetUserProfileNotFoundFailure
         │
         │  pipeline publica la response vía Mediator.Publish
         ▼
  GetUserProfilePresenter  ← Presentation — traduce a HTTP
         │  _viewModel.Set(success) | _viewModel.Fail(msg)
         ▼
  UsersController
         │  return Ok(_viewModel) | StatusCode(404, _viewModel)
         ▼
HTTP Response  { data: {...}, isSuccess: true, message: null, utcTimeStamp: "..." }
```

---

## Tabla resumen rápida

| Concepto | Proyecto | Carpeta | Para qué |
|----------|---------|---------|---------|
| Entity | Domain | `Entities/` | Representa una cosa del negocio |
| Enum | Domain | `Enums/` | Lista de valores fijos del dominio |
| Repository Interface | Domain | `Repositories/` | Contrato de acceso a datos |
| DTO | Application | `Dto/` | Datos que ve el cliente (salida) |
| Request | Application | `UseCases/{Accion}/` | Input del caso de uso |
| Handler | Application | `UseCases/{Accion}/` | Lógica del caso de uso |
| Response (base) | Application | `UseCases/{Accion}/Responses/` | Tipo base del resultado |
| Success | Application | `UseCases/{Accion}/Responses/` | Resultado exitoso |
| Failure | Application | `UseCases/{Accion}/Responses/` | Resultado fallido |
| Entity config | Shared/Database | `EntityTypeConfigurations/` | Mapeo EF Core de tabla, columnas, índices |
| Repository Impl | Infrastructure | `Repositories/` | Implementación concreta del repo (AppDbContext) |
| Controller | Presentation | `Controllers/` | Endpoint HTTP |
| Presenter | Presentation | `Presenters/` | Traduce response a HTTP |
| RequestBody | Presentation | `RequestBodies/` | Body JSON del POST/PUT |
| Integration Event | Contracts | `Events/` | Mensaje entre módulos |
| ServiceCollectionEx | cada capa | raíz de capa | Registro DI de la capa |
