using Common.Results;

namespace Application.UseCases.Users.DisableUser.Responses;

public sealed record DisableUserNotFoundFailure(string Message) : DisableUserResponse, INotFoundFailure;
