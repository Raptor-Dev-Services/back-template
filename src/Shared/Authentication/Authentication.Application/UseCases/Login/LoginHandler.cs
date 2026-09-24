using Authentication.Application.Dto;
using Authentication.Domain.Abstractions;
using Authentication.Application.UseCases.Login.Responses;
using Authentication.Domain.Repositories;
using Common.Messaging;

namespace Authentication.Application.UseCases.Login;

public sealed class LoginHandler : IRequestHandler<LoginRequest, LoginResponse>
{
    private readonly IUserCredentialRepository _credentials;
    private readonly IRefreshTokenRepository   _refreshTokens;
    private readonly IPasswordHasher           _hasher;
    private readonly IJwtTokenService          _jwt;

    public LoginHandler(
        IUserCredentialRepository credentials,
        IRefreshTokenRepository refreshTokens,
        IPasswordHasher hasher,
        IJwtTokenService jwt)
    {
        _credentials   = credentials;
        _refreshTokens = refreshTokens;
        _hasher        = hasher;
        _jwt           = jwt;
    }

    public async Task<LoginResponse> Handle(LoginRequest request, CancellationToken cancellationToken)
    {
        var credential = await _credentials.GetForLoginAsync(request.Email, cancellationToken);

        if (credential is null || !_hasher.Verify(request.Password, credential.PasswordHash) || !credential.IsActive)
            return new LoginInvalidCredentialsFailure("Credenciales inválidas.");

        var accessToken  = _jwt.GenerateAccessToken(credential.PublicId, credential.Email, credential.Role, credential.TenantId);
        var refreshToken = _jwt.GenerateRefreshToken();
        var expiry       = _jwt.GetRefreshTokenExpiry();

        await _refreshTokens.InsertAsync(credential.Id, refreshToken, expiry, cancellationToken);

        return new LoginSuccess(new TokenDto(accessToken, refreshToken, expiry));
    }
}
