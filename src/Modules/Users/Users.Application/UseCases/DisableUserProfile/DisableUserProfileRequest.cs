using Common.Messaging;
using Users.Application.UseCases.DisableUserProfile.Responses;

namespace Users.Application.UseCases.DisableUserProfile;

/// <summary>El actor sale del JWT: nadie se da de baja a si mismo por accidente (ni deja al tenant sin administrador).</summary>
public sealed record DisableUserProfileRequest(Guid ActorUserId, Guid PublicId) : IRequest<DisableUserProfileResponse>;
