using Application.UseCases.Auth.Login.Responses;
using Common.Messaging;

namespace Application.UseCases.Auth.Login;

public sealed record LoginRequest(string Email, string Password) : IRequest<LoginResponse>;
