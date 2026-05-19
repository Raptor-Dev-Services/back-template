using Authentication.Application.Dto;
using Common.Results;

namespace Authentication.Application.UseCases.Login.Responses;

public sealed record LoginSuccess(TokenDto Data) : LoginResponse, ISuccess<TokenDto>;
