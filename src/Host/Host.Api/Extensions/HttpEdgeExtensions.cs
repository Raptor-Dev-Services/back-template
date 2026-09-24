using System.Globalization;
using System.Net;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Shared.Kernel.Security;
using Shared.Web;
using Shared.Web.Errors;
using Shared.Web.Http;

namespace Host.Api.Extensions;

/// <summary>
/// El borde HTTP: CORS por origen, IP real detras de un proxy DE CONFIANZA, limite de tasa y cabeceras de
/// seguridad. Todo configurable y con fail-fast donde un error de configuracion abriria un hueco en silencio.
/// </summary>
public static class HttpEdgeExtensions
{
    public const string CorsPolicy = "Default";

    public static IServiceCollection AddHttpEdge(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        AddCors(services, configuration, environment);
        AddForwardedHeaders(services, configuration);
        if (IsRateLimitingEnabled(configuration, environment))
            AddRateLimiting(services, configuration);
        return services;
    }

    /// <summary>
    /// Orden del borde. Cada posicion tiene su motivo:
    /// <list type="number">
    ///   <item>ForwardedHeaders ANTES que todo: el limitador particiona por la IP real, no la del proxy.</item>
    ///   <item>Correlacion y cabeceras de seguridad: cualquier respuesta posterior (401, 429, 500) sale con ambas.</item>
    ///   <item>Manejo de errores: envuelve el resto del pipeline.</item>
    ///   <item>CORS antes de autenticar: el preflight OPTIONS se resuelve sin token y no cuenta contra el limite.</item>
    ///   <item>Limitador DESPUES de autenticar: la politica por usuario necesita el claim <c>sub</c> ya validado.</item>
    /// </list>
    /// </summary>
    public static WebApplication UseHttpEdge(this WebApplication app)
    {
        if (app.Configuration.GetValue<bool>("ForwardedHeaders:Enabled"))
            app.UseForwardedHeaders();

        app.UseMiddleware<CorrelationIdMiddleware>();
        app.UseMiddleware<SecurityHeadersMiddleware>();
        if (!app.Environment.IsDevelopment() && !app.Environment.IsEnvironment("Testing"))
            app.UseHsts();

        return app;
    }

    /// <summary>El limitador, solo si esta encendido. Va DESPUES de UseAuthentication (ver <see cref="UseHttpEdge"/>).</summary>
    public static WebApplication UseRateLimitingIfEnabled(this WebApplication app)
    {
        if (IsRateLimitingEnabled(app.Configuration, app.Environment))
            app.UseRateLimiter();
        return app;
    }

    /// <summary>
    /// En Testing el limitador va apagado por omision: el TestServer no fija la IP y toda la suite compartiria una
    /// particion, con 429 falsos. <c>RateLimiting:Enabled</c> fuerza la decision en cualquier entorno (una prueba lo
    /// enciende para medir el propio limitador).
    /// </summary>
    private static bool IsRateLimitingEnabled(IConfiguration configuration, IHostEnvironment environment) =>
        configuration.GetValue<bool?>("RateLimiting:Enabled") ?? !environment.IsEnvironment("Testing");

    private static void AddCors(IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        // Se acepta un ESCALAR separado por comas (lo que se pone en una variable de entorno) o un ARREGLO (lo que
        // se escribe en appsettings). Son claves distintas en IConfiguration; si solo se leyera el arreglo, un
        // origen puesto por la variable documentada se ignoraria en silencio. El escalar gana.
        var section = configuration.GetSection("Cors:AllowedOrigins");
        var origins = (string.IsNullOrWhiteSpace(section.Value) ? section.Get<string[]>() ?? [] : [section.Value])
            .SelectMany(o => o.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        foreach (var origin in origins)
        {
            if (origin == "*" || !Uri.TryCreate(origin, UriKind.Absolute, out var uri) || uri.AbsolutePath != "/")
                throw new InvalidOperationException(
                    $"Cors:AllowedOrigins tiene un origen invalido: '{origin}'. Usa esquema://host[:puerto], sin comodines ni rutas.");
            if (environment.IsProduction() && uri.Scheme != Uri.UriSchemeHttps)
                throw new InvalidOperationException($"En produccion los origenes CORS deben ser https: '{origin}'.");
        }

        services.AddCors(options => options.AddPolicy(CorsPolicy, policy =>
        {
            // Por origen, nunca AllowAnyOrigin. Sin credenciales: la sesion viaja en el header Authorization, no en
            // cookies. X-Correlation-Id expuesto para que la SPA pueda mostrarlo al reportar un fallo.
            if (origins.Length > 0)
                policy.WithOrigins(origins)
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .WithExposedHeaders(CorrelationIdMiddleware.HeaderName, "Retry-After");
        }));
    }

    private static void AddForwardedHeaders(IServiceCollection services, IConfiguration configuration)
    {
        if (!configuration.GetValue<bool>("ForwardedHeaders:Enabled"))
            return;

        var proxies = Split(configuration["ForwardedHeaders:KnownProxies"]);
        var networks = Split(configuration["ForwardedHeaders:KnownNetworks"]);
        if (proxies.Length == 0 && networks.Length == 0)
            throw new InvalidOperationException(
                "ForwardedHeaders:Enabled=true sin KnownProxies ni KnownNetworks: se confiaria en un X-Forwarded-For " +
                "que cualquiera puede inventar. Declara la IP o la red del proxy.");

        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.KnownProxies.Clear();
            options.KnownIPNetworks.Clear();

            foreach (var proxy in proxies)
                options.KnownProxies.Add(IPAddress.TryParse(proxy, out var ip)
                    ? ip
                    : throw new InvalidOperationException($"ForwardedHeaders:KnownProxies tiene una IP invalida: '{proxy}'."));

            foreach (var network in networks)
                options.KnownIPNetworks.Add(System.Net.IPNetwork.TryParse(network, out var net)
                    ? net
                    : throw new InvalidOperationException($"ForwardedHeaders:KnownNetworks tiene una red invalida: '{network}'."));
        });
    }

    private static void AddRateLimiting(IServiceCollection services, IConfiguration configuration)
    {
        var limits = configuration.GetSection("RateLimiting");
        var authPerMinute = limits.GetValue("AuthPerMinute", 10);
        var uploadPerMinute = limits.GetValue("UploadPerMinute", 30);
        var globalPerMinute = limits.GetValue("GlobalPerMinute", 300);

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            // Estricto y por IP: la superficie de fuerza bruta (login, 2FA, refresh, restablecer, bootstrap).
            options.AddPolicy(RateLimitPolicies.Auth, http =>
                RateLimitPartition.GetFixedWindowLimiter($"auth:{ClientIp(http)}", _ => PerMinute(authPerMinute)));

            // Por USUARIO (cae a la IP sin sesion): rotar de IP no debe dar cuota nueva a quien ya porta un token.
            options.AddPolicy(RateLimitPolicies.Upload, http =>
                RateLimitPartition.GetFixedWindowLimiter(
                    $"upload:{http.User.FindFirst(AppClaimTypes.Subject)?.Value ?? ClientIp(http)}", _ => PerMinute(uploadPerMinute)));

            // Red de seguridad holgada por IP para todo lo demas; las politicas nombradas se apilan encima.
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(http =>
                RateLimitPartition.GetFixedWindowLimiter($"global:{ClientIp(http)}", _ => PerMinute(globalPerMinute)));

            // El 429 del limitador no pasa por MVC: aqui se arma el MISMO envelope que el resto de la API.
            options.OnRejected = async (context, cancellationToken) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                    context.HttpContext.Response.Headers.RetryAfter = ((int)retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);

                await ApiEnvelope.WriteFailureAsync(context.HttpContext, StatusCodes.Status429TooManyRequests,
                    ApiEnvelope.DefaultMessageFor(StatusCodes.Status429TooManyRequests));
            };
        });
    }

    private static FixedWindowRateLimiterOptions PerMinute(int permits) => new()
    {
        PermitLimit = permits,
        Window = TimeSpan.FromMinutes(1),
        QueueLimit = 0,
        AutoReplenishment = true,
    };

    private static string ClientIp(HttpContext http) => http.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    private static string[] Split(string? value) =>
        (value ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
