using Authentication.Application;
using Authentication.Application.Sessions;
using Authentication.Application.UseCases.BootstrapTenant;
using Authentication.Application.UseCases.BootstrapTenant.Responses;
using Authentication.Application.UseCases.InviteUser;
using Authentication.Application.UseCases.InviteUser.Responses;
using Authentication.Domain.Abstractions;
using Authentication.Domain.Entities;
using Authentication.Domain.Rbac;
using Authentication.Domain.Repositories;
using Common.Messaging;
using NSubstitute;
using Shared.Kernel.Audit;
using Shared.Kernel.Context;
using Shared.Kernel.Email;
using Tenancy.Contracts.Interfaces;
using Xunit;

namespace Authentication.Tests.UseCases;

public sealed class ProvisioningHandlerTests
{
    private readonly ITenancyApi _tenancy = Substitute.For<ITenancyApi>();
    private readonly IUserCredentialRepository _credentials = Substitute.For<IUserCredentialRepository>();
    private readonly IRbacRepository _rbac = Substitute.For<IRbacRepository>();
    private readonly IPasswordHasher _hasher = Substitute.For<IPasswordHasher>();
    private readonly IMediator _mediator = Substitute.For<IMediator>();
    private readonly ITenantScope _scope = Substitute.For<ITenantScope>();
    private readonly InlineUnitOfWork _unitOfWork = new();

    private BootstrapTenantHandler Bootstrap(string? configuredSecret) =>
        new(new BootstrapOptions { Secret = configuredSecret }, _tenancy, _credentials, _rbac, _hasher, _mediator, _scope, Substitute.For<IAuditLog>(), _unitOfWork);

    private static BootstrapTenantRequest Request(string? secret) =>
        new(secret, "Empresa", "empresa", "admin@example.test", "Contrasena-Segura-123", "Admin");

    [Fact]
    public async Task Sin_secreto_configurado_el_bootstrap_esta_apagado_y_no_toca_la_base()
    {
        var result = await Bootstrap(configuredSecret: null).Handle(Request("lo-que-sea"), default);

        Assert.IsType<BootstrapTenantDisabledFailure>(result);
        await _tenancy.DidNotReceiveWithAnyArgs().CreateTenantAsync(default!, default!, default);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("secreto-casi-correcto-0123456789abcdef-0123456789")]
    public async Task Un_secreto_que_no_coincide_se_rechaza(string? provided)
    {
        var result = await Bootstrap("secreto-correcto-0123456789abcdef-0123456789").Handle(Request(provided), default);

        Assert.IsType<BootstrapTenantInvalidSecretFailure>(result);
        await _credentials.DidNotReceiveWithAnyArgs().EmailExistsAsync(default!, default);
    }

    [Fact]
    public async Task Una_contrasena_debil_no_crea_nada()
    {
        const string secret = "secreto-correcto-0123456789abcdef-0123456789";
        var result = await Bootstrap(secret).Handle(Request(secret) with { AdminPassword = "corta" }, default);

        Assert.IsType<BootstrapTenantValidationFailure>(result);
    }

    [Fact]
    public async Task Solo_un_administrador_concede_el_rol_de_administrador()
    {
        var actorId = Guid.NewGuid();
        _credentials.FindInTenantAsync(actorId, Arg.Any<CancellationToken>()).Returns(new UserCredential { Id = 1, TenantId = 3 });
        _rbac.GetGrantsAsync(3, 1, Arg.Any<CancellationToken>()).Returns(((IReadOnlyList<string>)[RoleCodes.Member], (IReadOnlyList<string>)["users.manage"]));
        _rbac.GetRolesByCodesAsync(3, Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
            .Returns([new Role { Id = 10, TenantId = 3, Code = RoleCodes.Admin }]);

        var mailer = new PasswordSetupMailer(Substitute.For<IPasswordSetupTokenRepository>(), Substitute.For<IEmailSender>(),
            new WebOptions(), TestSessions.Options, _unitOfWork);
        var handler = new InviteUserHandler(_credentials, _rbac, _hasher, mailer, _mediator, Substitute.For<IAuditLog>(), _unitOfWork);

        var result = await handler.Handle(new InviteUserRequest(3, actorId, "nuevo@example.test", "Nuevo", [RoleCodes.Admin]), default);

        Assert.IsType<InviteUserForbiddenFailure>(result);
        _credentials.DidNotReceive().Add(Arg.Any<UserCredential>());
    }
}
