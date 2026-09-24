using Common.Messaging;
using Users.Application.UseCases.UpdateUserProfile.Responses;
using Users.Domain.Repositories;

namespace Users.Application.UseCases.UpdateUserProfile;

internal sealed class UpdateUserProfileHandler(IUserProfileRepository profiles)
    : IRequestHandler<UpdateUserProfileRequest, UpdateUserProfileResponse>
{
    public async Task<UpdateUserProfileResponse> Handle(UpdateUserProfileRequest request, CancellationToken cancellationToken)
    {
        var fullName = request.FullName?.Trim() ?? string.Empty;
        if (fullName.Length is 0 or > 200)
            return new UpdateUserProfileValidationFailure("El nombre es obligatorio y admite hasta 200 caracteres.");

        var profile = await profiles.GetForUpdateAsync(request.PublicId, cancellationToken);
        if (profile is null)
            return new UpdateUserProfileNotFoundFailure("Perfil de usuario no encontrado.");

        profile.FullName = fullName;
        await profiles.SaveChangesAsync(cancellationToken);
        return new UpdateUserProfileSuccess(UserProfileMapping.ToDto(profile));
    }
}
