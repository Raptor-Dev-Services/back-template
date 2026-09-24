using System.Net;
using Authentication.Domain.Abstractions;
using Authentication.Domain.Entities;
using Authentication.Domain.Repositories;
using Shared.Kernel.Context;
using Shared.Kernel.Email;
using Shared.Kernel.Security;

namespace Authentication.Application.Sessions;

/// <summary>
/// Emite un enlace de un solo uso para fijar la contrasena y lo manda por correo. Lo comparten la invitacion
/// (alta por un administrador) y el restablecimiento: una sola fuente de verdad del token y del correo.
///
/// <para>Invalida los enlaces vivos anteriores de la credencial (solo uno vigente a la vez), persiste SOLO el
/// hash y manda el valor en claro unicamente en el correo.</para>
///
/// <para>Guarda ANTES de enviar y debe correr dentro de una transaccion del caso de uso: si el envio falla, la
/// transaccion se revierte y no queda un token que nadie recibio; si el guardado fallara, no sale un correo con
/// un enlace muerto.</para>
/// </summary>
internal sealed class PasswordSetupMailer(
    IPasswordSetupTokenRepository setupTokens,
    IEmailSender email,
    WebOptions web,
    AuthOptions options,
    IUnitOfWork unitOfWork)
{
    public async Task SendAsync(UserCredential credential, PasswordSetupPurpose purpose, CancellationToken cancellationToken)
    {
        var nowUtc = DateTime.UtcNow;
        await setupTokens.InvalidateActiveAsync(credential.Id, nowUtc, cancellationToken);

        var raw = SecureTokens.Generate();
        var lifetime = purpose == PasswordSetupPurpose.Invitation
            ? TimeSpan.FromHours(options.InvitationHours)
            : TimeSpan.FromMinutes(options.PasswordResetMinutes);

        setupTokens.Add(new PasswordSetupToken
        {
            TenantId = credential.TenantId,
            CredentialId = credential.Id,
            TokenHash = SecureTokens.Hash(raw),
            Purpose = purpose,
            ExpiresAtUtc = nowUtc.Add(lifetime),
        });
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var link = WebUtility.HtmlEncode(web.Link("/set-password", "token", raw));
        var (subject, intro) = purpose == PasswordSetupPurpose.Invitation
            ? ("Te invitaron a la plataforma", "Te dieron de alta. Usa este enlace para elegir tu contrasena")
            : ("Restablece tu contrasena", "Recibimos una solicitud para restablecer tu contrasena. Usa este enlace");

        var body =
            $"<p>Hola,</p><p>{intro} (vigencia {FormatLifetime(lifetime)}):</p>" +
            $"<p><a href=\"{link}\">{link}</a></p>" +
            "<p>Si no esperabas este correo, ignoralo: tu cuenta no cambia.</p>";

        await email.SendAsync(credential.Email, subject, body, cancellationToken);
    }

    private static string FormatLifetime(TimeSpan lifetime) =>
        lifetime.TotalHours >= 1 ? $"{lifetime.TotalHours:0} horas" : $"{lifetime.TotalMinutes:0} minutos";
}
