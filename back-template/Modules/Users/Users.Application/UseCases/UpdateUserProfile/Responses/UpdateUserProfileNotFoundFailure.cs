using Common.Results;

namespace Users.Application.UseCases.UpdateUserProfile.Responses;

public sealed record UpdateUserProfileNotFoundFailure(string Message) : UpdateUserProfileResponse, INotFoundFailure;
