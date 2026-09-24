using Authentication.Application.Dto;
using Authentication.Application.Sessions;
using Authentication.Application.UseCases.InviteUser.Responses;
using Authentication.Contracts.Events;
using Authentication.Domain;
using Authentication.Domain.Abstractions;
using Authentication.Domain.Entities;
using Authentication.Domain.Rbac;
using Authentication.Domain.Repositories;
using Common.Messaging;
using Shared.Kernel.Context;
using Shared.Kernel.Security;

namespace Authentication.Application.UseCases.InviteUser;

/// <summary>
/// Alta de un usuario por un administrador: credencial SIN contrasena utilizable (el hash de un secreto
/// aleatorio que nadie conoce), roles pedidos, perfil en Users y un enlace de invitacion por correo. Todo en una
/// transaccion; si el correo no sale, no queda un usuario a medias.
/// </summary>
internal sealed class InviteUserHandler(
    IUserCredentialRepository credentials,
    IRbacRepository rbac,
    IPasswordHasher hasher,
    PasswordSetupMailer mailer,
    IMediator mediator,
    IUnitOfWork unitOfWork) : IRequestHandler<InviteUserRequest, InviteUserResponse>
{
    public async Task<InviteUserResponse> Handle(InviteUserRequest request, CancellationToken cancellationToken)
    {
        var email = Identity.NormalizeEmail(request.Email);
        var fullName = request.FullName?.Trim() ?? string.Empty;
        if (!Identity.IsValidEmail(email))
            return new InviteUserValidationFailure("El correo no es valido.");
        if (fullName.Length is 0 or > 200)
            return new InviteUserValidationFailure("El nombre es obligatorio y admite hasta 200 caracteres.");

        var codes = (request.RoleCodes is { Count: > 0 } ? request.RoleCodes : [RoleCodes.Member])
            .Select(c => c.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var roles = await rbac.GetRolesByCodesAsync(request.TenantId, codes, cancellationToken);
        var unknown = codes.Except(roles.Select(r => r.Code), StringComparer.Ordinal).ToArray();
        if (unknown.Length > 0)
            return new InviteUserValidationFailure($"Rol desconocido: {string.Join(", ", unknown)}.");

        if (codes.Contains(RoleCodes.Admin))
        {
            var actor = await credentials.FindInTenantAsync(request.ActorUserId, cancellationToken);
            var actorRoles = actor is null ? [] : (await rbac.GetGrantsAsync(request.TenantId, actor.Id, cancellationToken)).Roles;
            if (!actorRoles.Contains(RoleCodes.Admin))
                return new InviteUserForbiddenFailure("Solo un administrador puede conceder el rol de administrador.");
        }

        if (await credentials.EmailExistsAsync(email, cancellationToken))
            return new InviteUserConflictFailure("Ya existe una cuenta con ese correo.");

        return await unitOfWork.ExecuteAsync<InviteUserResponse>(async ct =>
        {
            var credential = new UserCredential
            {
                TenantId = request.TenantId,
                Email = email,
                PasswordHash = hasher.Hash(SecureTokens.Generate()),
            };
            credentials.Add(credential);
            await unitOfWork.SaveChangesAsync(ct);

            foreach (var role in roles)
                rbac.AssignRole(request.TenantId, credential.Id, role.Id);

            await mediator.Publish(new UserShouldBeCreatedIntegrationEvent(credential.PublicId, request.TenantId, fullName, email), ct);
            await mailer.SendAsync(credential, PasswordSetupPurpose.Invitation, ct);

            return new InviteUserSuccess(new InvitedUserDto(credential.PublicId, email, [.. roles.Select(r => r.Code)]));
        }, cancellationToken);
    }
}
