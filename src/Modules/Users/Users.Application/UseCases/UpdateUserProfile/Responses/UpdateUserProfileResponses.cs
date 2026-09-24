using Common.Messaging;
using Common.Results;
using Users.Contracts.Dtos;

namespace Users.Application.UseCases.UpdateUserProfile.Responses;

public abstract record UpdateUserProfileResponse : IResponse;

public sealed record UpdateUserProfileSuccess(UserProfileDto Data) : UpdateUserProfileResponse, ISuccess<UserProfileDto>;

public sealed record UpdateUserProfileNotFoundFailure(string Message) : UpdateUserProfileResponse, INotFoundFailure;

public sealed record UpdateUserProfileValidationFailure(string Message) : UpdateUserProfileResponse, IValidationFailure;
