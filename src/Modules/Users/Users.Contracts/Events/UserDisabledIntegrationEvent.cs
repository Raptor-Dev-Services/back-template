using Common.Messaging;

namespace Users.Contracts.Events;

/// <summary>
/// Un administrador dio de baja el perfil de un usuario. Authentication lo escucha para desactivar su
/// credencial y revocar sus sesiones: un usuario dado de baja no debe seguir entrando con el refresh token que
/// ya tenia. Se publica dentro de la transaccion de la baja, asi que o se aplican las dos cosas o ninguna.
/// </summary>
public sealed record UserDisabledIntegrationEvent(Guid PublicId) : INotification;
