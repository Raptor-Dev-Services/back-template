# Observabilidad y salud

## Logs (Serilog)

`Host.Api/Extensions/ObservabilityExtensions.cs`. Serilog es el unico proveedor de logging.

- En Development: texto legible con propiedades. Fuera: JSON compacto (un evento por linea).
- Seq opcional con `Seq__ServerUrl` (en el devstack, `http://localhost:5341`).
- Niveles desde la seccion `Serilog` de la configuracion. `Microsoft.AspNetCore` y
  `Microsoft.AspNetCore.Hosting.Diagnostics` quedan en `Warning`: el log de hosting escribe la URL completa, con
  query string.
- **Un evento por peticion** (`UseAppRequestLogging`): metodo, ruta **sin query string**, status y latencia. Error
  si hubo excepcion o 5xx; las sondas `/health` bajan a `Verbose`.
- `RequestContextEnricher` agrega a cada evento `TraceId`, `CorrelationId`, `TenantId` y `UserId`. Solo
  identificadores: nunca nombre, correo ni tokens.
- El pipeline del mediador de Common registra request y response de cada caso de uso con los campos sensibles
  tapados.
- Plantillas con propiedades (`logger.LogInformation("Tarea {Code}...", code)`), nunca interpolacion. Nunca
  `Console.WriteLine`.

## Correlacion

`CorrelationIdMiddleware` acepta `X-Correlation-Id` del cliente (saneado, 128 caracteres max) o usa el TraceId, lo
publica para los logs y lo devuelve en la respuesta.

## Trazas y metricas

`AddObservability` de Common: OpenTelemetry con exportacion OTLP a `Observability__OtlpEndpoint` (vacio = no se
exporta). El meter se llama `Observability:MeterName` (`BackTemplate.Api`). No hay endpoint Prometheus.

## Salud

`Host.Api/Extensions/HealthExtensions.cs`, ambas anonimas:

| Ruta | Significado | Checks |
|---|---|---|
| `/health/live` | el proceso esta vivo | ninguno (no toca dependencias) |
| `/health/ready` | puede servir trafico; 503 si algo falla | `postgres` (con el rol de la app) y `object-storage` (el bucket existe) |

La respuesta es JSON con el estado por check y mensajes genericos; el detalle de un fallo va al log.

El contenedor usa `/health/ready` como `HEALTHCHECK` mediante `src/Host/HealthProbe` (la imagen chiseled no trae
curl ni wget), y el deploy no se da por bueno hasta que responde sano. Ver [Deployment.md](Deployment.md).

## Bitacora de acciones

Distinta de los logs: `IAuditLog` guarda en la base, dentro de la transaccion de la accion, quien hizo que sobre
que (invitar, bloquear, dar de baja, quitar 2FA, pausar/ejecutar tareas). Se consulta en `GET /api/v1/audit-log`
(`audit.read`), paginada y filtrable por `action`.

## Tareas programadas

`AutomatedTaskRun` es el historial de cada corrida (estado, procesados, fallidos, mensaje, quien la disparo). Se
consulta en `GET /api/v1/automated-tasks/{code}/runs`.
