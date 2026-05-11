# Observability.md — Logging, Trazas y Métricas

Cómo usar el sistema de observabilidad del proyecto: Serilog → Seq, OpenTelemetry → Jaeger, Prometheus y health checks.

---

## Resumen del stack

| Herramienta | Propósito | URL en dev |
|-------------|-----------|------------|
| **Serilog** | Logging estructurado | — |
| **Seq** | Dashboard de logs, filtros, alertas | `http://localhost:5341` |
| **OpenTelemetry** | Trazas distribuidas y métricas | — |
| **Jaeger** | Visualizar trazas | `http://localhost:16686` |
| **Prometheus** | Scraping de métricas | `http://localhost:5080/metrics` |
| **Grafana** | Dashboards de métricas | `http://localhost:3000` (si se configura) |

---

## Logging con Serilog

### Cómo usar ILogger\<T\>

**Nunca usar `Console.WriteLine`.** Siempre inyectar `ILogger<T>`:

```csharp
public sealed class ProductsController : BaseApiController
{
    private readonly ILogger<ProductsController> _logger;

    public ProductsController(IMediator mediator, ILogger<ProductsController> logger, ...)
    {
        _logger = logger;
    }

    public async Task<IActionResult> GetById(Guid id, CancellationToken ct = default)
    {
        try
        {
            _ = await Mediator.Send(new GetProductRequest(id), ct);
            return _viewModel.IsSuccess ? Ok(_viewModel) : StatusCode(500, _viewModel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en GetById Product id={ProductId}", id);
            //                               ↑ logging estructurado — pasar valores como parámetros,
            //                                 NO interpolar en el string de formato
            var innerEx = ex;
            while (innerEx.InnerException != null) innerEx = innerEx.InnerException!;
            return StatusCode(500, _viewModel.Fail(innerEx.Message));
        }
    }
}
```

### Niveles de log y cuándo usarlos

| Nivel | Cuándo | Ejemplo |
|-------|--------|---------|
| `LogTrace` | Extremo detalle, normalmente desactivado | Cada iteración de un loop interno |
| `LogDebug` | Info de diagnóstico para desarrollo | Valores de variables intermedias |
| `LogInformation` | Flujo normal de la aplicación | "Usuario {UserId} autenticado", "Migración aplicada" |
| `LogWarning` | Algo inesperado pero recuperable | Query lento, validación fallida, 404 |
| `LogError` | Error que afecta una operación | Excepción en un handler, fallo de repositorio |
| `LogCritical` | Fallo crítico del sistema | Base de datos caída, dependencia no disponible |

### Logging estructurado

```csharp
// ✓ Correcto — parámetros nombrados (Serilog los indexa y permite filtrar en Seq)
_logger.LogInformation("Creando producto Name={Name} Price={Price}", name, price);

// ✗ Incorrecto — interpolación de string (pierde la estructura)
_logger.LogInformation($"Creando producto Name={name} Price={price}");
```

### Ver el SQL de las queries

Activar en `appsettings.Local.json`:
```json
"CustomLogging": {
  "IncludeSqlText": true
}
```

Solo activar en el entorno `Local` — en producción puede exponer datos sensibles.

---

## Seq — Dashboard de logs

### Acceder a Seq

```bash
# Levantar Seq con compose-db.yaml
docker compose -f compose-db.yaml up -d

# Abrir en el navegador
http://localhost:5341
```

### Filtros útiles en Seq

```sql
-- Ver todos los logs de error y critical
@Level in ['Error', 'Fatal']

-- Ver logs de un módulo específico
SourceContext like '%Products%'

-- Ver queries lentas (≥300ms)
@Level = 'Warning' and QueryName is not null

-- Ver logs de un correlation ID específico
CorrelationId = 'abc123'

-- Ver logs de la última hora con error
@Level = 'Error' and @Timestamp > Now() - 1h

-- Ver queries SQL (cuando IncludeSqlText=true)
SqlText is not null
```

### Propiedades automáticas en cada log

Serilog enriquece automáticamente cada evento con:

| Propiedad | Origen | Descripción |
|-----------|--------|-------------|
| `Project` | `CustomLogging:Project` | Nombre del proyecto |
| `Application` | `CustomLogging:Application` | Nombre de la aplicación |
| `Version` | `CustomLogging:Version` | Versión |
| `MachineName` | `Environment.MachineName` | Hostname del servidor |
| `Environment` | `ASPNETCORE_ENVIRONMENT` | Local/Development/Staging/Production |
| `CorrelationId` | CorrelationIdMiddleware | ID de la request HTTP |
| `QueryName` | DapperSqlDbConnectionBase | Nombre del query SQL |
| `ElapsedMs` | DapperSqlDbConnectionBase | Tiempo de ejecución del query |
| `SqlHash` | DapperSqlDbConnectionBase | SHA-256 del SQL (para identificar sin exponer) |

---

## OpenTelemetry — Trazas distribuidas

### Levantar Jaeger (agregar al compose-db.yaml o correr aparte)

```bash
docker run -d --name jaeger \
  -p 16686:16686 \
  -p 4317:4317 \
  jaegertracing/all-in-one:latest
```

O agregar al compose-db.yaml:
```yaml
  jaeger:
    image: jaegertracing/all-in-one:latest
    ports:
      - "16686:16686"   # UI
      - "4317:4317"     # OTLP gRPC
```

Configurar en `appsettings.Local.json`:
```json
"Observability": {
  "OtlpEndpoint": "http://localhost:4317"
}
```

### Ver trazas

Abrir `http://localhost:16686`, buscar servicio `back-template-local`.

### Qué se traza automáticamente

OpenTelemetry instrumenta automáticamente:
- Requests HTTP entrantes (ASP.NET Core) — incluye route, status code, duración
- Requests HTTP salientes (HttpClient) — incluye URL, method, status
- Métricas del runtime .NET — GC, threads, allocations

### Agregar atributos a una traza

```csharp
using System.Diagnostics;

// Agregar etiquetas al span actual (sin crear un nuevo span)
Activity.Current?.SetTag("product.id", productId.ToString());
Activity.Current?.SetTag("user.id", userId.ToString());
```

### Crear spans personalizados

```csharp
private static readonly ActivitySource _source = new("back-template.products");

public async Task<Product?> GetByPublicIdAsync(Guid publicId, CancellationToken ct)
{
    using var span = _source.StartActivity("GetProduct");
    span?.SetTag("product.publicId", publicId.ToString());

    var result = await _sql.GetByPublicIdAsync(publicId, ct);

    span?.SetTag("product.found", result is not null);
    return result;
}
```

Registrar el ActivitySource en DI:
```csharp
// No es necesario registrar — ActivitySource es estático
// Pero si quieres que OpenTelemetry lo instrumente, agregar en AddObservability:
// tracing.AddSource("back-template.products");
```

---

## Prometheus — Métricas

### Endpoint de métricas

```bash
curl http://localhost:5080/metrics
```

Respuesta (formato Prometheus text):
```
# HELP http_server_active_requests Number of active HTTP server requests.
# TYPE http_server_active_requests gauge
http_server_active_requests 0

# HELP http_server_request_duration_seconds Duration of HTTP server requests.
# TYPE http_server_request_duration_seconds histogram
http_server_request_duration_seconds_bucket{method="GET",route="/api/products/{id:guid}",status_code="200",le="0.005"} 12
...
```

### Métricas expuestas automáticamente

| Métrica | Tipo | Descripción |
|---------|------|-------------|
| `http_server_request_duration_seconds` | Histogram | Duración de requests HTTP por route/method/status |
| `http_server_active_requests` | Gauge | Requests HTTP activos |
| `http_client_request_duration_seconds` | Histogram | Duración de llamadas HTTP salientes |
| `process_runtime_dotnet_gc_collections_count_total` | Counter | Colecciones de GC por generación |
| `process_runtime_dotnet_gc_heap_size_bytes` | Gauge | Tamaño del heap por generación |
| `process_runtime_dotnet_thread_pool_threads_count` | Gauge | Threads del ThreadPool |
| `process_runtime_dotnet_monitor_lock_contention_count_total` | Counter | Contenciones de lock |

### Crear métricas custom

```csharp
using System.Diagnostics.Metrics;

// En Infrastructure o Application — crear el meter una vez como static
public static class ProductMetrics
{
    private static readonly Meter Meter = new(Common.Observability.ServiceCollectionEx.ApiMeterName);

    // Counter: número de productos creados
    public static readonly Counter<long> ProductsCreated =
        Meter.CreateCounter<long>("products_created_total", "count", "Total de productos creados");

    // Histogram: tiempo de procesamiento de una operación de negocio
    public static readonly Histogram<double> ProcessingTime =
        Meter.CreateHistogram<double>("product_processing_ms", "ms", "Tiempo de procesamiento");
}

// Usarlas en el handler
public async Task<InsertProductResponse> Handle(InsertProductRequest request, CancellationToken ct)
{
    var sw = Stopwatch.StartNew();
    var result = await _repo.InsertAsync(request.Name, request.Description, request.Price, ct);
    sw.Stop();

    ProductMetrics.ProductsCreated.Add(1, new TagList { { "status", "success" } });
    ProductMetrics.ProcessingTime.Record(sw.Elapsed.TotalMilliseconds);

    return new InsertProductSuccess(new ProductDto(...));
}
```

---

## Health Check — `/api/health`

```bash
curl http://localhost:5080/api/health
```

Respuesta:
```json
{
  "status": "Healthy",
  "totalDuration": 12.3,
  "checks": [
    {
      "name": "postgres",
      "status": "Healthy",
      "duration": 11.2,
      "tags": ["db", "postgres"],
      "error": null
    }
  ]
}
```

### Agregar más checks

En `Host/Extensions/HealthExtensions.cs`:

```csharp
services.AddHealthChecks()
    .AddNpgSql(
        connectionString: configuration.GetConnectionString("MainDbConnection")!,
        name: "postgres",
        tags: ["db", "postgres"])
    // Agregar Redis si se usa
    .AddRedis(
        redisConnectionString: configuration["Redis:ConnectionString"]!,
        name: "redis",
        tags: ["cache", "redis"])
    // Check custom
    .AddCheck("external-api", () =>
    {
        // Lógica de verificación
        return HealthCheckResult.Healthy("API externa disponible");
    }, tags: ["external"]);
```

---

## Configuración por entorno

| Entorno | Seq | OTLP/Jaeger | Log level | SQL text |
|---------|-----|-------------|-----------|----------|
| `Local` | localhost:5341 | localhost:4317 | Warning (forzado Debug en dev) | ✓ |
| `Development` | localhost:5341 | localhost:4317 | Debug | ✗ |
| `Staging` | staging-seq | staging-otel | Information | ✗ |
| `Production` | prod-seq | prod-otel | Warning | ✗ |

---

## Correlation ID — trazabilidad end-to-end

Cada request HTTP recibe un `X-Correlation-Id` único. Se propaga:
- En la **response** como header `X-Correlation-Id`
- En los **logs de Serilog** como propiedad `CorrelationId`
- En las **trazas de OpenTelemetry** como atributo `correlation_id`

Para propagar a llamadas HTTP salientes (microservicios):
```csharp
// En el HttpClient
services.AddHttpClient<IMiServicioExterno, MiServicioExterno>()
    .AddHttpMessageHandler<CorrelationIdPropagationHandler>();

// Handler que lee el CorrelationId del contexto y lo agrega al header saliente
public sealed class CorrelationIdPropagationHandler : DelegatingHandler
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CorrelationIdPropagationHandler(IHttpContextAccessor httpContextAccessor)
        => _httpContextAccessor = httpContextAccessor;

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var correlationId = _httpContextAccessor.HttpContext?
            .Items[CorrelationIdMiddleware.HeaderName]?.ToString();

        if (!string.IsNullOrEmpty(correlationId))
            request.Headers.TryAddWithoutValidation(
                CorrelationIdMiddleware.HeaderName, correlationId);

        return base.SendAsync(request, cancellationToken);
    }
}
```
