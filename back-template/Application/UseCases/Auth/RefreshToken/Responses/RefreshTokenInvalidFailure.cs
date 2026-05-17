using Common.Results;

namespace Application.UseCases.Auth.RefreshToken.Responses;

public sealed record RefreshTokenInvalidFailure(string Message) : RefreshTokenResponse, IFailure;
