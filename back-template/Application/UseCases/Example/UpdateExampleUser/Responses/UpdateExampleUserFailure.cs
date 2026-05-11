using Common.Results;

namespace Application.UseCases.Example.UpdateExampleUser;

public sealed record UpdateExampleUserNotFoundFailure(string Message) : UpdateExampleUserResponse, INotFoundFailure;
