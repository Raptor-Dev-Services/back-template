using Shared.Kernel.Results;

namespace Authentication.Application.UseCases.Login.Responses;

public sealed record LoginInvalidCredentialsFailure(string Message) : LoginResponse, IUnauthorizedFailure;
