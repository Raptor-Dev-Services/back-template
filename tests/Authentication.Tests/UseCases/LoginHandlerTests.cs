using Authentication.Application.UseCases.Login;
using Authentication.Application.UseCases.Login.Responses;
using Authentication.Domain.Abstractions;
using Authentication.Domain.Entities;
using Authentication.Domain.Repositories;
using NSubstitute;
using Tenancy.Contracts.Dtos;
using Tenancy.Contracts.Interfaces;
using Xunit;

namespace Authentication.Tests.UseCases;

public sealed class LoginHandlerTests
{
    private readonly IUserCredentialRepository _credentials = Substitute.For<IUserCredentialRepository>();
    private readonly IRefreshTokenRepository _refreshTokens = Substitute.For<IRefreshTokenRepository>();
    private readonly IRbacRepository _rbac = Substitute.For<IRbacRepository>();
    private readonly IPasswordHasher _hasher = Substitute.For<IPasswordHasher>();
    private readonly ITenancyApi _tenancy = Substitute.For<ITenancyApi>();
    private readonly InlineUnitOfWork _unitOfWork = new();

    private LoginHandler Handler() =>
        new(_credentials, _hasher, _tenancy, TestSessions.Create(_rbac, _refreshTokens), _unitOfWork);

    private UserCredential Credential(bool locked = false, bool active = true)
    {
        var credential = new UserCredential { Id = 7, TenantId = 3, Email = "ana@example.test", PasswordHash = "hash", IsLocked = locked, IsActive = active };
        _credentials.FindForSignInAsync("ana@example.test", Arg.Any<CancellationToken>()).Returns(credential);
        _tenancy.GetTenantByIdAsync(3, Arg.Any<CancellationToken>()).Returns(new TenantDto(3, Guid.NewGuid(), "E", "e", IsActive: true));
        return credential;
    }

    [Fact]
    public async Task Un_correo_inexistente_verifica_igual_contra_un_hash_ficticio()
    {
        var result = await Handler().Handle(new LoginRequest("nadie@example.test", "x", null), default);

        Assert.IsType<LoginInvalidCredentialsFailure>(result);
        _hasher.Received(1).Verify("x", null); // el mismo trabajo que con una cuenta real: sin atajo de tiempo
    }

    [Fact]
    public async Task Una_cuenta_bloqueada_con_contrasena_mala_no_revela_que_esta_bloqueada()
    {
        Credential(locked: true);
        _hasher.Verify("mala", "hash").Returns(false);

        var result = await Handler().Handle(new LoginRequest("ana@example.test", "mala", null), default);

        Assert.IsType<LoginInvalidCredentialsFailure>(result);
    }

    [Fact]
    public async Task Una_cuenta_bloqueada_con_la_contrasena_correcta_es_403_y_no_emite_sesion()
    {
        Credential(locked: true);
        _hasher.Verify("buena", "hash").Returns(true);

        var result = await Handler().Handle(new LoginRequest("ana@example.test", "buena", null), default);

        Assert.IsType<LoginForbiddenFailure>(result);
        _refreshTokens.DidNotReceive().Add(Arg.Any<RefreshToken>());
    }

    [Fact]
    public async Task Una_cuenta_dada_de_baja_responde_como_credenciales_invalidas()
    {
        Credential(active: false);
        _hasher.Verify("buena", "hash").Returns(true);

        var result = await Handler().Handle(new LoginRequest("ana@example.test", "buena", null), default);

        Assert.IsType<LoginInvalidCredentialsFailure>(result);
    }

    [Fact]
    public async Task Un_tenant_suspendido_no_inicia_sesion()
    {
        Credential();
        _hasher.Verify("buena", "hash").Returns(true);
        _tenancy.GetTenantByIdAsync(3, Arg.Any<CancellationToken>()).Returns(new TenantDto(3, Guid.NewGuid(), "E", "e", IsActive: false));

        var result = await Handler().Handle(new LoginRequest("ana@example.test", "buena", null), default);

        Assert.IsType<LoginForbiddenFailure>(result);
    }

    [Fact]
    public async Task El_login_valido_normaliza_el_correo_y_guarda_solo_el_hash_del_refresh()
    {
        var credential = Credential();
        _hasher.Verify("buena", "hash").Returns(true);
        RefreshToken? stored = null;
        _refreshTokens.When(r => r.Add(Arg.Any<RefreshToken>())).Do(c => stored = c.Arg<RefreshToken>());

        var result = await Handler().Handle(new LoginRequest("  ANA@example.test ", "buena", "10.0.0.1"), default);

        var success = Assert.IsType<LoginSuccess>(result);
        Assert.NotNull(stored);
        Assert.NotEqual(success.Data.RefreshToken, stored!.TokenHash);
        Assert.Equal(64, stored.TokenHash.Length);
        Assert.Equal(credential.TenantId, stored.TenantId);
        Assert.Equal("10.0.0.1", stored.CreatedByIp);
        Assert.NotNull(credential.LastLoginAtUtc);
        Assert.Equal(1, _unitOfWork.Saves);
    }
}
