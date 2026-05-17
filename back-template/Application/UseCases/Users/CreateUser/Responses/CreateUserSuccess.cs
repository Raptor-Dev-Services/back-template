using Application.Dto.Users;
using Common.Results;

namespace Application.UseCases.Users.CreateUser.Responses;

public sealed record CreateUserSuccess(UserDto Data) : CreateUserResponse, ISuccess<UserDto>;
