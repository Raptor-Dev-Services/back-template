using Application.Dto.Users;
using Application.UseCases.Users.GetUsers.Responses;
using Common.Messaging;
using Domain.Repositories.Users;

namespace Application.UseCases.Users.GetUsers;

public sealed class GetUsersHandler : IRequestHandler<GetUsersRequest, GetUsersResponse>
{
    private readonly IUserRepository _users;

    public GetUsersHandler(IUserRepository users) => _users = users;

    public async Task<GetUsersResponse> Handle(GetUsersRequest request, CancellationToken cancellationToken)
    {
        var page     = Math.Clamp(request.Page, 1, int.MaxValue);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var listTask  = _users.GetPagedAsync(request.TenantId, page, pageSize, cancellationToken);
        var totalTask = _users.GetCountAsync(request.TenantId, cancellationToken);
        await Task.WhenAll(listTask, totalTask);

        var dtos = listTask.Result.Select(u => new UserDto(
            u.PublicId, u.FullName, u.Email, u.Role,
            u.IsActive, u.CreatedAtUtc, u.UpdatedAtUtc)).ToArray();

        return new GetUsersSuccess(dtos, totalTask.Result, page, pageSize);
    }
}
