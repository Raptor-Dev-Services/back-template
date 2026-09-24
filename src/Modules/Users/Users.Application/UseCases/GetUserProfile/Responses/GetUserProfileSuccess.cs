using Common.Results;
using Users.Contracts.Dtos;

namespace Users.Application.UseCases.GetUserProfile.Responses;

public sealed record GetUserProfileSuccess(UserProfileDto Data) : GetUserProfileResponse, ISuccess<UserProfileDto>;
