using Authentication.Application.UseCases.Logout.Responses;
using Common.Messaging;

namespace Authentication.Application.UseCases.Logout;

public sealed record LogoutRequest(string RefreshToken) : IRequest<LogoutResponse>;
