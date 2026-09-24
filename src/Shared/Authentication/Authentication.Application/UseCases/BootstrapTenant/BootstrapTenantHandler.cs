using Authentication.Application.Dto;
using Authentication.Application.UseCases.BootstrapTenant.Responses;
using Authentication.Contracts.Events;
using Authentication.Domain;
using Authentication.Domain.Abstractions;
using Authentication.Domain.Entities;
using Authentication.Domain.Rbac;
using Authentication.Domain.Repositories;
using Common.Messaging;
using Shared.Kernel.Context;
using Shared.Kernel.Security;
using Tenancy.Contracts.Interfaces;

namespace Authentication.Application.UseCases.BootstrapTenant;

/// <summary>
/// Aprovisionamiento GATEADO del primer administrador de un tenant. Reemplaza al antiguo <c>POST /auth/register</c>,
/// que era anonimo y tomaba el <c>TenantId</c> y el <c>Role</c> del cuerpo: cualquiera se daba de alta como
/// Admin en la empresa que quisiera.
///
/// <list type="bullet">
///   <item>Sin <c>Bootstrap:Secret</c> configurado, esta apagado (403). Con secreto, se compara en tiempo constante (401 si no coincide).</item>
///   <item>Solo corre sobre un tenant SIN usuarios: no puede usarse para meter un segundo administrador.</item>
///   <item>Todo en una transaccion: tenant, roles del catalogo, credencial, rol Admin y perfil.</item>
/// </list>
/// Los usuarios siguientes los da de alta un administrador con una invitacion.
/// </summary>
internal sealed class BootstrapTenantHandler(
    BootstrapOptions options,
    ITenancyApi tenancy,
    IUserCredentialRepository credentials,
    IRbacRepository rbac,
    IPasswordHasher hasher,
    IMediator mediator,
    ITenantScope tenantScope,
    IUnitOfWork unitOfWork) : IRequestHandler<BootstrapTenantRequest, BootstrapTenantResponse>
{
    public async Task<BootstrapTenantResponse> Handle(BootstrapTenantRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(options.Secret))
            return new BootstrapTenantDisabledFailure("El bootstrap esta deshabilitado (configura Bootstrap:Secret).");
        if (!SecureTokens.FixedTimeEquals(request.Secret, options.Secret))
            return new BootstrapTenantInvalidSecretFailure("Secreto de bootstrap invalido.");

        var email = Identity.NormalizeEmail(request.AdminEmail);
        var fullName = request.AdminFullName?.Trim() ?? string.Empty;
        if (!Identity.IsValidEmail(email))
            return new BootstrapTenantValidationFailure("El correo del administrador no es valido.");
        if (fullName.Length is 0 or > 200)
            return new BootstrapTenantValidationFailure("El nombre del administrador es obligatorio y admite hasta 200 caracteres.");
        if (PasswordPolicy.Validate(request.AdminPassword) is { } weak)
            return new BootstrapTenantValidationFailure(weak);
        if (await credentials.EmailExistsAsync(email, cancellationToken))
            return new BootstrapTenantConflictFailure("Ya existe una cuenta con ese correo.");

        return await unitOfWork.ExecuteAsync<BootstrapTenantResponse>(async ct =>
        {
            // CreateTenantAsync valida nombre y slug (422) y la unicidad del slug (409).
            var tenant = await tenancy.GetTenantBySlugAsync(request.TenantSlug, ct)
                         ?? await tenancy.CreateTenantAsync(request.TenantName, request.TenantSlug, ct);

            if (await credentials.AnyInTenantAsync(tenant.Id, ct))
                return new BootstrapTenantConflictFailure("El tenant ya tiene usuarios; los siguientes los invita un administrador.");

            // Peticion anonima: sin esto, el perfil (tabla con RLS) se rechazaria por no tener tenant en contexto.
            using var _ = tenantScope.Enter(tenant.Id);

            await rbac.EnsureTenantProvisionedAsync(tenant.Id, ct);

            var credential = new UserCredential
            {
                TenantId = tenant.Id,
                Email = email,
                PasswordHash = hasher.Hash(request.AdminPassword),
                PasswordChangedAtUtc = DateTime.UtcNow,
            };
            credentials.Add(credential);
            await unitOfWork.SaveChangesAsync(ct);

            var admin = (await rbac.GetRolesByCodesAsync(tenant.Id, [RoleCodes.Admin], ct)).Single();
            rbac.AssignRole(tenant.Id, credential.Id, admin.Id);

            await mediator.Publish(new UserShouldBeCreatedIntegrationEvent(credential.PublicId, tenant.Id, fullName, email), ct);
            await unitOfWork.SaveChangesAsync(ct);

            return new BootstrapTenantSuccess(new BootstrapResultDto(tenant.PublicId, tenant.Id, tenant.Slug, credential.PublicId, email));
        }, cancellationToken);
    }
}
