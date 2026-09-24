using Authentication.Application.UseCases.Login.Responses;
using Common.Messaging;

namespace Authentication.Application.UseCases.Login;

public sealed record LoginRequest(string Email, string Password, string? ClientIp) : IRequest<LoginResponse>;
