using Common.Messaging;
using Common.Results;
using Users.Contracts.Dtos;

namespace Users.Application.UseCases.GetUserProfile.Responses;

public abstract record GetUserProfileResponse : IResponse;

public sealed record GetUserProfileSuccess(UserProfileDto Data) : GetUserProfileResponse, ISuccess<UserProfileDto>;

public sealed record GetUserProfileNotFoundFailure(string Message) : GetUserProfileResponse, INotFoundFailure;
