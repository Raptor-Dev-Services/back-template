using Authentication.Domain.Abstractions;
using Authentication.Application.UseCases.Login;
using Authentication.Application.UseCases.Login.Responses;
using Authentication.Domain.Entities;
using Authentication.Domain.Repositories;
using NSubstitute;
using Xunit;

namespace Authentication.Tests.UseCases;

public sealed class LoginHandlerTests
{
    private readonly IUserCredentialRepository _credentials   = Substitute.For<IUserCredentialRepository>();
    private readonly IRefreshTokenRepository   _refreshTokens = Substitute.For<IRefreshTokenRepository>();
    private readonly IPasswordHasher           _hasher        = Substitute.For<IPasswordHasher>();
    private readonly IJwtTokenService          _jwt           = Substitute.For<IJwtTokenService>();

    [Fact]
    public async Task Handle_WhenCredentialNotFound_ReturnsInvalidCredentialsFailure()
    {
        _credentials.GetForLoginAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((UserCredential?)null);

        var result = await new LoginHandler(_credentials, _refreshTokens, _hasher, _jwt)
            .Handle(new LoginRequest("user@test.com", "password"), default);

        Assert.IsType<LoginInvalidCredentialsFailure>(result);
    }

    [Fact]
    public async Task Handle_WhenPasswordInvalid_ReturnsInvalidCredentialsFailure()
    {
        var credential = new UserCredential
        {
            Id = 1, PublicId = Guid.NewGuid(), TenantId = 1,
            Email = "user@test.com", PasswordHash = "hash", Role = "User", IsActive = true
        };
        _credentials.GetForLoginAsync("user@test.com", Arg.Any<CancellationToken>()).Returns(credential);
        _hasher.Verify("wrong-password", "hash").Returns(false);

        var result = await new LoginHandler(_credentials, _refreshTokens, _hasher, _jwt)
            .Handle(new LoginRequest("user@test.com", "wrong-password"), default);

        Assert.IsType<LoginInvalidCredentialsFailure>(result);
    }

    [Fact]
    public async Task Handle_WhenValidCredentials_ReturnsLoginSuccess()
    {
        var credential = new UserCredential
        {
            Id = 1, PublicId = Guid.NewGuid(), TenantId = 1,
            Email = "user@test.com", PasswordHash = "hash", Role = "User", IsActive = true
        };
        _credentials.GetForLoginAsync("user@test.com", Arg.Any<CancellationToken>()).Returns(credential);
        _hasher.Verify("password", "hash").Returns(true);
        _jwt.GenerateAccessToken(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<long>())
            .Returns("access-token");
        _jwt.GenerateRefreshToken().Returns("refresh-token");
        _jwt.GetRefreshTokenExpiry().Returns(DateTime.UtcNow.AddDays(7));

        var result = await new LoginHandler(_credentials, _refreshTokens, _hasher, _jwt)
            .Handle(new LoginRequest("user@test.com", "password"), default);

        var success = Assert.IsType<LoginSuccess>(result);
        Assert.Equal("access-token", success.Data.AccessToken);
        Assert.Equal("refresh-token", success.Data.RefreshToken);
    }
}
