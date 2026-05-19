using Common.Messaging;

namespace Authentication.Contracts.Events;

public sealed record UserShouldBeCreatedIntegrationEvent(
    Guid   PublicId,
    long   TenantId,
    long   BranchId,
    string FullName,
    string Email,
    string Role) : INotification;
