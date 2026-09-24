using Common.Results;

namespace Users.Application.UseCases.GetUserProfile.Responses;

public sealed record GetUserProfileNotFoundFailure(string Message) : GetUserProfileResponse, INotFoundFailure;
