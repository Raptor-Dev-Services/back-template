using Authentication.Application.Dto;
using Common.Results;

namespace Authentication.Application.UseCases.RefreshToken.Responses;

public sealed record RefreshTokenSuccess(TokenDto Data) : RefreshTokenResponse, ISuccess<TokenDto>;
