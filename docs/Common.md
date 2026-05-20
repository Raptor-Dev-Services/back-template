# Common.md — Referencia del Submódulo Common

`Common` es la librería compartida del ecosistema Raptor Dev Services. Se incluye como submódulo Git en `Common/Common/Common.csproj`.

**Repositorio:** https://github.com/Raptor-Dev-Services/Common

> **Regla absoluta:** nunca editar archivos dentro de `Common/` desde este repositorio. Todos los cambios a la librería se hacen en su propio repositorio y se actualizan con `git submodule update --remote`.

---

## Actualizar el submódulo

```bash
# Traer la última versión del submódulo
git submodule update --remote --merge

# Verificar qué commit apunta el submódulo
git submodule status
```

---

## Namespaces disponibles

| Namespace | Propósito |
|-----------|-----------|
| `Common.Messaging` | Mediator, IRequest, IResponse, handlers, pipeline |
| `Common.Results` | Interfaces de resultado: ISuccess, IFailure, INotFoundFailure, etc. |
| `Common.ViewModels` | ResultViewModel\<T\> — respuesta HTTP estandarizada |
| `Common.Abstractions` | IPresenter\<T\>, IInteractor\<TRequest, TResponse\> |
| `Common.Data` | DapperSqlDbConnectionBase, IOpenDbConnectionFactory _(no usado — EF Core)_ |
| `Common.PostgreSql` | ConfigurationNpgsqlConnectionFactory\<T\>, SchemaMigrationHostedService _(no usado — EF Core)_ |
| `Common.Logging` | AddLoggingServices() — Serilog + Seq |
| `Common.Observability` | AddObservability() — OpenTelemetry + Prometheus |
| `Common.Web` | Middleware: CorrelationId, ProblemDetails |
| `Common.MultiTenancy` | ITenantContextAccessor, TenantContextAccessor |
| `Common.Exceptions` | BusinessRuleException |
| `Common.Errors` | ErrorList |
| `Common.Options` | AddValidatedOptions\<T\>() |

---

## Common.Messaging

El sistema de mensajería central del proyecto. Reemplaza MediatR con una implementación propia más simple y transparente.

### Interfaces principales

```csharp
public interface IRequest<out TResponse> { }
public interface IResponse : INotification { }

public interface IRequestHandler<in TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    Task<TResponse> Handle(TRequest request, CancellationToken cancellationToken);
}

public interface INotificationHandler<in TNotification>
    where TNotification : INotification
{
    Task Handle(TNotification notification, CancellationToken cancellationToken);
}

public interface IMediator
{
    Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default);
    Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
        where TNotification : INotification;
}
```

### Registro — `AddMediator(params Assembly[])`

En este proyecto se hace **una única llamada** a `AddMediator` en `Host.Api/Program.cs` con los assemblies de todos los módulos Application:

```csharp
builder.Services.AddMediator(
    typeof(Tenancy.Application.ServiceCollectionEx).Assembly,
    typeof(Users.Application.ServiceCollectionEx).Assembly,
    typeof(Authentication.Application.ServiceCollectionEx).Assembly
);
```

> **Una sola llamada — crítico.** Múltiples llamadas a `AddMediator` registran el pipeline varias veces, causando que cada response sea publicada N veces hacia los presenters.

`AddMediator` registra automáticamente por cada assembly:
- `IMediator` → `Mediator` (Scoped)
- `IPipelineBehavior<,>` → `InteractorPipeline<,>` (Scoped)
- Todos los `IRequestHandler<,>` del assembly (Scoped)

> **Los `INotificationHandler<>` (presenters) se registran manualmente en cada `{Modulo}.Presentation/ServiceCollectionEx.cs`** — no en el scan de `AddMediator`. Esto evita doble invocación.

### InteractorPipeline — el corazón del flujo

El pipeline se ejecuta automáticamente entre `Mediator.Send()` y el handler:

1. Loguea el request con `LogInformation("{@Request}", request)`
2. Llama al handler (`next()`)
3. Si la respuesta es `IFailure` → loguea `LogWarning`; si es éxito → `LogInformation`
4. **Publica la respuesta** con `Mediator.Publish(response)` — dispara al Presenter
5. Si hay `BusinessRuleException` → `LogError` y relanza
6. Si hay cualquier otra excepción → `LogCritical` y relanza

Por eso el controller descarta el valor de retorno de `Send` con `_=`:

```csharp
_ = await _mediator.Send(new MiRequest(...), ct);
// La respuesta ya llegó al Presenter vía Publish antes de que Send retorne
```

La excepción es cuando el controller necesita distinguir el tipo de fallo para el HTTP status code:

```csharp
var result = await _mediator.Send(new GetUserProfileRequest(id, tenantId), ct);
if (_viewModel.IsSuccess) return Ok(_viewModel);
return result is GetUserProfileNotFoundFailure ? NotFound(_viewModel) : StatusCode(500, _viewModel);
```

### Cómo fluye el Mediator.Send internamente

```
Mediator.Send(request)
    → InteractorPipeline.Handle(request, next, ct)
        → logs request
        → next() = IRequestHandler.Handle(request, ct) → response
        → logs response
        → Mediator.Publish(response)
            → busca INotificationHandler<TResponse> registrados
            → ejecuta todos (Task.WhenAll)
            → el Presenter actualiza el ResultViewModel
        → retorna response
```

---

## Common.Results

```csharp
public interface ISuccess { }
public interface ISuccess<T> : ISuccess { T Data { get; } }

public interface IFailure           { string Message { get; } }
public interface INotFoundFailure   : IFailure { }   // → HTTP 404
public interface IConflictFailure   : IFailure { }   // → HTTP 409
public interface IValidationFailure : IFailure { }   // → HTTP 400
// IFailure sin herencia específica                  → HTTP 500
```

### Uso en los Responses

```csharp
// Response base
public abstract record GetUserProfileResponse : IResponse;

// Éxito con dato único
public sealed record GetUserProfileSuccess(UserProfileDto Data) : GetUserProfileResponse, ISuccess<UserProfileDto>;

// Éxito con estructura propia (paginación)
// NO usar ISuccess<TSelf> — referencia circular en JSON
public sealed record GetUserProfilesSuccess(
    IReadOnlyCollection<UserProfileDto> Items, int Total, int Page, int PageSize)
    : GetUserProfilesResponse, ISuccess;

// Fallos
public sealed record GetUserProfileNotFoundFailure(string Message) : GetUserProfileResponse, INotFoundFailure;
```

---

## Common.ViewModels — ResultViewModel\<T\>

```csharp
public class ResultViewModel<T>
{
    public object? Data       { get; private set; }
    public bool    IsSuccess  { get; private set; }
    public string? Message    { get; private set; }
    public DateTime UtcTimeStamp { get; private set; }

    public void Set<TData>(ISuccess<TData> success);         // Data = success.Data
    public ResultViewModel<T> OK(object data);               // Data = el objeto
    public ResultViewModel<T> Fail(string message);          // IsSuccess = false
    public ResultViewModel<T> Fail(Exception ex);
}
```

| Método | Cuándo usar |
|--------|------------|
| `Set(ISuccess<TDto> s)` | Success implementa `ISuccess<TDto>` |
| `OK(success)` | Success con estructura propia (paginación, colección) |
| `OK(new { })` | Éxito sin datos (update, disable) |
| `Fail(msg)` | Cualquier `IFailure` |

Registrado como `Scoped` en cada `{Modulo}.Presentation/ServiceCollectionEx.cs`:

```csharp
services.AddScoped(typeof(ResultViewModel<>));
```

---

## Common.Abstractions

### IPresenter\<TResponse\>

```csharp
public interface IPresenter<TResult> : INotificationHandler<TResult>
    where TResult : IResponse
{ }
```

---

## Common.Data _(no usado en este proyecto)_

`Common.Data` expone `IOpenDbConnectionFactory` y `DapperSqlDbConnectionBase` — abstracciones sobre Dapper/Npgsql. En la configuración actual con EF Core, **no se usan**. La conexión a PostgreSQL la gestiona `AppDbContext` vía `Npgsql.EntityFrameworkCore.PostgreSQL`.

---

## Common.PostgreSql _(no usado en este proyecto)_

`Common.PostgreSql` expone `ConfigurationNpgsqlConnectionFactory<T>` y `SchemaMigrationHostedService` — fábrica de conexiones Dapper y migración SQL por scripts. En la configuración actual con EF Core, **no se usan**. El esquema se inicializa con `DatabaseInitializationService` (`EnsureCreatedAsync()`).

---

## Common.Logging — AddLoggingServices()

```json
"CustomLogging": {
  "Project":       "back-template",
  "Application":   "back-template-api",
  "Version":       "1.0.0",
  "SeqUri":        "http://localhost:5341",
  "LogEventLevel": "Debug",
  "IncludeSqlText": false
}
```

---

## Common.Observability — AddObservability()

```json
"Observability": {
  "ServiceName":    "back-template-api",
  "ServiceVersion": "1.0.0",
  "OtlpEndpoint":   "http://localhost:4317"
}
```

El endpoint de Prometheus siempre queda en `/metrics`:

```csharp
app.MapPrometheusScrapingEndpoint();
```

---

## Common.MultiTenancy

`ITenantContextAccessor` permite propagar el `TenantId` actual en el contexto de Serilog, OpenTelemetry y el global query filter de EF Core.

En este proyecto se registra directamente (sin llamar a `AddMultiTenancy()` completo):

```csharp
// Host.Api/Program.cs
builder.Services.AddSingleton<ITenantContextAccessor, TenantContextAccessor>();
```

El middleware `TenantClaimsMiddleware` lee los claims del JWT y los inyecta en `ITenantContextAccessor`:

```csharp
app.UseMiddleware<TenantClaimsMiddleware>();
```

---

## Common.Web — Middleware

### `UseCoreProblemDetails()`

Captura excepciones no manejadas y retorna respuestas RFC 7807:

| Excepción | Código HTTP |
|-----------|-------------|
| `BusinessRuleException` | 400 |
| Cualquier otra excepción | 500 |

### `UseCorrelationId()`

Lee `X-Correlation-Id` del request (o genera un GUID nuevo), propaga el header en la response, y enriquece logs y trazas con `correlation_id`.

---

## Resumen de métodos de extensión de Common

| Método | Dónde llamarlo | Descripción |
|--------|---------------|-------------|
| `AddMediator(assembly1, assembly2, ...)` | `Host.Api/Program.cs` | Registrar mediator + handlers de todos los módulos |
| `AddLoggingServices(config)` | `Host.Api/Program.cs` | Serilog + Seq |
| `AddObservability(config)` | `Host.Api/Program.cs` | OpenTelemetry + Prometheus |
| ~~`AddSchemaMigrations()`~~ | ~~`Host.Api/Program.cs`~~ | _(reemplazado por `DatabaseInitializationService`)_ |
| `UseCoreProblemDetails()` | `Host.Api/Program.cs` | Middleware de errores RFC 7807 |
| `UseCorrelationId()` | `Host.Api/Program.cs` | Middleware de correlation ID |
| `AddValidatedOptions<T>(section)` | Cualquier ServiceCollectionEx | Options con validación |
