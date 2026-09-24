using Common.Messaging;
using Shared.Kernel.Audit;
using Shared.Kernel.Context;
using Users.Application.UseCases.DisableUserProfile.Responses;
using Users.Contracts.Events;
using Users.Domain.Repositories;

namespace Users.Application.UseCases.DisableUserProfile;

/// <summary>
/// Baja (reversible) de un usuario: el perfil se marca inactivo y, en la MISMA transaccion, Authentication
/// apaga su credencial y revoca sus sesiones (via <see cref="UserDisabledIntegrationEvent"/>).
/// </summary>
internal sealed class DisableUserProfileHandler(
    IUserProfileRepository profiles,
    IMediator mediator,
    IAuditLog audit,
    IUnitOfWork unitOfWork) : IRequestHandler<DisableUserProfileRequest, DisableUserProfileResponse>
{
    public async Task<DisableUserProfileResponse> Handle(DisableUserProfileRequest request, CancellationToken cancellationToken)
    {
        if (request.ActorUserId == request.PublicId)
            return new DisableUserProfileValidationFailure("No puedes dar de baja tu propia cuenta.");

        var profile = await profiles.GetForUpdateAsync(request.PublicId, cancellationToken);
        if (profile is null)
            return new DisableUserProfileNotFoundFailure("Perfil de usuario no encontrado.");

        return await unitOfWork.ExecuteAsync<DisableUserProfileResponse>(async ct =>
        {
            profile.IsActive = false;
            await mediator.Publish(new UserDisabledIntegrationEvent(profile.PublicId), ct);
            audit.Append("user.disabled", "UserProfile", profile.PublicId.ToString(), "Baja del usuario: credencial apagada y sesiones revocadas.");
            await unitOfWork.SaveChangesAsync(ct);
            return new DisableUserProfileSuccess(UserProfileMapping.ToDto(profile));
        }, cancellationToken);
    }
}
