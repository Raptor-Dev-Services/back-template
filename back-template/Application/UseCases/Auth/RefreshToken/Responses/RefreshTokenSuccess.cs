using Application.Dto.Auth;
using Common.Results;

namespace Application.UseCases.Auth.RefreshToken.Responses;

public sealed record RefreshTokenSuccess(TokenDto Data) : RefreshTokenResponse, ISuccess<TokenDto>;
