using Common.Messaging;
using Users.Application.UseCases.GetUserProfile.Responses;
using Users.Contracts.Dtos;
using Users.Domain.Repositories;

namespace Users.Application.UseCases.GetUserProfile;

internal sealed class GetUserProfileHandler(IUserProfileRepository profiles)
    : IRequestHandler<GetUserProfileRequest, GetUserProfileResponse>
{
    public async Task<GetUserProfileResponse> Handle(GetUserProfileRequest request, CancellationToken cancellationToken)
    {
        // Un perfil de otro tenant no existe para este llamador: el filtro lo oculta y la respuesta es la
        // misma que para un id inventado (404), sin confirmar que el id existe en otra parte.
        var profile = await profiles.GetByPublicIdAsync(request.PublicId, cancellationToken);
        if (profile is null)
            return new GetUserProfileNotFoundFailure("Perfil de usuario no encontrado.");

        return new GetUserProfileSuccess(new UserProfileDto(
            profile.PublicId, profile.FullName, profile.IsActive, profile.CreatedAtUtc, profile.UpdatedAtUtc));
    }
}
