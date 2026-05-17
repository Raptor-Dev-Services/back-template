using Application.Dto.Auth;
using Application.Services;
using Application.UseCases.Auth.RefreshToken.Responses;
using Common.Messaging;
using Domain.Repositories.Auth;
using Domain.Repositories.Users;

namespace Application.UseCases.Auth.RefreshToken;

public sealed class RefreshTokenHandler : IRequestHandler<RefreshTokenRequest, RefreshTokenResponse>
{
    private readonly IRefreshTokenRepository _refreshTokens;
    private readonly IUserRepository         _users;
    private readonly IJwtTokenService        _jwt;

    public RefreshTokenHandler(
        IRefreshTokenRepository refreshTokens,
        IUserRepository users,
        IJwtTokenService jwt)
    {
        _refreshTokens = refreshTokens;
        _users         = users;
        _jwt           = jwt;
    }

    public async Task<RefreshTokenResponse> Handle(RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        var existing = await _refreshTokens.GetByTokenAsync(request.Token, cancellationToken);

        if (existing is null || existing.IsRevoked || existing.ExpiresAtUtc <= DateTime.UtcNow)
            return new RefreshTokenInvalidFailure("Token de actualización inválido o expirado.");

        var user = await _users.GetByIdAsync(existing.UserId, cancellationToken);
        if (user is null || !user.IsActive)
            return new RefreshTokenInvalidFailure("Usuario no encontrado o inactivo.");

        await _refreshTokens.RevokeAsync(request.Token, cancellationToken);

        var newAccessToken  = _jwt.GenerateAccessToken(user.PublicId, user.Email, user.Role, user.TenantId, user.BranchId);
        var newRefreshToken = _jwt.GenerateRefreshToken();
        var expiry          = _jwt.GetRefreshTokenExpiry();

        await _refreshTokens.InsertAsync(user.Id, newRefreshToken, expiry, cancellationToken);

        return new RefreshTokenSuccess(new TokenDto(newAccessToken, newRefreshToken, expiry));
    }
}
