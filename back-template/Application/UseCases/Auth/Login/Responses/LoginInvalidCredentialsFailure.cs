using Common.Results;

namespace Application.UseCases.Auth.Login.Responses;

public sealed record LoginInvalidCredentialsFailure(string Message) : LoginResponse, IFailure;
