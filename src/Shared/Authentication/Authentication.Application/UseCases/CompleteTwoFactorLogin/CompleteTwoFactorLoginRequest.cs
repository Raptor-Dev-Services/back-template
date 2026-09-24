using Authentication.Application.UseCases.CompleteTwoFactorLogin.Responses;
using Common.Messaging;

namespace Authentication.Application.UseCases.CompleteTwoFactorLogin;

/// <summary>Segundo paso del login: el reto del primero + un codigo TOTP o de recuperacion.</summary>
public sealed record CompleteTwoFactorLoginRequest(string ChallengeToken, string Code, string? ClientIp)
    : IRequest<CompleteTwoFactorLoginResponse>;
