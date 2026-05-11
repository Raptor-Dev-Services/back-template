using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Net.Mime;
using System.Text.Json;

namespace Host.Extensions;

/// <summary>
/// Extensiones para configurar y mapear los health checks de la aplicación.
/// </summary>
public static class HealthExtensions
{
    /// <summary>
    /// Registra los health checks. Incluye verificación de conectividad a PostgreSQL.
    /// </summary>
    /// <param name="services">Colección de servicios del contenedor DI.</param>
    /// <param name="configuration">Configuración de la aplicación.</param>
    /// <returns>La misma <paramref name="services"/> para encadenamiento.</returns>
    public static IServiceCollection AddHealthServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHealthChecks()
            .AddNpgSql(
                connectionString: configuration.GetConnectionString("MainDbConnection")!,
                name: "postgres",
                failureStatus: HealthStatus.Unhealthy,
                tags: ["db", "postgres"]);

        return services;
    }

    /// <summary>
    /// Mapea el endpoint <c>GET /api/health</c> con respuesta JSON detallada por servicio.
    /// </summary>
    /// <param name="app">La aplicación web.</param>
    /// <returns>La misma <paramref name="app"/> para encadenamiento.</returns>
    public static WebApplication MapHealth(this WebApplication app)
    {
        app.MapHealthChecks("/api/health", new HealthCheckOptions
        {
            ResponseWriter = WriteJsonResponse
        });

        return app;
    }

    private static async Task WriteJsonResponse(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = MediaTypeNames.Application.Json;

        var response = new
        {
            status        = report.Status.ToString(),
            totalDuration = report.TotalDuration.TotalMilliseconds,
            checks        = report.Entries.Select(e => new
            {
                name     = e.Key,
                status   = e.Value.Status.ToString(),
                duration = e.Value.Duration.TotalMilliseconds,
                tags     = e.Value.Tags,
                error    = e.Value.Exception?.Message
            })
        };

        await context.Response.WriteAsync(
            JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = false }));
    }
}
