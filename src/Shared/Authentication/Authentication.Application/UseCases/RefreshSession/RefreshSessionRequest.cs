using Authentication.Application.UseCases.RefreshSession.Responses;
using Common.Messaging;

namespace Authentication.Application.UseCases.RefreshSession;

public sealed record RefreshSessionRequest(string RefreshToken, string? ClientIp) : IRequest<RefreshSessionResponse>;
