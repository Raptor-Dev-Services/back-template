using Authentication.Application.UseCases.InviteUser.Responses;
using Common.Messaging;

namespace Authentication.Application.UseCases.InviteUser;

/// <summary>
/// Un administrador da de alta a un usuario de SU tenant. El tenant y el actor salen del JWT (controller); el
/// invitado recibe un enlace para fijar su contrasena. Nadie elige ni ve la contrasena de otro.
/// </summary>
public sealed record InviteUserRequest(
    long TenantId,
    Guid ActorUserId,
    string Email,
    string FullName,
    IReadOnlyCollection<string>? RoleCodes) : IRequest<InviteUserResponse>;
