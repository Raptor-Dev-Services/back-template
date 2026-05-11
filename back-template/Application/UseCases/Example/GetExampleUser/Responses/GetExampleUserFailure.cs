using Common.Results;

namespace Application.UseCases.Example.GetExampleUser;

public sealed record GetExampleUserNotFoundFailure(string Message) : GetExampleUserResponse, INotFoundFailure;
