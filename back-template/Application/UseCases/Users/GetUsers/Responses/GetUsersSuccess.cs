using Application.Dto.Users;
using Common.Results;

namespace Application.UseCases.Users.GetUsers.Responses;

public sealed record GetUsersSuccess(
    IReadOnlyCollection<UserDto> Users,
    int Total,
    int Page,
    int PageSize) : GetUsersResponse, ISuccess;
