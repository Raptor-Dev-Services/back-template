using Authentication.Application.UseCases.RefreshSession;
using Authentication.Application.UseCases.RefreshSession.Responses;
using Authentication.Domain.Entities;
using Authentication.Domain.Repositories;
using NSubstitute;
using Shared.Kernel.Security;
using Tenancy.Contracts.Dtos;
using Tenancy.Contracts.Interfaces;
using Xunit;

namespace Authentication.Tests.UseCases;

public sealed class RefreshSessionHandlerTests
{
    private readonly IRefreshTokenRepository _refreshTokens = Substitute.For<IRefreshTokenRepository>();
    private readonly IUserCredentialRepository _credentials = Substitute.For<IUserCredentialRepository>();
    private readonly IRbacRepository _rbac = Substitute.For<IRbacRepository>();
    private readonly ITenancyApi _tenancy = Substitute.For<ITenancyApi>();
    private readonly InlineUnitOfWork _unitOfWork = new();

    private RefreshSessionHandler Handler() =>
        new(_refreshTokens, _credentials, _tenancy, TestSessions.Create(_rbac, _refreshTokens), _unitOfWork);

    private RefreshToken Stored(string raw, DateTime? revokedAt = null, string? reason = null, DateTime? expires = null)
    {
        var token = new RefreshToken
        {
            TenantId = 3,
            CredentialId = 7,
            TokenHash = SecureTokens.Hash(raw),
            ExpiresAtUtc = expires ?? DateTime.UtcNow.AddDays(1),
            RevokedAtUtc = revokedAt,
            RevokedReason = reason,
        };
        _refreshTokens.FindByHashAsync(token.TokenHash, Arg.Any<CancellationToken>()).Returns(token);
        return token;
    }

    [Fact]
    public async Task Presentar_un_token_ya_rotado_revoca_todas_las_sesiones_del_usuario()
    {
        Stored("viejo", revokedAt: DateTime.UtcNow.AddMinutes(-1), reason: RevocationReasons.Rotated);

        var result = await Handler().Handle(new RefreshSessionRequest("viejo", null), default);

        var failure = Assert.IsType<RefreshSessionInvalidFailure>(result);
        Assert.Equal(RefreshSessionHandler.ReuseDetected, failure.Message);
        await _refreshTokens.Received(1).RevokeAllActiveAsync(7, Arg.Any<DateTime>(), RevocationReasons.Reuse, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Un_token_cerrado_por_logout_es_invalido_sin_castigar_las_demas_sesiones()
    {
        Stored("cerrado", revokedAt: DateTime.UtcNow.AddMinutes(-1), reason: RevocationReasons.Logout);

        var result = await Handler().Handle(new RefreshSessionRequest("cerrado", null), default);

        Assert.IsType<RefreshSessionInvalidFailure>(result);
        await _refreshTokens.DidNotReceive().RevokeAllActiveAsync(Arg.Any<long>(), Arg.Any<DateTime>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Un_token_vencido_es_invalido()
    {
        Stored("vencido", expires: DateTime.UtcNow.AddSeconds(-1));

        Assert.IsType<RefreshSessionInvalidFailure>(await Handler().Handle(new RefreshSessionRequest("vencido", null), default));
    }

    [Fact]
    public async Task Una_cuenta_bloqueada_no_renueva_y_su_token_queda_revocado()
    {
        var token = Stored("vivo");
        _credentials.FindForSignInAsync(7L, Arg.Any<CancellationToken>()).Returns(new UserCredential { Id = 7, TenantId = 3, IsLocked = true });

        var result = await Handler().Handle(new RefreshSessionRequest("vivo", null), default);

        Assert.IsType<RefreshSessionInvalidFailure>(result);
        Assert.Equal(RevocationReasons.Locked, token.RevokedReason);
    }

    [Fact]
    public async Task Rotar_revoca_el_presentado_y_lo_encadena_al_nuevo()
    {
        var token = Stored("vivo");
        _credentials.FindForSignInAsync(7L, Arg.Any<CancellationToken>()).Returns(new UserCredential { Id = 7, TenantId = 3, Email = "a@b.test" });
        _tenancy.GetTenantByIdAsync(3, Arg.Any<CancellationToken>()).Returns(new TenantDto(3, Guid.NewGuid(), "E", "e", true));

        var result = await Handler().Handle(new RefreshSessionRequest("vivo", null), default);

        var success = Assert.IsType<RefreshSessionSuccess>(result);
        Assert.Equal(RevocationReasons.Rotated, token.RevokedReason);
        Assert.Equal(SecureTokens.Hash(success.Data.RefreshToken), token.ReplacedByTokenHash);
    }
}
