using Common.Messaging;
using Users.Application.UseCases.GetUserProfile.Responses;
using Users.Domain.Repositories;

namespace Users.Application.UseCases.GetUserProfile;

internal sealed class GetUserProfileHandler(IUserProfileRepository profiles)
    : IRequestHandler<GetUserProfileRequest, GetUserProfileResponse>
{
    public async Task<GetUserProfileResponse> Handle(GetUserProfileRequest request, CancellationToken cancellationToken)
    {
        // Un perfil de otro tenant no existe para este llamador: el filtro (y RLS) lo ocultan y la respuesta es
        // la misma que para un id inventado (404), sin confirmar que el id existe en otra empresa.
        var profile = await profiles.GetByPublicIdAsync(request.PublicId, cancellationToken);
        return profile is null
            ? new GetUserProfileNotFoundFailure("Perfil de usuario no encontrado.")
            : new GetUserProfileSuccess(UserProfileMapping.ToDto(profile));
    }
}
