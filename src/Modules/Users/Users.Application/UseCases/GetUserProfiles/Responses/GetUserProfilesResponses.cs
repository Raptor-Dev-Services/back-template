using Common.Messaging;
using Common.Results;
using Shared.Kernel.Results;
using Users.Contracts.Dtos;

namespace Users.Application.UseCases.GetUserProfiles.Responses;

public abstract record GetUserProfilesResponse : IResponse;

public sealed record GetUserProfilesSuccess(PagedResult<UserProfileDto> Data)
    : GetUserProfilesResponse, ISuccess<PagedResult<UserProfileDto>>;
