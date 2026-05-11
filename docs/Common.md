# Common.md — Referencia del Submódulo Common

`Common` es la librería compartida del ecosistema Raptor Dev Services. Se incluye como submódulo Git en `Common/`.

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
| `Common.Data` | DapperSqlDbConnectionBase, IOpenDbConnectionFactory |
| `Common.PostgreSql` | ConfigurationNpgsqlConnectionFactory\<T\>, SchemaMigrationHostedService |
| `Common.Logging` | AddLoggingServices() — Serilog + Seq |
| `Common.Observability` | AddObservability() — OpenTelemetry + Prometheus |
| `Common.Web` | Middleware: CorrelationId, ProblemDetails, TenantResolution |
| `Common.Exceptions` | BusinessRuleException |
| `Common.Errors` | ErrorList |
| `Common.Options` | AddValidatedOptions\<T\>() |
| `Common.MultiTenancy` | Multi-tenancy (no usado en este template) |

---

## Common.Messaging

El sistema de mensajería central del proyecto. Reemplaza MediatR con una implementación propia más simple y transparente.

### Interfaces principales

```csharp
// Marca un request con su tipo de respuesta esperado
public interface IRequest<out TResponse> { }

// Marca una respuesta (también es INotification para poder publicarla)
public interface IResponse : INotification { }

// Implementar en Application/UseCases/.../...Handler.cs
public interface IRequestHandler<in TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    Task<TResponse> Handle(TRequest request, CancellationToken cancellationToken);
}

// Implementar en WebApi/.../Presenters/...Presenter.cs
public interface INotificationHandler<in TNotification>
    where TNotification : INotification
{
    Task Handle(TNotification notification, CancellationToken cancellationToken);
}

// El mediador que inyectas en controllers y handlers que necesiten disparar sub-requests
public interface IMediator
{
    Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default);
    Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
        where TNotification : INotification;
}
```

### Registro — `AddMediator(Assembly)`

```csharp
// Application/ServiceCollectionEx.cs
services.AddMediator(Assembly.GetExecutingAssembly());
```

Qué registra automáticamente:
- `IMediator` → `Mediator` (Scoped)
- `IPipelineBehavior<,>` → `InteractorPipeline<,>` (Scoped)
- Todos los `IRequestHandler<,>` del ensamblado (Scoped)
- Todos los `INotificationHandler<>` del ensamblado (Scoped)

### InteractorPipeline — el corazón del flujo

El pipeline se ejecuta automáticamente entre `Mediator.Send()` y el handler. Su comportamiento:

1. **Loguea el request** con `LogInformation("{@Request}", request)`
2. **Llama al handler** (`next()`)
3. Si la respuesta es `IFailure` → loguea `LogWarning`
4. Si es éxito → loguea `LogInformation`
5. **Publica la respuesta** con `Mediator.Publish(response)` — esto dispara al Presenter
6. Si hay `BusinessRuleException` → `LogError` y relanza
7. Si hay cualquier otra excepción → `LogCritical` y relanza

Por eso el controller descarta el valor de retorno de `Send`:
```csharp
_ = await Mediator.Send(new MiRequest(...), ct);
// La respuesta ya llegó al Presenter vía Publish antes de que Send retorne
```

### Cómo fluye el Mediator.Send internamente

```
Mediator.Send(request)
    → busca IPipelineBehavior<TRequest, TResponse> registrados
    → ejecuta behaviors en cadena (solo InteractorPipeline en este template)
    → InteractorPipeline.Handle(request, next, ct)
        → logs request
        → next() = IRequestHandler<TRequest, TResponse>.Handle(request, ct)
        → logs response
        → Mediator.Publish(response)
            → busca todos los INotificationHandler<TResponse>
            → ejecuta todos en paralelo (Task.WhenAll)
            → el Presenter actualiza el ResultViewModel
        → retorna response
    → Mediator.Send retorna response (que el controller descarta con _=)
```

---

## Common.Results

Interfaces de resultado que tipan el éxito o fracaso de un caso de uso.

```csharp
// Éxito sin datos (Update, Delete, acciones sin retorno)
public interface ISuccess { }

// Éxito con datos (Get, Insert con RETURNING)
public interface ISuccess<T> : ISuccess
{
    T Data { get; }
}

// Fallos — todos tienen string Message
public interface IFailure           { string Message { get; } }
public interface INotFoundFailure   : IFailure { }   // → HTTP 404
public interface IConflictFailure   : IFailure { }   // → HTTP 409
public interface IValidationFailure : IFailure { }   // → HTTP 400
// IFailure genérico sin herencia específica        → HTTP 500
```

### Tabla de mapeo HTTP

| Interface | Significado | Código HTTP |
|-----------|-------------|-------------|
| `ISuccess` | Acción completada sin datos | 200 |
| `ISuccess<T>` | Completada con datos `T Data` | 200 |
| `IFailure` | Error genérico del servidor | 500 |
| `INotFoundFailure` | Recurso no encontrado | 404 |
| `IConflictFailure` | Conflicto (duplicado, estado incorrecto) | 409 |
| `IValidationFailure` | Validación fallida | 400 |

### Uso en los Responses

```csharp
// Response base — abstract, implementa IResponse
public abstract record GetProductResponse : IResponse;

// Éxito con dato
public sealed record GetProductSuccess(ProductDto Data)
    : GetProductResponse, ISuccess<ProductDto>;

// Éxito sin dato
public sealed record DeleteProductSuccess()
    : DeleteProductResponse, ISuccess;

// Fallos
public sealed record GetProductNotFoundFailure(string Message)
    : GetProductResponse, INotFoundFailure;

public sealed record InsertProductConflictFailure(string Message)
    : InsertProductResponse, IConflictFailure;
```

> **NUNCA** hacer `ISuccess<TSelf>` donde `Data => this`. Eso crea referencia circular en la serialización JSON.

---

## Common.ViewModels — ResultViewModel\<T\>

El envelope estándar de todas las respuestas HTTP del proyecto.

```csharp
public class ResultViewModel<T>
{
    public object? Data      { get; private set; }
    public bool    IsSuccess { get; private set; }
    public string? Message   { get; private set; }
    public DateTime UtcTimeStamp { get; private set; }

    // Variante A: éxito con ISuccess<TData> — Data = success.Data
    public void Set<TData>(ISuccess<TData> success, Func<TData, object>? callback = null);

    // Variante B y C: éxito con datos custom u objeto vacío
    public ResultViewModel<T> OK(object data, int statusCode = 200);

    // Fallo: IsSuccess = false, Data = null, Message = mensaje
    public ResultViewModel<T> Fail(string message, int statusCode = 400);
    public ResultViewModel<T> Fail(Exception ex, int statusCode = 400);
}
```

### JSON de respuesta

```json
{
  "data": { "userId": "...", "fullName": "..." },
  "isSuccess": true,
  "message": null,
  "utcTimeStamp": "2026-05-11T14:30:00Z"
}
```

En fallo:
```json
{
  "data": null,
  "isSuccess": false,
  "message": "Usuario no encontrado.",
  "utcTimeStamp": "2026-05-11T14:30:01Z"
}
```

### Cuándo usar cada método

| Método | Presenter usa cuando... |
|--------|------------------------|
| `Set(ISuccess<TDto> success)` | La respuesta implementa `ISuccess<TDto>` |
| `OK(success)` | Éxito con estructura propia (paginación, colección) |
| `OK(new { })` | Éxito sin datos (update, disable) |
| `Fail(message)` | Cualquier `IFailure` |

El Presenter llama uno de estos métodos; el Controller luego hace:
```csharp
return _viewModel.IsSuccess ? Ok(_viewModel) : StatusCode(500, _viewModel);
```

> **El controlador no distingue entre 404/409/400 por diseño.** Todo fallo retorna 500 desde el controller. Si necesitas HTTP semántico, el presenter puede levantar una `HttpResponseException` o usar un flag extra en el ViewModel. Esta es una decisión de diseño intencional para simplificar el controller.

---

## Common.Abstractions

### IPresenter\<TResponse\>

Alias tipado de `INotificationHandler<TResponse>` pensado para los Presenters:

```csharp
public interface IPresenter<TResult> : INotificationHandler<TResult>
    where TResult : IResponse
{ }
```

Implementar en cada `...Presenter.cs`:
```csharp
public sealed class GetProductPresenter : IPresenter<GetProductResponse>
{
    public Task Handle(GetProductResponse notification, CancellationToken cancellationToken)
    {
        // ...
        return Task.CompletedTask;
    }
}
```

### IInteractor\<TRequest, TResponse\>

Alias de `IRequestHandler<TRequest, TResponse>` para los handlers (nombre alternativo):

```csharp
public interface IInteractor<TRequest, TResponse> : IRequestHandler<TRequest, TResponse>
    where TRequest  : IRequest<TResponse>
    where TResponse : IResponse
{ }
```

En este template los handlers implementan directamente `IRequestHandler<,>` por claridad, pero `IInteractor` es equivalente.

---

## Common.Data

### IOpenDbConnectionFactory

Contrato para abrir una `IDbConnection`:

```csharp
public interface IOpenDbConnectionFactory
{
    Task<IDbConnection> GetOpenConnectionAsync(CancellationToken cancellationToken = default);
}
```

`MainDbConnectionFactory` implementa esta interfaz. Se registra como alias en DI:
```csharp
services.AddSingleton<IOpenDbConnectionFactory>(sp => sp.GetRequiredService<MainDbConnectionFactory>());
```

Se usa directamente solo para transacciones manuales (ver [docs/DB.md](DB.md#4-transacciones)).

### DapperSqlDbConnectionBase

Clase base de `MainDapperDbConnection`. Envuelve Dapper con:

- **Timing automático** con `Stopwatch` por cada query
- **Logging de performance** escalado por tiempo de ejecución
- **SHA-256 hash del SQL** en los logs (para identificar queries sin exponer texto)
- **Texto SQL opcional** controlado por `CustomLogging:IncludeSqlText`

```csharp
public MainDapperDbConnection(
    MainDbConnectionFactory factory,
    ILogger<MainDapperDbConnection> logger,
    IConfiguration configuration)
    : base(factory, logger, configuration.GetValue<bool>("CustomLogging:IncludeSqlText"))
{ }
```

#### Umbrales de logging

| Tiempo | Nivel |
|--------|-------|
| < 300 ms | `Debug` (o el nivel que pases en `level`) |
| ≥ 300 ms | `Warning` |
| ≥ 1 000 ms | `Error` |
| ≥ 2 000 ms | `Critical` |

#### Formato del log

```
SQL {QueryName} {Outcome} in {ElapsedMs} ms | hash: {SqlHash} | params: {@Params}
```

Con `IncludeSqlText: true`:
```
SQL {QueryName} {Outcome} in {ElapsedMs} ms | hash: {SqlHash} | sql: {SqlText} | params: {@Params}
```

#### Métodos disponibles

```csharp
// Múltiples filas
Task<IEnumerable<T>> QueryAsync<T>(string sql, object? param = null, string? queryName = null,
    LogLevel level = LogLevel.Debug, CancellationToken cancellationToken = default);

// 0 o 1 fila (lanza si hay más de 1)
Task<T?> QuerySingleAsync<T>(string sql, object? param = null, ...);

// Primera fila o null
Task<T?> QueryFirstAsync<T>(string sql, object? param = null, ...);

// INSERT / UPDATE / DELETE → filas afectadas
Task<int> ExecuteAsync(string sql, object? param = null, ...);

// COUNT, EXISTS, escalar
Task<T> ExecuteScalarAsync<T>(string sql, object? param = null, ...);
```

El `queryName` es opcional — si no se pasa, se usa el nombre del método (`QueryAsync`, `ExecuteAsync`, etc.). Pasar un nombre explícito mejora la trazabilidad en los logs.

---

## Common.PostgreSql

### ConfigurationNpgsqlConnectionFactory\<TConnectionName\>

Clase base genérica para la fábrica de conexiones. El parámetro de tipo `TConnectionName` es la **clase marcadora** — su nombre de tipo se usa como clave en `ConnectionStrings`:

```csharp
// Clase marcadora — el nombre "MainDbConnection" es la clave de configuración
public sealed class MainDbConnection;

// Fábrica — hereda la fábrica genérica con el marcador
public sealed class MainDbConnectionFactory : ConfigurationNpgsqlConnectionFactory<MainDbConnection>
{
    public MainDbConnectionFactory(IConfiguration configuration) : base(configuration) { }
}
```

Internamente lee:
```csharp
configuration.GetConnectionString(typeof(TConnectionName).Name)
// = configuration.GetConnectionString("MainDbConnection")
```

En `appsettings.json`:
```json
"ConnectionStrings": {
  "MainDbConnection": "Host=localhost;Port=5432;Database=back_template;Username=postgres;Password=postgres"
}
```

### SchemaMigrationHostedService

Servicio en segundo plano que se ejecuta al iniciar la aplicación. Aplica las migraciones SQL pendientes automáticamente.

**Comportamiento:**
1. Localiza los archivos `.sql` en `Services/Schema Migration/Tables/` ordenados alfabéticamente
2. Crea el esquema `dbo` si no existe
3. Crea la tabla `dbo.SchemaMigrations` si no existe (guarda los scripts aplicados)
4. Por cada archivo, si no está en `SchemaMigrations` → lo ejecuta en una transacción → registra el nombre
5. Si la DB no está lista → reintenta hasta 20 veces con backoff (1 s, 2 s... hasta 5 s máximo)
6. Archivos que empiezan con `000_template` son ignorados (reservado para plantillas de ejemplo)

**Tabla de control:**
```sql
CREATE TABLE IF NOT EXISTS dbo.SchemaMigrations (
    ScriptName   VARCHAR(260) PRIMARY KEY,
    AppliedAtUtc TIMESTAMP(0) NOT NULL DEFAULT (timezone('utc', now()))
);
```

### AddSchemaMigrations()

```csharp
// Host/Program.cs
builder.Services.AddSchemaMigrations();

// Opcional: ruta personalizada (por defecto "Services/Schema Migration/Tables")
builder.Services.AddSchemaMigrations(opt =>
    opt.ScriptsRelativePath = Path.Combine("MiCarpeta", "Migrations"));
```

---

## Common.Logging — AddLoggingServices()

Configura Serilog con:
- Sink `Console`
- Sink `Debug`
- Sink `Seq` (opcional, solo si `SeqUri` está configurado)
- Enrichers: `FromLogContext`, `TenantId` (si hay multi-tenancy), `Project`, `MachineName`, `Environment`, `Application`, `Version`

### Claves de configuración

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

| Clave | Tipo | Descripción |
|-------|------|-------------|
| `Project` | string | Nombre del proyecto (aparece en todos los logs) |
| `Application` | string | Nombre de la aplicación |
| `Version` | string | Versión del servicio |
| `SeqUri` | string | URL de Seq. Vacío = no envía a Seq |
| `LogEventLevel` | string | Nivel mínimo: `Verbose`, `Debug`, `Information`, `Warning`, `Error`, `Fatal` |
| `IncludeSqlText` | bool | Incluye el SQL real en los logs. Solo activar en `Local` |

> En `Development` el nivel mínimo se fuerza a `Debug` aunque `LogEventLevel` sea mayor.

---

## Common.Observability — AddObservability()

Configura OpenTelemetry con:
- **Trazas** (`WithTracing`): ASP.NET Core + HttpClient, exporta a OTLP (Jaeger)
- **Métricas** (`WithMetrics`): ASP.NET Core + HttpClient + Runtime + Prometheus scraping + OTLP

### Claves de configuración

```json
"Observability": {
  "ServiceName":    "back-template-api",
  "ServiceVersion": "1.0.0",
  "OtlpEndpoint":   "http://localhost:4317"
}
```

| Clave | Descripción |
|-------|-------------|
| `ServiceName` | Nombre del servicio en Jaeger/Grafana |
| `ServiceVersion` | Versión del servicio |
| `OtlpEndpoint` | Endpoint OTLP gRPC. Vacío = no exporta trazas/métricas a OTLP |

El endpoint de Prometheus siempre queda en `/metrics` (no requiere config):
```csharp
app.MapPrometheusScrapingEndpoint();  // → GET /metrics
```

---

## Common.Web — Middleware

### `UseCoreProblemDetails()` — ProblemDetailsMiddleware

Captura excepciones no manejadas y retorna respuestas RFC 7807 (`application/problem+json`):

| Excepción | Código HTTP | Title |
|-----------|-------------|-------|
| `BusinessRuleException` | 400 | "Business rule violation" |
| Cualquier otra excepción | 500 | "Unhandled error" |

La respuesta incluye `traceId` del `Activity.Current` para correlación con Jaeger.

### `UseCorrelationId()` — CorrelationIdMiddleware

- Lee el header `X-Correlation-Id` del request
- Si no existe, genera un GUID nuevo
- Agrega el header `X-Correlation-Id` en la response
- Propaga el correlation ID a los logs de Serilog y a los atributos de OpenTelemetry (`correlation_id`)

Útil para correlacionar logs y trazas de una misma request entre servicios.

---

## Common.Exceptions

### BusinessRuleException

Excepción de dominio para violaciones de reglas de negocio. El `ProblemDetailsMiddleware` la captura y retorna HTTP 400 automáticamente.

```csharp
// Lanzar una regla de negocio
throw new BusinessRuleException("El stock no puede ser negativo.");

// Equivalente con ErrorList
var errors = new ErrorList();
errors.Add("El campo Nombre es requerido.");
errors.Add("El precio debe ser mayor a cero.");
if (!errors.IsEmpty) throw errors.AsException();
```

---

## Common.Options — AddValidatedOptions\<T\>()

Registra `IOptions<T>` con validación de DataAnnotations y validación al inicio:

```csharp
// Ejemplo de uso (no requerido en este template, pero disponible)
services.AddValidatedOptions<JwtOptions>(configuration.GetSection("Jwt"));

public class JwtOptions
{
    [Required] public string Key { get; set; } = "";
    [Required] public string Issuer { get; set; } = "";
    [Range(1, 1440)] public int ExpirationMinutes { get; set; } = 60;
}
```

---

## Common.MultiTenancy

Sistema completo de multi-tenancy disponible en la librería pero **no usado en este template**.

Capacidades disponibles si se necesita en el futuro:
- Resolución de tenant por header (`X-Tenant-Id`), query string o subdominio
- `ITenantContextAccessor` — accede al tenant actual en cualquier punto del código
- `ITenantConnectionStringResolver` — resuelve connection strings por tenant
- `ITenantExecutionContextRunner` — ejecuta código en el contexto de un tenant específico
- Middleware `UseTenantResolution()` — integra todo en el pipeline HTTP
- `TenantPropagationHttpMessageHandler` — propaga el tenant a llamadas HTTP salientes

Para activarlo:
```csharp
// Program.cs
builder.Services.AddMultiTenancy(builder.Configuration);
// ...
app.UseTenantResolution();
```

---

## Resumen de todos los métodos de extensión de Common

| Método | Dónde llamarlo | Descripción |
|--------|---------------|-------------|
| `AddMediator(Assembly)` | `Application/ServiceCollectionEx.cs` | Registra mediator + handlers |
| `AddLoggingServices(config)` | `Host/Program.cs` | Serilog + Seq |
| `AddObservability(config)` | `Host/Program.cs` | OpenTelemetry + Prometheus |
| `AddSchemaMigrations()` | `Host/Program.cs` | Migraciones SQL automáticas |
| `UseCoreProblemDetails()` | `Host/Program.cs` | Middleware de errores RFC 7807 |
| `UseCorrelationId()` | `Host/Program.cs` | Middleware de correlation ID |
| `AddValidatedOptions<T>(section)` | Cualquier ServiceCollectionEx | Options con validación |
| `AddMultiTenancy(config)` | `Host/Program.cs` | Multi-tenancy (opcional) |
| `UseTenantResolution()` | `Host/Program.cs` | Middleware de tenant (opcional) |
