using Common.Messaging;

namespace Authentication.Contracts.Events;

/// <summary>
/// Authentication dio de alta una credencial y el modulo Users debe crear su perfil. El tenant viaja
/// EXPLICITO: el alta puede ocurrir sin tenant en la peticion (el bootstrap).
/// </summary>
public sealed record UserShouldBeCreatedIntegrationEvent(
    Guid PublicId,
    long TenantId,
    string FullName,
    string Email) : INotification;
