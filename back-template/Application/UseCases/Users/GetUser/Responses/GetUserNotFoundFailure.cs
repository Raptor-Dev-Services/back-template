using Common.Results;

namespace Application.UseCases.Users.GetUser.Responses;

public sealed record GetUserNotFoundFailure(string Message) : GetUserResponse, INotFoundFailure;
