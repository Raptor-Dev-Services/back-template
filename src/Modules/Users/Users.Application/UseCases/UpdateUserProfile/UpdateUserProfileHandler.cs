using Common.Messaging;
using Users.Application.UseCases.UpdateUserProfile.Responses;
using Users.Domain.Repositories;

namespace Users.Application.UseCases.UpdateUserProfile;

public sealed class UpdateUserProfileHandler : IRequestHandler<UpdateUserProfileRequest, UpdateUserProfileResponse>
{
    private readonly IUserProfileRepository _profiles;

    public UpdateUserProfileHandler(IUserProfileRepository profiles) => _profiles = profiles;

    public async Task<UpdateUserProfileResponse> Handle(UpdateUserProfileRequest request, CancellationToken cancellationToken)
    {
        var updated = await _profiles.UpdateAsync(request.PublicId, request.TenantId, request.FullName, cancellationToken);
        return updated
            ? new UpdateUserProfileSuccess()
            : new UpdateUserProfileNotFoundFailure("Perfil de usuario no encontrado.");
    }
}
