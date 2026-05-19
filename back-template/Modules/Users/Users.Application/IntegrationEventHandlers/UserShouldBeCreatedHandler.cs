using Authentication.Contracts.Events;
using Common.Messaging;
using Users.Contracts.Events;
using Users.Domain.Repositories;

namespace Users.Application.IntegrationEventHandlers;

public sealed class UserShouldBeCreatedHandler : INotificationHandler<UserShouldBeCreatedIntegrationEvent>
{
    private readonly IUserProfileRepository _profiles;
    private readonly IMediator              _mediator;

    public UserShouldBeCreatedHandler(IUserProfileRepository profiles, IMediator mediator)
    {
        _profiles = profiles;
        _mediator = mediator;
    }

    public async Task Handle(UserShouldBeCreatedIntegrationEvent notification, CancellationToken cancellationToken)
    {
        await _profiles.InsertAsync(
            notification.PublicId,
            notification.TenantId,
            notification.FullName,
            cancellationToken);

        await _mediator.Publish(new UserRegisteredIntegrationEvent(notification.PublicId), cancellationToken);
    }
}
