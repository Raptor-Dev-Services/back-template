using Common.Results;
using Users.Contracts.Dtos;

namespace Users.Application.UseCases.GetUserProfiles.Responses;

public sealed record GetUserProfilesSuccess(
    IReadOnlyCollection<UserProfileDto> Profiles,
    int Total,
    int Page,
    int PageSize) : GetUserProfilesResponse, ISuccess;
