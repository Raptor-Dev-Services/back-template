using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Shared.Infrastructure.Persistence;

namespace Host.Api.Extensions;

/// <summary>
/// Dos sondas con significados distintos, porque el orquestador toma decisiones distintas con cada una:
/// <list type="bullet">
///   <item><c>/health/live</c>: el PROCESO esta vivo. No toca dependencias: una base lenta no debe hacer que el
///   orquestador reinicie una aplicacion sana.</item>
///   <item><c>/health/ready</c>: puede servir trafico. Corre los checks etiquetados <see cref="ReadyTag"/>
///   (Postgres con el rol de la aplicacion). 503 si alguno falla: se le deja de enrutar trafico.</item>
/// </list>
/// Anonimas, fuera del limite de tasa por IP estricto, y registradas a nivel Verbose (ver RequestLogging).
/// </summary>
public static class HealthExtensions
{
    public const string ReadyTag = "ready";

    public static IServiceCollection AddHealthServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHealthChecks()
            .AddCheck<PostgresHealthCheck>("postgres", tags: [ReadyTag]);
        return services;
    }

    public static WebApplication MapHealth(this WebApplication app)
    {
        app.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = _ => false,
            ResponseWriter = HealthResponseWriter.WriteAsync,
        }).AllowAnonymous();

        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = registration => registration.Tags.Contains(ReadyTag),
            ResponseWriter = HealthResponseWriter.WriteAsync,
        }).AllowAnonymous();

        return app;
    }
}

/// <summary>
/// Postgres alcanzable por la MISMA conexion de la aplicacion (rol _app, con el interceptor de RLS). No consulta
/// entidades, asi que no depende del contexto de tenant. El detalle del fallo queda para el log, no para la
/// respuesta.
/// </summary>
internal sealed class PostgresHealthCheck(AppDbContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            return await db.Database.CanConnectAsync(cancellationToken)
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy("PostgreSQL no responde.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return HealthCheckResult.Unhealthy("PostgreSQL no responde.", ex);
        }
    }
}

/// <summary>
/// El minimo util para el orquestador: estado global y, por check, nombre y estado. Nunca descripciones, mensajes
/// de excepcion ni datos de conexion (la version anterior devolvia <c>Exception.Message</c>, que en un fallo de
/// Npgsql incluye host y usuario). La decision la toma el codigo HTTP (200/503); el diagnostico vive en el log.
/// </summary>
public static class HealthResponseWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static Task WriteAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json; charset=utf-8";
        var payload = new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(e => new { name = e.Key, status = e.Value.Status.ToString() }),
        };
        return context.Response.WriteAsync(JsonSerializer.Serialize(payload, JsonOptions), context.RequestAborted);
    }
}
