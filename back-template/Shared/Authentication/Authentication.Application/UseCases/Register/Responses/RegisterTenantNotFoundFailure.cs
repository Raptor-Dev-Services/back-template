using Common.Results;

namespace Authentication.Application.UseCases.Register.Responses;

public sealed record RegisterTenantNotFoundFailure(string Message) : RegisterResponse, INotFoundFailure;
