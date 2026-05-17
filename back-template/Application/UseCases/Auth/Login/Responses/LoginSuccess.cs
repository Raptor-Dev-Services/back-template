using Application.Dto.Auth;
using Common.Results;

namespace Application.UseCases.Auth.Login.Responses;

public sealed record LoginSuccess(TokenDto Data) : LoginResponse, ISuccess<TokenDto>;
