using Shared.Kernel.Results;

namespace Authentication.Application.UseCases.RefreshToken.Responses;

public sealed record RefreshTokenInvalidFailure(string Message) : RefreshTokenResponse, IUnauthorizedFailure;
