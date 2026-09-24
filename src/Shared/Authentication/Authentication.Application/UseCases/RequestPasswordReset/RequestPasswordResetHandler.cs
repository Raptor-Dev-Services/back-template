using Authentication.Application.Dto;
using Authentication.Application.Sessions;
using Authentication.Application.UseCases.RequestPasswordReset.Responses;
using Authentication.Domain;
using Authentication.Domain.Entities;
using Authentication.Domain.Repositories;
using Common.Messaging;
using Microsoft.Extensions.Logging;
using Shared.Kernel.Context;

namespace Authentication.Application.UseCases.RequestPasswordReset;

internal sealed class RequestPasswordResetHandler(
    IUserCredentialRepository credentials,
    PasswordSetupMailer mailer,
    IUnitOfWork unitOfWork,
    ILogger<RequestPasswordResetHandler> logger) : IRequestHandler<RequestPasswordResetRequest, RequestPasswordResetResponse>
{
    public async Task<RequestPasswordResetResponse> Handle(RequestPasswordResetRequest request, CancellationToken cancellationToken)
    {
        var accepted = new RequestPasswordResetAccepted(new AcceptedDto());

        var email = Identity.NormalizeEmail(request.Email);
        var credential = email.Length == 0 ? null : await credentials.FindForSignInAsync(email, cancellationToken);

        // Una cuenta bloqueada no se desbloquea restableciendo la contrasena: no se le manda enlace.
        if (credential is null || !credential.CanSignIn)
            return accepted;

        try
        {
            await unitOfWork.ExecuteAsync(async ct =>
            {
                await mailer.SendAsync(credential, PasswordSetupPurpose.Reset, ct);
                return true;
            }, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // La respuesta tiene que ser la misma aunque el correo falle: un 500 SOLO para las cuentas que
            // existen las delataria. El fallo no se pierde: queda en el log como Error para operaciones.
            logger.LogError(ex, "No se pudo enviar el enlace de restablecimiento a la credencial {CredentialId}.", credential.Id);
        }

        return accepted;
    }
}
