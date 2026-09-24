using Common.Messaging;
using Common.Results;
using Users.Contracts.Dtos;

namespace Users.Application.UseCases.DisableUserProfile.Responses;

public abstract record DisableUserProfileResponse : IResponse;

public sealed record DisableUserProfileSuccess(UserProfileDto Data) : DisableUserProfileResponse, ISuccess<UserProfileDto>;

public sealed record DisableUserProfileNotFoundFailure(string Message) : DisableUserProfileResponse, INotFoundFailure;

public sealed record DisableUserProfileValidationFailure(string Message) : DisableUserProfileResponse, IValidationFailure;
