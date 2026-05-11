using Common.Results;

namespace Application.UseCases.Example.InsertExampleUser;

public sealed record InsertExampleUserConflictFailure(string Message) : InsertExampleUserResponse, IConflictFailure;
