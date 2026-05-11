# Back.md — Arquitectura Backend

Guía de referencia para agregar funcionalidad al backend. Todo código generado debe respetar estas convenciones sin excepción.

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
| Passwords | BCrypt.Net-Next |
| Logging | Serilog → Seq |
| Tracing | OpenTelemetry OTLP → Jaeger |
| Métricas | Prometheus (`/metrics`) |
| Health | `/api/health` |
| Testing | xUnit |
| Deploy | Docker multi-stage |

---

## Capas y dependencias

```
Domain
Application    → Domain
Infrastructure → Domain + Common
WebApi         → Application + Common
Host           → Application + Infrastructure + WebApi + Common
Common         (transversal — sin lógica de negocio del proyecto)
```

**Reglas absolutas:**
- `Application` nunca referencia `Infrastructure`.
- `WebApi` nunca accede a PostgreSQL ni a repositorios concretos.
- `Domain` no tiene dependencias de proyecto.

---

## Árbol de directorios

```
back-template/
├── Domain/
│   ├── Entities/{Modulo}/
│   └── Repositories/{Modulo}/
├── Application/
│   ├── Dto/{Modulo}/
│   ├── UseCases/
│   │   └── {Modulo}/{Accion}/
│   │       ├── {Accion}Request.cs
│   │       ├── {Accion}Handler.cs
│   │       └── Responses/
│   │           ├── {Accion}Response.cs      ← abstract record : IResponse
│   │           ├── {Accion}Success.cs       ← sealed record : {Accion}Response, ISuccess<T>
│   │           └── {Accion}Failure.cs       ← sealed record : {Accion}Response, INotFoundFailure (etc)
│   └── ServiceCollectionEx.cs
├── Infrastructure/
│   ├── PostgreSql/
│   │   ├── MainDbConnection.cs
│   │   ├── MainDbConnectionFactory.cs
│   │   └── MainDapperDbConnection.cs
│   ├── Persistence/
│   │   └── SQLDB/Main/{Modulo}/{Entidad}Sql.cs
│   ├── Repositories/{Modulo}/
│   └── ServiceCollectionEx.cs
├── WebApi/
│   ├── Base/BaseApiController.cs
│   ├── EndPoints/{Modulo}/
│   │   ├── {Modulo}Controller.cs
│   │   ├── Presenters/{Accion}Presenter.cs
│   │   └── RequestBodies/{Accion}Body.cs
│   └── ServiceCollectionEx.cs
├── Host/
│   ├── Program.cs
│   ├── appsettings.json
│   └── Services/Schema Migration/Tables/*.sql
├── Tests/
└── Common/   (submódulo — NO editar)
```

---

## Flujo de una request

```
HTTP Request
    ↓
{Modulo}Controller  →  _ = await Mediator.Send(new {Accion}Request(...), ct)
                                ↓
                    {Accion}Handler.Handle(...)
                        lógica de negocio
                        return new {Accion}Success(...) | new {Accion}Failure(...)
                                ↓
                    InteractorPipeline (automático en Common.Messaging)
                        await Mediator.Publish(response)
                                ↓
                    {Accion}Presenter.Handle(response, ct)
                        rellena ResultViewModel<TController>
                                ↓
Controller  →  _viewModel.IsSuccess ? Ok(_viewModel) : StatusCode(500, _viewModel)
                                ↓
HTTP Response  (siempre ResultViewModel JSON)
```

El `InteractorPipeline` está registrado automáticamente por `AddMediator()`. El controller descarta el valor de retorno de `Send` (`_ = await ...`) porque la respuesta ya llegó al presenter vía Publish.

---

## Patrón de caso de uso

### 1. Request — `Application/UseCases/{Modulo}/{Accion}/{Accion}Request.cs`

```csharp
using Common.Messaging;

namespace Application.UseCases.Example.GetExampleUser;

public sealed record GetExampleUserRequest(Guid PublicId) : IRequest<GetExampleUserResponse>;
```

### 2. Responses — `Application/UseCases/{Modulo}/{Accion}/Responses/`

**`{Accion}Response.cs`** — contrato base (abstract, implementa `IResponse`):

```csharp
using Common.Messaging;

namespace Application.UseCases.Example.GetExampleUser;

public abstract record GetExampleUserResponse : IResponse;
```

**`{Accion}Success.cs`** — caso de éxito:

```csharp
// Variante A — éxito con un DTO único (usa ISuccess<TDto>)
using Application.Dto.Example.User;
using Common.Results;

namespace Application.UseCases.Example.GetExampleUser;

public sealed record GetExampleUserSuccess(ExampleUserDto Data) : GetExampleUserResponse, ISuccess<ExampleUserDto>;
```

```csharp
// Variante B — éxito con datos que no encajan en ISuccess<T> (paginación, colecciones, etc.)
// Usa ISuccess (sin genérico) y pasa el objeto completo al presenter
using Application.Dto.Example.User;
using Common.Results;

namespace Application.UseCases.Example.GetExampleUsers;

public sealed record GetExampleUsersSuccess(
    IReadOnlyCollection<ExampleUserDto> Users,
    int Total,
    int Page,
    int PageSize) : GetExampleUsersResponse, ISuccess;
```

> **NUNCA** implementar `ISuccess<TSelf>` con `Data => this`. Eso crea referencia circular en la serialización JSON.

**`{Accion}Failure.cs`** — caso de error:

```csharp
using Common.Results;

namespace Application.UseCases.Example.GetExampleUser;

public sealed record GetExampleUserNotFoundFailure(string Message) : GetExampleUserResponse, INotFoundFailure;
```

**Interfaces de resultado disponibles en `Common.Results`:**

| Interface | Semántica HTTP |
|-----------|---------------|
| `ISuccess` | 200 sin datos estructurados |
| `ISuccess<T>` | 200 con propiedad `T Data { get; }` |
| `IFailure` | Fallo genérico (500) |
| `INotFoundFailure` | 404 |
| `IConflictFailure` | 409 |
| `IValidationFailure` | 400 |

### 3. Handler — `Application/UseCases/{Modulo}/{Accion}/{Accion}Handler.cs`

```csharp
using Application.Dto.Example.User;
using Common.Messaging;
using Domain.Repositories.Example;

namespace Application.UseCases.Example.GetExampleUser;

public sealed class GetExampleUserHandler : IRequestHandler<GetExampleUserRequest, GetExampleUserResponse>
{
    private readonly IExampleUserRepository _repo;

    public GetExampleUserHandler(IExampleUserRepository repo) => _repo = repo;

    public async Task<GetExampleUserResponse> Handle(
        GetExampleUserRequest request, CancellationToken cancellationToken)
    {
        var user = await _repo.GetByPublicIdAsync(request.PublicId, cancellationToken);
        if (user is null)
            return new GetExampleUserNotFoundFailure("Usuario no encontrado.");

        return new GetExampleUserSuccess(new ExampleUserDto(
            user.PublicId, user.FullName, user.Email, user.Department,
            user.Notes, user.IsActive, user.CreatedAtUtc, user.UpdatedAtUtc));
    }
}
```

---

## Patrón Presenter + ResultViewModel (OBLIGATORIO)

Toda respuesta HTTP pasa por `ResultViewModel<TController>` de `Common.ViewModels`. Nunca retornar datos directamente desde el controller.

### 4. Presenter — `WebApi/EndPoints/{Modulo}/Presenters/{Accion}Presenter.cs`

**Variante A — éxito implementa `ISuccess<TDto>`:**

```csharp
using Application.Dto.Example.User;
using Application.UseCases.Example.GetExampleUser;
using Common.Abstractions;
using Common.Results;
using Common.ViewModels;

namespace WebApi.EndPoints.Example.Presenters;

public sealed class GetExampleUserPresenter : IPresenter<GetExampleUserResponse>
{
    private readonly ResultViewModel<ExampleUsersController> _viewModel;

    public GetExampleUserPresenter(ResultViewModel<ExampleUsersController> viewModel)
        => _viewModel = viewModel;

    public Task Handle(GetExampleUserResponse notification, CancellationToken cancellationToken)
    {
        if (notification is IFailure failure)
            _viewModel.Fail(failure.Message);
        else if (notification is ISuccess<ExampleUserDto> success)
            _viewModel.Set(success);           // ← Data = success.Data (el DTO)

        return Task.CompletedTask;
    }
}
```

**Variante B — éxito implementa `ISuccess` (sin genérico), datos en el record completo:**

```csharp
using Application.UseCases.Example.GetExampleUsers;
using Common.Abstractions;
using Common.Results;
using Common.ViewModels;

namespace WebApi.EndPoints.Example.Presenters;

public sealed class GetExampleUsersPresenter : IPresenter<GetExampleUsersResponse>
{
    private readonly ResultViewModel<ExampleUsersController> _viewModel;

    public GetExampleUsersPresenter(ResultViewModel<ExampleUsersController> viewModel)
        => _viewModel = viewModel;

    public Task Handle(GetExampleUsersResponse notification, CancellationToken cancellationToken)
    {
        if (notification is IFailure failure)
            _viewModel.Fail(failure.Message);
        else if (notification is GetExampleUsersSuccess success)
            _viewModel.OK(success);            // ← Data = el record completo (Users, Total, Page, PageSize)

        return Task.CompletedTask;
    }
}
```

**Variante C — éxito sin datos (Update, Disable, acciones que no retornan entidad):**

```csharp
using Application.UseCases.Example.UpdateExampleUser;
using Common.Abstractions;
using Common.Results;
using Common.ViewModels;

namespace WebApi.EndPoints.Example.Presenters;

public sealed class UpdateExampleUserPresenter : IPresenter<UpdateExampleUserResponse>
{
    private readonly ResultViewModel<ExampleUsersController> _viewModel;

    public UpdateExampleUserPresenter(ResultViewModel<ExampleUsersController> viewModel)
        => _viewModel = viewModel;

    public Task Handle(UpdateExampleUserResponse notification, CancellationToken cancellationToken)
    {
        if (notification is IFailure failure)
            _viewModel.Fail(failure.Message);
        else if (notification is ISuccess)
            _viewModel.OK(new { });            // ← éxito sin datos: objeto vacío

        return Task.CompletedTask;
    }
}
```

**Métodos de `ResultViewModel<T>`:**

| Método | Cuándo usarlo |
|--------|--------------|
| `_viewModel.Set(ISuccess<TDto> success)` | Variante A — éxito con `ISuccess<TDto>`, Data = success.Data |
| `_viewModel.OK(object data)` | Variante B — éxito con datos custom (paginación, colecciones) |
| `_viewModel.OK(new { })` | Variante C — éxito sin datos (update, disable, acciones sin retorno) |
| `_viewModel.Fail(string message)` | Cualquier fallo — IsSuccess = false |

### 5. Controller — `WebApi/EndPoints/{Modulo}/{Modulo}Controller.cs`

```csharp
using Application.UseCases.Example.GetExampleUser;
using Application.UseCases.Example.GetExampleUsers;
using Common.Messaging;
using Common.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using WebApi.Base;
using WebApi.EndPoints.Example.RequestBodies;

namespace WebApi.EndPoints.Example;

[Route("api/example/users")]
// [Authorize]   ← descomentar en producción
public sealed class ExampleUsersController : BaseApiController
{
    private readonly ILogger<ExampleUsersController> _logger;
    private readonly ResultViewModel<ExampleUsersController> _viewModel;

    public ExampleUsersController(
        IMediator mediator,
        ILogger<ExampleUsersController> logger,
        ResultViewModel<ExampleUsersController> viewModel) : base(mediator)
    {
        _logger    = logger;
        _viewModel = viewModel;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page     = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        try
        {
            _ = await Mediator.Send(new GetExampleUsersRequest(page, pageSize), ct);
            return _viewModel.IsSuccess ? Ok(_viewModel) : StatusCode(500, _viewModel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en GetAll ExampleUsers");
            var innerEx = ex;
            while (innerEx.InnerException != null) innerEx = innerEx.InnerException!;
            return StatusCode(500, _viewModel.Fail(innerEx.Message));
        }
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct = default)
    {
        try
        {
            _ = await Mediator.Send(new GetExampleUserRequest(id), ct);
            return _viewModel.IsSuccess ? Ok(_viewModel) : StatusCode(500, _viewModel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en GetById ExampleUser");
            var innerEx = ex;
            while (innerEx.InnerException != null) innerEx = innerEx.InnerException!;
            return StatusCode(500, _viewModel.Fail(innerEx.Message));
        }
    }
}
```

**Reglas del controller:**
- Siempre extiende `BaseApiController` (inyecta `IMediator`).
- `_ = await Mediator.Send(...)` — descarta el retorno.
- `_viewModel.IsSuccess ? Ok(_viewModel) : StatusCode(500, _viewModel)` en el happy path.
- `catch` llama `_viewModel.Fail(innerEx.Message)` y retorna `StatusCode(500, ...)`.
- El `IMediator` es `Common.Messaging.IMediator`, **no** MediatR NuGet.

---

## Registro de dependencias (DI)

### `Application/ServiceCollectionEx.cs`

```csharp
using Common.Messaging;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace Application;

public static class ServiceCollectionEx
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddMediator(Assembly.GetExecutingAssembly());
        return services;
    }
}
```

`AddMediator` escanea el ensamblado y registra automáticamente todos los `IRequestHandler<,>`. También registra el `InteractorPipeline` que hace el Publish tras cada handler.

### `WebApi/ServiceCollectionEx.cs`

```csharp
using Common.Messaging;
using Common.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using WebApi.EndPoints.Example.Presenters;

namespace WebApi;

public static class ServiceCollectionEx
{
    public static IMvcBuilder AddWebApiServices(this IServiceCollection services)
    {
        services.AddScoped(typeof(ResultViewModel<>));

        // Presenters — registrar uno por abstract response
        services.AddScoped<INotificationHandler<GetExampleUserResponse>,  GetExampleUserPresenter>();
        services.AddScoped<INotificationHandler<GetExampleUsersResponse>, GetExampleUsersPresenter>();
        services.AddScoped<INotificationHandler<InsertExampleUserResponse>, InsertExampleUserPresenter>();
        services.AddScoped<INotificationHandler<UpdateExampleUserResponse>, UpdateExampleUserPresenter>();
        services.AddScoped<INotificationHandler<DisableExampleUserResponse>, DisableExampleUserPresenter>();

        return services
            .AddControllers()
            .AddApplicationPart(Assembly.GetExecutingAssembly());
    }
}
```

- `ResultViewModel<>` se registra open-generic como `Scoped`.
- Cada presenter se registra como `INotificationHandler<TAbstractResponse>` (también `Scoped`).
- El `InteractorPipeline` resuelve todos los handlers registrados para el tipo de respuesta y les hace Publish.

---

## Convenciones de nomenclatura

| Tipo | Patrón | Ejemplo |
|------|--------|---------|
| Request | `{Accion}Request` | `GetExampleUserRequest` |
| Handler | `{Accion}Handler` | `GetExampleUserHandler` |
| Response base | `{Accion}Response` | `GetExampleUserResponse` |
| Respuesta exitosa | `{Accion}Success` | `GetExampleUserSuccess` |
| Respuesta de error | `{Accion}{Tipo}Failure` | `GetExampleUserNotFoundFailure` |
| Presenter | `{Accion}Presenter` | `GetExampleUserPresenter` |
| Request body | `{Accion}Body` | `InsertExampleUserBody` |
| Controller | `{Modulo}Controller` | `ExampleUsersController` |
| DTO | `{Entidad}Dto` | `ExampleUserDto` |
| Repositorio interfaz | `I{Entidad}Repository` | `IExampleUserRepository` |
| Servicio interfaz | `I{Feature}Service` | `IJwtTokenService` |

---

## Program.cs — composición del Host

```csharp
using Application;
using Common.Logging;
using Common.Observability;
using Common.PostgreSql;
using Common.Web;
using Host.Extensions;
using Infrastructure;
using WebApi;

var builder = WebApplication.CreateBuilder(args);

// Observabilidad
builder.Services.AddLoggingServices(builder.Configuration);        // Serilog → Seq
builder.Services.AddObservability(builder.Configuration);          // OpenTelemetry OTLP + Prometheus

// Capas de la aplicación
builder.Services.AddApplicationServices();                         // Mediator + handlers
builder.Services.AddInfrastructureServices(builder.Configuration); // DB + repositorios
builder.Services.AddWebApiServices();                              // Presenters + controllers

// Servicios del host
builder.Services.AddSchemaMigrations();                            // Migraciones SQL automáticas
builder.Services.AddHealthServices(builder.Configuration);         // /api/health (Npgsql check)

builder.Services.AddJwtAuthentication(builder.Configuration);      // JWT HS256
builder.Services.AddLocalhostCors();                               // CORS localhost:*

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerWithJwt();                              // Swagger + Bearer UI

var app = builder.Build();

// Swagger: activo en Local, Development y Staging
if (app.Environment.IsDevelopment() ||
    app.Environment.IsEnvironment("Local") ||
    app.Environment.IsEnvironment("Staging"))
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Middleware
app.UseCoreProblemDetails();
app.UseCorrelationId();
// app.UseHttpsRedirection();   // habilitar en producción con TLS terminado en el host
app.UseCors(CorsExtensions.PolicyName);
app.UseAuthentication();
app.UseAuthorization();

// Endpoints
app.MapControllers();
app.MapHealth();
app.MapPrometheusScrapingEndpoint();   // /metrics

app.Run();
```

---

## Host/Extensions

Cada extensión encapsula la configuración de un servicio. Viven en `Host/Extensions/`.

### `JwtAuthExtensions.cs` — `AddJwtAuthentication(config)`

Lee `Jwt:Key`, `Jwt:Issuer`, `Jwt:Audience`. Lanza `InvalidOperationException` si alguno falta. HS256, `ClockSkew = TimeSpan.Zero`.

### `CorsExtensions.cs` — `AddLocalhostCors()` + `CorsExtensions.PolicyName`

Permite cualquier origen `localhost` / `127.0.0.1`, cualquier header/método, con credenciales. El `PolicyName` es la constante que pasa a `app.UseCors(...)`.

### `SwaggerExtensions.cs` — `AddSwaggerWithJwt()`

Swagger con esquema Bearer pre-configurado. El campo de token en Swagger UI no requiere el prefijo "Bearer".

### `HealthExtensions.cs` — `AddHealthServices(config)` + `MapHealth()`

Registra check de PostgreSQL con tag `["db", "postgres"]`. Expone `GET /api/health` con JSON:

```json
{
  "status": "Healthy",
  "totalDuration": 12.3,
  "checks": [
    { "name": "postgres", "status": "Healthy", "duration": 11.2, "tags": ["db","postgres"], "error": null }
  ]
}
```

---

## Entornos y appsettings

| Entorno | Archivo | Swagger | SQL text log |
|---------|---------|---------|--------------|
| `Local` | `appsettings.Local.json` | ✓ | ✓ |
| `Development` | `appsettings.Development.json` | ✓ | ✗ |
| `Staging` | `appsettings.Staging.json` | ✓ | ✗ |
| `Production` | `appsettings.Production.json` | ✗ | ✗ |

`Local` es el perfil de trabajo diario en máquina: activa `CustomLogging:IncludeSqlText: true` para ver el SQL real en los logs de Serilog.

---

## Observabilidad

- **Logging:** `ILogger<T>` vía inyección → Serilog → Seq (`http://localhost:5341` en dev).
- **Tracing:** OpenTelemetry OTLP → Jaeger (`http://localhost:16686` en dev).
- **Métricas:** Prometheus en `/metrics`.
- **Health:** `/api/health`.
- Nunca usar `Console.WriteLine`. Nunca duplicar config que ya existe en `Common`.

---

## Reglas que no se negocian

1. `Application` nunca referencia `Infrastructure`.
2. `WebApi` nunca accede a PostgreSQL ni a repositorios concretos.
3. El mediador es `Common.Messaging.IMediator` — **nunca MediatR NuGet**.
4. Todo SQL vive en clases `...Sql` — cero SQL inline en repositorios, servicios o handlers.
5. Toda respuesta HTTP pasa por `ResultViewModel<TController>` — nunca retornar datos directos.
6. No secretos en `appsettings*.json` — todo secreto va en variables de entorno.
7. No editar el submódulo `Common` desde este repositorio.
8. Al terminar cualquier cambio: `dotnet build` desde `Host` con 0 errores antes de dar la tarea por terminada.

---

## Checklist al agregar un módulo nuevo

- [ ] Entidad de dominio en `Domain/Entities/{Modulo}/`
- [ ] Interfaz de repositorio en `Domain/Repositories/{Modulo}/`
- [ ] DTO en `Application/Dto/{Modulo}/`
- [ ] Por cada acción — `{Accion}Request.cs`, `{Accion}Handler.cs`, `Responses/{Accion}Response.cs`, `Responses/{Accion}Success.cs`, `Responses/{Accion}Failure.cs`
- [ ] Clase `{Entidad}Sql` en `Infrastructure/Persistence/SQLDB/Main/{Modulo}/`
- [ ] Implementación del repositorio en `Infrastructure/Repositories/{Modulo}/`
- [ ] DI en `Infrastructure/ServiceCollectionEx.cs`
- [ ] Presenter por acción en `WebApi/EndPoints/{Modulo}/Presenters/`
- [ ] Controller en `WebApi/EndPoints/{Modulo}/`
- [ ] Registrar presenters en `WebApi/ServiceCollectionEx.cs`
- [ ] Migraciones SQL en `Host/Services/Schema Migration/Tables/`
- [ ] `dotnet build` pasa sin errores
