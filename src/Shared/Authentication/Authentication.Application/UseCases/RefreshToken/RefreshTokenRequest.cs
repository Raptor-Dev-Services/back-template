using Authentication.Application.UseCases.RefreshToken.Responses;
using Common.Messaging;

namespace Authentication.Application.UseCases.RefreshToken;

public sealed record RefreshTokenRequest(string Token) : IRequest<RefreshTokenResponse>;
