using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shared.Kernel.Email;

namespace Shared.Infrastructure.Email;

public static class EmailServiceCollectionEx
{
    /// <summary>
    /// Falla si el SMTP esta configurado A MEDIAS: con <c>Smtp:Host</c> se elige el envio real, y ese envio
    /// necesita remitente. Sin <c>Smtp:From</c> reventaria en cada correo. Configurado a medias es peor que no
    /// configurado. El Host la llama al arrancar para que salte ahi y no en el primer envio.
    /// </summary>
    public static void EnsureSmtpConfigurationIsComplete(IConfiguration configuration)
    {
        var section = configuration.GetSection(SmtpOptions.SectionName);
        if (!string.IsNullOrWhiteSpace(section["Host"]) && string.IsNullOrWhiteSpace(section["From"]))
            throw new InvalidOperationException(
                "Smtp:From es obligatorio cuando Smtp:Host esta configurado (Smtp__From). Sin remitente no sale ningun correo.");
    }

    /// <summary>
    /// Elige el emisor por configuracion y entorno:
    /// <list type="number">
    ///   <item>con <c>Smtp:Host</c> -> <see cref="SmtpEmailSender"/>, en cualquier entorno (en desarrollo, Mailpit);</item>
    ///   <item>sin SMTP en Development o Testing -> <see cref="LoggingEmailSender"/>;</item>
    ///   <item>sin SMTP en cualquier otro entorno -> <see cref="FailFastEmailSender"/>.</item>
    /// </list>
    /// </summary>
    public static IServiceCollection AddAppEmail(this IServiceCollection services, IConfiguration configuration)
    {
        EnsureSmtpConfigurationIsComplete(configuration);

        var smtp = configuration.GetSection(SmtpOptions.SectionName);
        var options = new SmtpOptions
        {
            Host = smtp["Host"] ?? string.Empty,
            Port = int.TryParse(smtp["Port"], out var port) ? port : 587,
            User = smtp["User"],
            Password = smtp["Password"],
            From = smtp["From"] ?? string.Empty,
            UseSsl = !bool.TryParse(smtp["UseSsl"], out var useSsl) || useSsl,
        };

        services.AddSingleton<IEmailSender>(provider =>
        {
            if (!string.IsNullOrWhiteSpace(options.Host))
                return new SmtpEmailSender(options, provider.GetRequiredService<ILogger<SmtpEmailSender>>());

            var environment = provider.GetRequiredService<IHostEnvironment>();
            return environment.IsDevelopment() || environment.IsEnvironment("Testing")
                ? new LoggingEmailSender(provider.GetRequiredService<ILogger<LoggingEmailSender>>())
                : new FailFastEmailSender(provider.GetRequiredService<ILogger<FailFastEmailSender>>());
        });

        var baseUrl = configuration[$"{WebOptions.SectionName}:BaseUrl"];
        services.AddSingleton(string.IsNullOrWhiteSpace(baseUrl) ? new WebOptions() : new WebOptions { BaseUrl = baseUrl.Trim() });

        return services;
    }
}
