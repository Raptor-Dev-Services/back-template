using Common.Results;

namespace Users.Application.UseCases.UpdateUserProfile.Responses;

public sealed record UpdateUserProfileValidationFailure(string Message) : UpdateUserProfileResponse, IValidationFailure;
