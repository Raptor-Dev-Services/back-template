using Authentication.Application.UseCases.RequestPasswordReset.Responses;
using Common.Messaging;

namespace Authentication.Application.UseCases.RequestPasswordReset;

public sealed record RequestPasswordResetRequest(string Email) : IRequest<RequestPasswordResetResponse>;
