using Application.UseCases.Auth.RefreshToken.Responses;
using Common.Messaging;

namespace Application.UseCases.Auth.RefreshToken;

public sealed record RefreshTokenRequest(string Token) : IRequest<RefreshTokenResponse>;
