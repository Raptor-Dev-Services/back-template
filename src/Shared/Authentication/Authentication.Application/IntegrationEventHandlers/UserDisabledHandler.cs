using Authentication.Domain.Entities;
using Authentication.Domain.Repositories;
using Common.Messaging;
using Shared.Kernel.Context;
using Users.Contracts.Events;

namespace Authentication.Application.IntegrationEventHandlers;

/// <summary>La baja de un perfil apaga su credencial y cierra todas sus sesiones.</summary>
internal sealed class UserDisabledHandler(
    IUserCredentialRepository credentials,
    IRefreshTokenRepository refreshTokens,
    IUnitOfWork unitOfWork) : INotificationHandler<UserDisabledIntegrationEvent>
{
    public async Task Handle(UserDisabledIntegrationEvent notification, CancellationToken cancellationToken)
    {
        var credential = await credentials.FindInTenantAsync(notification.PublicId, cancellationToken);
        if (credential is null)
            return;

        credential.IsActive = false;
        await refreshTokens.RevokeAllActiveAsync(credential.Id, DateTime.UtcNow, RevocationReasons.Disabled, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
