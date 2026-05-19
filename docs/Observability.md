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

Registro en `Host.Api/Program.cs`:

```csharp
builder.Services.AddLoggingServices(builder.Configuration);
builder.Services.AddObservability(builder.Configuration);
```

---

## Logging con Serilog

### Cómo usar ILogger\<T\>

**Nunca usar `Console.WriteLine`.** Siempre inyectar `ILogger<T>`:

```csharp
public sealed class UsersController : ControllerBase
{
    private readonly ILogger<UsersController> _logger;

    public async Task<IActionResult> GetById(Guid id, CancellationToken ct = default)
    {
        try
        {
            var result = await _mediator.Send(new GetUserProfileRequest(id, CurrentTenantId), ct);
            if (_viewModel.IsSuccess) return Ok(_viewModel);
            return result is GetUserProfileNotFoundFailure ? NotFound(_viewModel) : StatusCode(500, _viewModel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en GetById UserProfile id={UserProfileId}", id);
            var inner = ex;
            while (inner.InnerException != null) inner = inner.InnerException!;
            return StatusCode(500, _viewModel.Fail(inner.Message));
        }
    }
}
```

### Niveles de log y cuándo usarlos

| Nivel | Cuándo | Ejemplo |
|-------|--------|---------|
| `LogTrace` | Extremo detalle, normalmente desactivado | Cada iteración de un loop interno |
| `LogDebug` | Info de diagnóstico para desarrollo | Valores de variables intermedias |
| `LogInformation` | Flujo normal de la aplicación | "Usuario autenticado", "Migración aplicada" |
| `LogWarning` | Algo inesperado pero recuperable | Query lento, 404, validación fallida |
| `LogError` | Error que afecta una operación | Excepción en un handler |
| `LogCritical` | Fallo crítico del sistema | Base de datos caída |

### Logging estructurado — siempre parámetros nombrados

```csharp
// ✓ Correcto — Serilog indexa los valores y permite filtrar en Seq
_logger.LogInformation("Actualizando perfil PublicId={PublicId} TenantId={TenantId}", publicId, tenantId);

// ✗ Incorrecto — interpolación pierde la estructura
_logger.LogInformation($"Actualizando perfil PublicId={publicId}");
```

### Ver el SQL de las queries

Activar en `appsettings.Local.json`:

```json
"CustomLogging": {
  "IncludeSqlText": true
}
```

Solo activar en `Local` — en producción puede exponer datos sensibles.

---

## Seq — Dashboard de logs

```bash
# Levantar Seq con compose-db.yaml
docker compose -f compose-db.yaml up -d

# Abrir en el navegador
# http://localhost:5341
```

### Filtros útiles en Seq

```sql
-- Ver todos los logs de error
@Level in ['Error', 'Fatal']

-- Ver logs de un módulo específico
SourceContext like '%Users%'

-- Ver queries lentas (≥300ms)
@Level = 'Warning' and QueryName is not null

-- Ver logs de un correlation ID específico
CorrelationId = 'abc123'

-- Ver queries SQL (cuando IncludeSqlText=true)
SqlText is not null
```

### Propiedades automáticas en cada log

| Propiedad | Origen | Descripción |
|-----------|--------|-------------|
| `Project` | `CustomLogging:Project` | Nombre del proyecto |
| `Application` | `CustomLogging:Application` | Nombre de la aplicación |
| `MachineName` | `Environment.MachineName` | Hostname |
| `Environment` | `ASPNETCORE_ENVIRONMENT` | Local/Development/Staging/Production |
| `CorrelationId` | CorrelationIdMiddleware | ID de la request HTTP |
| `QueryName` | MainDapperDbConnection | Nombre del query SQL |
| `ElapsedMs` | MainDapperDbConnection | Tiempo de ejecución del query |
| `SqlHash` | MainDapperDbConnection | SHA-256 del SQL |

---

## OpenTelemetry — Trazas distribuidas

### Configuración en `appsettings.Local.json`

```json
"Observability": {
  "ServiceName":    "back-template-local",
  "ServiceVersion": "1.0.0",
  "OtlpEndpoint":   "http://localhost:4317"
}
```

### Levantar Jaeger

```yaml
# Agregar al compose-db.yaml:
  jaeger:
    image: jaegertracing/all-in-one:latest
    ports:
      - "16686:16686"   # UI
      - "4317:4317"     # OTLP gRPC
```

Ver trazas en `http://localhost:16686`, buscar servicio `back-template-local`.

### Agregar atributos a una traza

```csharp
using System.Diagnostics;

Activity.Current?.SetTag("user.tenantId", tenantId.ToString());
Activity.Current?.SetTag("profile.publicId", publicId.ToString());
```

---

## Prometheus — Métricas

```bash
curl http://localhost:5080/metrics
```

### Métricas expuestas automáticamente

| Métrica | Tipo | Descripción |
|---------|------|-------------|
| `http_server_request_duration_seconds` | Histogram | Duración de requests HTTP |
| `http_server_active_requests` | Gauge | Requests activos |
| `process_runtime_dotnet_gc_collections_count_total` | Counter | Colecciones de GC |
| `process_runtime_dotnet_gc_heap_size_bytes` | Gauge | Tamaño del heap |
| `process_runtime_dotnet_thread_pool_threads_count` | Gauge | Threads del ThreadPool |

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
      "duration": 11.2
    }
  ]
}
```

### Agregar más checks

`Host.Api/Extensions/HealthExtensions.cs`:

```csharp
services.AddHealthChecks()
    .AddNpgSql(
        connectionString: configuration.GetConnectionString("MainDbConnection")!,
        name: "postgres",
        tags: ["db", "postgres"])
    .AddRedis(
        redisConnectionString: configuration["Redis:ConnectionString"]!,
        name: "redis",
        tags: ["cache", "redis"]);
```

---

## Configuración por entorno

| Entorno | Seq | OTLP/Jaeger | Log level | SQL text |
|---------|-----|-------------|-----------|----------|
| `Local` | localhost:5341 | localhost:4317 | Debug | ✓ |
| `Development` | localhost:5341 | localhost:4317 | Debug | ✗ |
| `Staging` | staging-seq | staging-otel | Information | ✗ |
| `Production` | prod-seq | prod-otel | Warning | ✗ |

---

## Correlation ID — trazabilidad end-to-end

Cada request HTTP recibe un `X-Correlation-Id` único. Se propaga en:
- Response como header `X-Correlation-Id`
- Logs de Serilog como propiedad `CorrelationId`
- Trazas de OpenTelemetry como atributo `correlation_id`

Activado automáticamente con `app.UseCorrelationId()` en `Host.Api/Program.cs`.
