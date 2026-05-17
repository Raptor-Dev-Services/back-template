using Common.Results;

namespace Application.UseCases.Users.CreateUser.Responses;

public sealed record CreateUserEmailConflictFailure(string Message) : CreateUserResponse, IConflictFailure;
