using Application.Dto.Example.User;
using Common.Results;

namespace Application.UseCases.Example.GetExampleUsers;

public sealed record GetExampleUsersSuccess(
    IReadOnlyCollection<ExampleUserDto> Users,
    int Total,
    int Page,
    int PageSize) : GetExampleUsersResponse, ISuccess;
