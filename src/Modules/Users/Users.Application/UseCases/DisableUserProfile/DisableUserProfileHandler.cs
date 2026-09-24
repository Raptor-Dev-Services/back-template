using Common.Messaging;
using Users.Application.UseCases.DisableUserProfile.Responses;
using Users.Domain.Repositories;

namespace Users.Application.UseCases.DisableUserProfile;

internal sealed class DisableUserProfileHandler : IRequestHandler<DisableUserProfileRequest, DisableUserProfileResponse>
{
    private readonly IUserProfileRepository _profiles;

    public DisableUserProfileHandler(IUserProfileRepository profiles) => _profiles = profiles;

    public async Task<DisableUserProfileResponse> Handle(DisableUserProfileRequest request, CancellationToken cancellationToken)
    {
        var disabled = await _profiles.DisableAsync(request.PublicId, request.TenantId, cancellationToken);
        return disabled
            ? new DisableUserProfileSuccess()
            : new DisableUserProfileNotFoundFailure("Perfil de usuario no encontrado.");
    }
}
