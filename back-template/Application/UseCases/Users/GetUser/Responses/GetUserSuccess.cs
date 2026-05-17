using Application.Dto.Users;
using Common.Results;

namespace Application.UseCases.Users.GetUser.Responses;

public sealed record GetUserSuccess(UserDto Data) : GetUserResponse, ISuccess<UserDto>;
