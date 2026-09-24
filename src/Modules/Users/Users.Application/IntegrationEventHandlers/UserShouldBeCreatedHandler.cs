using Authentication.Contracts.Events;
using Common.Messaging;
using Users.Contracts.Events;
using Users.Domain.Entities;
using Users.Domain.Repositories;

namespace Users.Application.IntegrationEventHandlers;

/// <summary>
/// Crea el perfil cuando Authentication da de alta una credencial. El tenant viaja EXPLICITO en el evento:
/// el alta puede ocurrir sin contexto de tenant en la peticion (el bootstrap del primer administrador).
/// </summary>
internal sealed class UserShouldBeCreatedHandler(IUserProfileRepository profiles, IMediator mediator)
    : INotificationHandler<UserShouldBeCreatedIntegrationEvent>
{
    public async Task Handle(UserShouldBeCreatedIntegrationEvent notification, CancellationToken cancellationToken)
    {
        await profiles.AddAsync(new UserProfile
        {
            PublicId = notification.PublicId,
            TenantId = notification.TenantId,
            FullName = notification.FullName,
        }, cancellationToken);

        await mediator.Publish(new UserRegisteredIntegrationEvent(notification.PublicId), cancellationToken);
    }
}
