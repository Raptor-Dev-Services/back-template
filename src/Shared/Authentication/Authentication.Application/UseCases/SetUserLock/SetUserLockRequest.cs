using Authentication.Application.UseCases.SetUserLock.Responses;
using Common.Messaging;

namespace Authentication.Application.UseCases.SetUserLock;

/// <summary>Bloquea o desbloquea a un usuario del tenant del actor. Bloquear revoca todas sus sesiones.</summary>
public sealed record SetUserLockRequest(Guid ActorUserId, Guid TargetUserId, bool Locked) : IRequest<SetUserLockResponse>;
