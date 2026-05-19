using Authentication.Contracts.Events;
using Common.Messaging;
using Tenancy.Contracts.Interfaces;
using Users.Contracts.Events;
using Users.Domain.Repositories;

namespace Users.Application.IntegrationEventHandlers;

public sealed class UserShouldBeCreatedHandler : INotificationHandler<UserShouldBeCreatedIntegrationEvent>
{
    private readonly IUserProfileRepository _profiles;
    private readonly ITenancyApi            _tenancy;
    private readonly IMediator              _mediator;

    public UserShouldBeCreatedHandler(
        IUserProfileRepository profiles,
        ITenancyApi tenancy,
        IMediator mediator)
    {
        _profiles = profiles;
        _tenancy  = tenancy;
        _mediator = mediator;
    }

    public async Task Handle(UserShouldBeCreatedIntegrationEvent notification, CancellationToken cancellationToken)
    {
        var tenant = await _tenancy.GetTenantByIdAsync(notification.TenantId, cancellationToken);
        if (tenant is null || !tenant.IsActive)
            return;

        await _profiles.InsertAsync(
            notification.PublicId,
            notification.TenantId,
            notification.BranchId,
            notification.FullName,
            cancellationToken);

        await _mediator.Publish(new UserRegisteredIntegrationEvent(notification.PublicId), cancellationToken);
    }
}
