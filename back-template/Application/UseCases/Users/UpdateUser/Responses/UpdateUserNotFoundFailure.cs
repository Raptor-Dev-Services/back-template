using Common.Results;

namespace Application.UseCases.Users.UpdateUser.Responses;

public sealed record UpdateUserNotFoundFailure(string Message) : UpdateUserResponse, INotFoundFailure;
