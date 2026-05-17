using Application.Dto.Auth;
using Application.Services;
using Application.UseCases.Auth.Login.Responses;
using Common.Messaging;
using Domain.Repositories.Auth;
using Domain.Repositories.Users;

namespace Application.UseCases.Auth.Login;

public sealed class LoginHandler : IRequestHandler<LoginRequest, LoginResponse>
{
    private readonly IUserRepository         _users;
    private readonly IRefreshTokenRepository _refreshTokens;
    private readonly IPasswordHasher         _hasher;
    private readonly IJwtTokenService        _jwt;

    public LoginHandler(
        IUserRepository users,
        IRefreshTokenRepository refreshTokens,
        IPasswordHasher hasher,
        IJwtTokenService jwt)
    {
        _users         = users;
        _refreshTokens = refreshTokens;
        _hasher        = hasher;
        _jwt           = jwt;
    }

    public async Task<LoginResponse> Handle(LoginRequest request, CancellationToken cancellationToken)
    {
        var user = await _users.GetForLoginAsync(request.Email, cancellationToken);

        if (user is null || !_hasher.Verify(request.Password, user.PasswordHash) || !user.IsActive)
            return new LoginInvalidCredentialsFailure("Credenciales inválidas.");

        var accessToken  = _jwt.GenerateAccessToken(user.PublicId, user.Email, user.Role, user.TenantId, user.BranchId);
        var refreshToken = _jwt.GenerateRefreshToken();
        var expiry       = _jwt.GetRefreshTokenExpiry();

        await _refreshTokens.InsertAsync(user.Id, refreshToken, expiry, cancellationToken);

        return new LoginSuccess(new TokenDto(accessToken, refreshToken, expiry));
    }
}
