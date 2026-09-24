using Authentication.Application.Dto;
using Authentication.Application.UseCases.SetUserLock.Responses;
using Authentication.Domain.Entities;
using Authentication.Domain.Repositories;
using Common.Messaging;
using Shared.Kernel.Audit;
using Shared.Kernel.Context;

namespace Authentication.Application.UseCases.SetUserLock;

internal sealed class SetUserLockHandler(
    IUserCredentialRepository credentials,
    IRefreshTokenRepository refreshTokens,
    IAuditLog audit,
    IUnitOfWork unitOfWork) : IRequestHandler<SetUserLockRequest, SetUserLockResponse>
{
    public async Task<SetUserLockResponse> Handle(SetUserLockRequest request, CancellationToken cancellationToken)
    {
        // Un administrador que se bloquea a si mismo puede dejar al tenant sin nadie que lo desbloquee.
        if (request.ActorUserId == request.TargetUserId)
            return new SetUserLockValidationFailure("No puedes bloquear ni desbloquear tu propia cuenta.");

        // FindInTenant: un id de otro tenant no existe para este actor (404 identico al de un id inventado).
        var target = await credentials.FindInTenantAsync(request.TargetUserId, cancellationToken);
        if (target is null)
            return new SetUserLockNotFoundFailure("Usuario no encontrado.");

        return await unitOfWork.ExecuteAsync<SetUserLockResponse>(async ct =>
        {
            target.IsLocked = request.Locked;
            var revoked = request.Locked
                ? await refreshTokens.RevokeAllActiveAsync(target.Id, DateTime.UtcNow, RevocationReasons.Locked, ct)
                : 0;

            audit.Append(request.Locked ? "user.locked" : "user.unlocked", "UserCredential", target.PublicId.ToString(),
                request.Locked ? $"Cuenta bloqueada; {revoked} sesion(es) revocada(s)." : "Cuenta desbloqueada.");

            await unitOfWork.SaveChangesAsync(ct);
            return new SetUserLockSuccess(new AcceptedDto());
        }, cancellationToken);
    }
}
