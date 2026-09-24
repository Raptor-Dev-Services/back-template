using Authentication.Application.Dto;
using Authentication.Domain.Abstractions;
using Authentication.Application.UseCases.RefreshToken.Responses;
using Authentication.Domain.Repositories;
using Common.Messaging;

namespace Authentication.Application.UseCases.RefreshToken;

public sealed class RefreshTokenHandler : IRequestHandler<RefreshTokenRequest, RefreshTokenResponse>
{
    private readonly IRefreshTokenRepository   _refreshTokens;
    private readonly IUserCredentialRepository _credentials;
    private readonly IJwtTokenService          _jwt;

    public RefreshTokenHandler(
        IRefreshTokenRepository refreshTokens,
        IUserCredentialRepository credentials,
        IJwtTokenService jwt)
    {
        _refreshTokens = refreshTokens;
        _credentials   = credentials;
        _jwt           = jwt;
    }

    public async Task<RefreshTokenResponse> Handle(RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        var existing = await _refreshTokens.GetByTokenAsync(request.Token, cancellationToken);

        if (existing is null || existing.IsRevoked || existing.ExpiresAtUtc <= DateTime.UtcNow)
            return new RefreshTokenInvalidFailure("Token de actualización inválido o expirado.");

        var credential = await _credentials.GetByIdAsync(existing.CredentialId, cancellationToken);
        if (credential is null || !credential.IsActive)
            return new RefreshTokenInvalidFailure("Usuario no encontrado o inactivo.");

        await _refreshTokens.RevokeAsync(request.Token, cancellationToken);

        var newAccessToken  = _jwt.GenerateAccessToken(credential.PublicId, credential.Email, credential.Role, credential.TenantId);
        var newRefreshToken = _jwt.GenerateRefreshToken();
        var expiry          = _jwt.GetRefreshTokenExpiry();

        await _refreshTokens.InsertAsync(credential.Id, newRefreshToken, expiry, cancellationToken);

        return new RefreshTokenSuccess(new TokenDto(newAccessToken, newRefreshToken, expiry));
    }
}
