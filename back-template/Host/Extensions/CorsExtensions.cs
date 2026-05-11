namespace Host.Extensions;

/// <summary>
/// Extensiones para configurar la política CORS de la aplicación.
/// </summary>
public static class CorsExtensions
{
    /// <summary>Nombre de la política CORS registrada.</summary>
    public const string PolicyName = "LocalhostPolicy";

    /// <summary>
    /// Registra una política CORS que permite cualquier origen <c>http://localhost:*</c> y <c>https://localhost:*</c>.
    /// Útil para desarrollo local con frontends en React, Angular, Vue, etc.
    /// </summary>
    /// <param name="services">Colección de servicios del contenedor DI.</param>
    /// <returns>La misma <paramref name="services"/> para encadenamiento.</returns>
    public static IServiceCollection AddLocalhostCors(this IServiceCollection services)
    {
        services.AddCors(options =>
        {
            options.AddPolicy(PolicyName, policy =>
            {
                policy
                    .SetIsOriginAllowed(origin =>
                    {
                        var uri = new Uri(origin);
                        return uri.Host == "localhost" || uri.Host == "127.0.0.1";
                    })
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials();
            });
        });

        return services;
    }
}
