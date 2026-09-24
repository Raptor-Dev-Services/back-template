using Common.Messaging;
using Users.Application.UseCases.GetUserProfile.Responses;
using Users.Contracts.Dtos;
using Users.Domain.Repositories;

namespace Users.Application.UseCases.GetUserProfile;

public sealed class GetUserProfileHandler : IRequestHandler<GetUserProfileRequest, GetUserProfileResponse>
{
    private readonly IUserProfileRepository _profiles;

    public GetUserProfileHandler(IUserProfileRepository profiles) => _profiles = profiles;

    public async Task<GetUserProfileResponse> Handle(GetUserProfileRequest request, CancellationToken cancellationToken)
    {
        var profile = await _profiles.GetByPublicIdAsync(request.PublicId, request.TenantId, cancellationToken);
        if (profile is null)
            return new GetUserProfileNotFoundFailure("Perfil de usuario no encontrado.");

        return new GetUserProfileSuccess(new UserProfileDto(
            profile.PublicId, profile.FullName, profile.IsActive,
            profile.CreatedAtUtc, profile.UpdatedAtUtc));
    }
}
