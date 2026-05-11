using Common.Results;

namespace Application.UseCases.Example.DisableExampleUser;

public sealed record DisableExampleUserNotFoundFailure(string Message) : DisableExampleUserResponse, INotFoundFailure;
