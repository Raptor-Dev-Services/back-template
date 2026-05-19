using Common.Messaging;

namespace Users.Contracts.Events;

public sealed record UserRegisteredIntegrationEvent(Guid PublicId) : INotification;
