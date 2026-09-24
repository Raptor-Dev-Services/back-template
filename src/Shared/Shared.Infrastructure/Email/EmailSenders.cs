using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Logging;
using Shared.Kernel.Email;

namespace Shared.Infrastructure.Email;

/// <summary>Configuracion SMTP (<c>Smtp__*</c>, siempre por entorno). La sola presencia de <see cref="Host"/> activa el envio real.</summary>
public sealed class SmtpOptions
{
    public const string SectionName = "Smtp";

    public string Host { get; set; } = string.Empty;

    /// <summary>587 (STARTTLS) es el default del envio autenticado; Mailpit en desarrollo escucha en 1025.</summary>
    public int Port { get; set; } = 587;

    public string? User { get; set; }

    /// <summary>Secreto: solo por entorno, nunca en el repo.</summary>
    public string? Password { get; set; }

    public string From { get; set; } = string.Empty;

    /// <summary>TLS en la conexion. Default true; Mailpit en desarrollo necesita false.</summary>
    public bool UseSsl { get; set; } = true;
}

/// <summary>
/// Envio REAL por SMTP (System.Net.Mail, parte del runtime). Solo registra metadatos (destinatario y asunto),
/// jamas el cuerpo. Un fallo de envio se propaga: tragarlo dejaria a alguien sin su enlace sin que nadie lo sepa.
/// </summary>
public sealed class SmtpEmailSender(SmtpOptions options, ILogger<SmtpEmailSender> logger) : IEmailSender
{
    public async Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken = default)
    {
        using var message = new MailMessage
        {
            From = new MailAddress(options.From),
            Subject = subject,
            Body = htmlBody,
            IsBodyHtml = true,
        };
        message.To.Add(toEmail);

        using var client = new SmtpClient(options.Host, options.Port)
        {
            EnableSsl = options.UseSsl,
            Credentials = string.IsNullOrWhiteSpace(options.User)
                ? null
                : new NetworkCredential(options.User, options.Password),
        };

        await client.SendMailAsync(message, cancellationToken);
        logger.LogInformation("Correo enviado por SMTP a {ToEmail}: {Subject}", toEmail, subject);
    }
}

/// <summary>
/// Adaptador de DESARROLLO: no envia nada, registra el correo a nivel <c>Debug</c> para poder seguir el flujo
/// (incluido el enlace con su token) sin proveedor. Solo se elige en Development y Testing sin SMTP: el cuerpo
/// lleva el token y en otro entorno seria una fuga.
/// </summary>
public sealed class LoggingEmailSender(ILogger<LoggingEmailSender> logger) : IEmailSender
{
    public Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Correo simulado a {ToEmail}: {Subject} | {HtmlBody}", toEmail, subject, htmlBody);
        return Task.CompletedTask;
    }
}

/// <summary>
/// Sin SMTP fuera de Development: falla FUERTE en cada envio (log Error + excepcion). Un despliegue sin correo
/// tiene que ser obvio el primer dia, no un alta de usuario que "funciona" y cuyo invitado nunca puede entrar.
/// </summary>
public sealed class FailFastEmailSender(ILogger<FailFastEmailSender> logger) : IEmailSender
{
    public Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken = default)
    {
        logger.LogError(
            "No hay proveedor de correo configurado (falta Smtp:Host): el correo a {ToEmail} ({Subject}) NO se envio.",
            toEmail, subject);
        throw new InvalidOperationException(
            "No hay proveedor de correo configurado. Define Smtp__Host, Smtp__From y sus credenciales.");
    }
}
