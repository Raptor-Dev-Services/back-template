using Authentication.Application.Dto;
using Authentication.Domain.Abstractions;
using Authentication.Domain.Entities;
using Authentication.Domain.Repositories;
using Shared.Kernel.Security;

namespace Authentication.Application.Sessions;

/// <summary>
/// Emite una sesion (access JWT + refresh token) para una credencial ya autenticada. Lo comparten el login y la
/// renovacion para que los dos emitan EXACTAMENTE lo mismo: los roles y permisos se leen de la base en cada
/// emision, asi que un permiso retirado deja de viajar en el siguiente refresh.
///
/// Agrega el refresh token al contexto pero NO guarda: el caso de uso decide la transaccion.
/// </summary>
internal sealed class SessionIssuer(
    IRbacRepository rbac,
    IRefreshTokenRepository refreshTokens,
    IAccessTokenIssuer accessTokens,
    AuthOptions options)
{
    public async Task<(AuthTokensDto Tokens, string RefreshTokenHash)> IssueAsync(
        UserCredential credential, string? clientIp, CancellationToken cancellationToken)
    {
        var (roles, permissions) = await rbac.GetGrantsAsync(credential.TenantId, credential.Id, cancellationToken);
        var access = accessTokens.Issue(new AccessTokenSubject(credential.PublicId, credential.TenantId, credential.Email, roles, permissions));

        var rawRefresh = SecureTokens.Generate();
        var refreshHash = SecureTokens.Hash(rawRefresh);
        var refreshExpires = DateTime.UtcNow.AddDays(options.RefreshTokenDays);

        refreshTokens.Add(new RefreshToken
        {
            TenantId = credential.TenantId,
            CredentialId = credential.Id,
            TokenHash = refreshHash,
            ExpiresAtUtc = refreshExpires,
            CreatedByIp = clientIp,
        });

        return (new AuthTokensDto(access.Value, access.ExpiresAtUtc, rawRefresh, refreshExpires), refreshHash);
    }
}
