using Users.Contracts.Dtos;
using Users.Domain.Entities;

namespace Users.Application.UseCases;

internal static class UserProfileMapping
{
    public static UserProfileDto ToDto(UserProfile profile) =>
        new(profile.PublicId, profile.FullName, profile.IsActive, profile.CreatedAtUtc, profile.UpdatedAtUtc);
}
