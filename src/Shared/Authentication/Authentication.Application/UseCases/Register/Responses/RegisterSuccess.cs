using Authentication.Application.Dto;
using Common.Results;

namespace Authentication.Application.UseCases.Register.Responses;

public sealed record RegisterSuccess(TokenDto Data) : RegisterResponse, ISuccess<TokenDto>;
