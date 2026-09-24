using System.Diagnostics;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting.Compact;
using Shared.Kernel.Context;
using Shared.Web.Http;

namespace Host.Api.Extensions;

public static class ObservabilityExtensions
{
    /// <summary>
    /// Serilog como UNICO proveedor de logging. Niveles y overrides desde la seccion <c>Serilog</c> de la
    /// configuracion (se ajustan por entorno sin recompilar). En Development, texto legible con las propiedades del
    /// evento; fuera, JSON compacto (un evento por linea) que el agregador indexa por TraceId/TenantId. Seq opcional
    /// con <c>Seq:ServerUrl</c>.
    ///
    /// <para>Los eventos se escriben con plantilla y propiedades, nunca interpolando valores en el texto. El
    /// pipeline del mediador de Common registra cada request y response con los campos sensibles ya tapados
    /// (SensitiveDataMasker, desde aedf830).</para>
    /// </summary>
    public static WebApplicationBuilder AddAppLogging(this WebApplicationBuilder builder)
    {
        builder.Services.AddHttpContextAccessor();
        builder.Host.UseSerilog((context, services, logger) =>
        {
            logger
                .ReadFrom.Configuration(context.Configuration)
                .Enrich.FromLogContext()
                .Enrich.WithProperty("Application", context.Configuration["Observability:ServiceName"] ?? "back-template-api")
                .Enrich.With(new RequestContextEnricher(services.GetRequiredService<IHttpContextAccessor>()));

            if (context.HostingEnvironment.IsDevelopment())
                logger.WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}");
            else
                logger.WriteTo.Console(new CompactJsonFormatter());

            var seq = context.Configuration["Seq:ServerUrl"];
            if (!string.IsNullOrWhiteSpace(seq))
                logger.WriteTo.Seq(seq);
        });

        return builder;
    }

    /// <summary>
    /// Un evento por peticion con metodo, ruta, status y latencia. SIN query string (default de Serilog, que aqui se
    /// deja explicito): puede llevar tokens o datos personales. Las sondas de salud bajan a Verbose: el orquestador
    /// las llama cada pocos segundos y a Information serian ruido y costo en el agregador.
    /// </summary>
    public static WebApplication UseAppRequestLogging(this WebApplication app)
    {
        app.UseSerilogRequestLogging(options =>
        {
            options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} respondio {StatusCode} en {Elapsed:0.0} ms";
            options.IncludeQueryInRequestPath = false;
            options.GetLevel = (http, _, exception) =>
                exception is not null || http.Response.StatusCode >= StatusCodes.Status500InternalServerError
                    ? LogEventLevel.Error
                    : http.Request.Path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase)
                        ? LogEventLevel.Verbose
                        : LogEventLevel.Information;
        });

        return app;
    }
}

/// <summary>
/// Agrega a CADA evento de una peticion lo que permite reconstruir su historia: <c>TraceId</c>, <c>CorrelationId</c>,
/// <c>TenantId</c> y <c>UserId</c>. Se lee al escribir cada evento (y no una vez al entrar) porque el tenant y el
/// usuario solo existen despues de autenticar. Solo identificadores: nunca nombre, correo ni tokens.
/// </summary>
public sealed class RequestContextEnricher(IHttpContextAccessor accessor) : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory factory)
    {
        var context = accessor.HttpContext;
        if (context is null)
            return; // arranque o tarea programada: no hay peticion que correlacionar

        Add(logEvent, factory, "TraceId", Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier);

        if (context.Items.TryGetValue(CorrelationIdMiddleware.ItemsKey, out var correlation) && correlation is string id)
            Add(logEvent, factory, "CorrelationId", id);

        var user = context.RequestServices?.GetService<ICurrentUser>();
        if (user?.TenantId is { } tenantId)
            Add(logEvent, factory, "TenantId", tenantId);
        if (user?.UserId is { } userId)
            Add(logEvent, factory, "UserId", userId);
    }

    private static void Add(LogEvent logEvent, ILogEventPropertyFactory factory, string name, object value) =>
        logEvent.AddPropertyIfAbsent(factory.CreateProperty(name, value));
}
