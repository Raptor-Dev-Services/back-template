using Authentication.Application.Sessions;
using Authentication.Application.UseCases.RefreshSession.Responses;
using Authentication.Domain.Entities;
using Authentication.Domain.Repositories;
using Common.Messaging;
using Shared.Kernel.Context;
using Shared.Kernel.Security;
using Tenancy.Contracts.Interfaces;

namespace Authentication.Application.UseCases.RefreshSession;

/// <summary>
/// Rotacion del refresh token: cada renovacion revoca el token presentado, lo encadena al nuevo y emite un
/// access token con los roles y permisos ACTUALES. Todo en una transaccion.
///
/// <para><b>Deteccion de reuso (RFC 6819).</b> Un token ya rotado solo vuelve a presentarse si alguien lo copio:
/// el titular legitimo ya tiene el nuevo. En ese caso se revocan TODAS las sesiones del usuario -expulsa al que
/// lo robo- y el legitimo vuelve a entrar con su contrasena, que es lo unico que el atacante no tiene.</para>
/// </summary>
internal sealed class RefreshSessionHandler(
    IRefreshTokenRepository refreshTokens,
    IUserCredentialRepository credentials,
    ITenancyApi tenancy,
    SessionIssuer sessions,
    IUnitOfWork unitOfWork) : IRequestHandler<RefreshSessionRequest, RefreshSessionResponse>
{
    public const string Invalid = "La sesion no es valida o expiro. Inicia sesion de nuevo.";

    public const string ReuseDetected =
        "Se detecto el reuso de una sesion ya renovada; por seguridad se cerraron todas tus sesiones.";

    public async Task<RefreshSessionResponse> Handle(RefreshSessionRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
            return new RefreshSessionInvalidFailure(Invalid);

        var nowUtc = DateTime.UtcNow;
        var existing = await refreshTokens.FindByHashAsync(SecureTokens.Hash(request.RefreshToken), cancellationToken);
        if (existing is null)
            return new RefreshSessionInvalidFailure(Invalid);

        if (existing.RevokedAtUtc is not null)
        {
            if (existing.RevokedReason != RevocationReasons.Rotated)
                return new RefreshSessionInvalidFailure(Invalid);

            await refreshTokens.RevokeAllActiveAsync(existing.CredentialId, nowUtc, RevocationReasons.Reuse, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return new RefreshSessionInvalidFailure(ReuseDetected);
        }

        if (!existing.IsActive(nowUtc))
            return new RefreshSessionInvalidFailure(Invalid);

        var credential = await credentials.FindForSignInAsync(existing.CredentialId, cancellationToken);
        if (credential is null || !credential.CanSignIn)
        {
            existing.Revoke(nowUtc, credential?.IsLocked == true ? RevocationReasons.Locked : RevocationReasons.Disabled);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return new RefreshSessionInvalidFailure(Invalid);
        }

        var tenant = await tenancy.GetTenantByIdAsync(credential.TenantId, cancellationToken);
        if (tenant is null || !tenant.IsActive)
            return new RefreshSessionForbiddenFailure("La cuenta de tu empresa esta suspendida.");

        return await unitOfWork.ExecuteAsync<RefreshSessionResponse>(async ct =>
        {
            var (tokens, newHash) = await sessions.IssueAsync(credential, request.ClientIp, ct);
            existing.Revoke(nowUtc, RevocationReasons.Rotated, newHash);

            // Dos renovaciones simultaneas del MISMO token: la segunda pierde la carrera en xmin y responde 409
            // en vez de abrir una segunda cadena de sesiones.
            await unitOfWork.SaveChangesAsync(ct);
            return new RefreshSessionSuccess(tokens);
        }, cancellationToken);
    }
}
