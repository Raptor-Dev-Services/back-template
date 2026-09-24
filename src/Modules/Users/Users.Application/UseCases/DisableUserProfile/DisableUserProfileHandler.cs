using Common.Messaging;
using Users.Application.UseCases.DisableUserProfile.Responses;
using Users.Domain.Repositories;

namespace Users.Application.UseCases.DisableUserProfile;

internal sealed class DisableUserProfileHandler(IUserProfileRepository profiles)
    : IRequestHandler<DisableUserProfileRequest, DisableUserProfileResponse>
{
    public async Task<DisableUserProfileResponse> Handle(DisableUserProfileRequest request, CancellationToken cancellationToken)
    {
        var profile = await profiles.GetForUpdateAsync(request.PublicId, cancellationToken);
        if (profile is null)
            return new DisableUserProfileNotFoundFailure("Perfil de usuario no encontrado.");

        profile.IsActive = false;
        await profiles.SaveChangesAsync(cancellationToken);
        return new DisableUserProfileSuccess();
    }
}
