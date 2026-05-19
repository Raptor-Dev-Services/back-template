using Common.Results;

namespace Authentication.Application.UseCases.Register.Responses;

public sealed record RegisterEmailConflictFailure(string Message) : RegisterResponse, IConflictFailure;
