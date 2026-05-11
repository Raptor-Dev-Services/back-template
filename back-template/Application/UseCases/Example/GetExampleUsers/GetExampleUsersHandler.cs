using Application.Dto.Example.User;
using Common.Messaging;
using Domain.Repositories.Example;

namespace Application.UseCases.Example.GetExampleUsers;

public sealed class GetExampleUsersHandler : IRequestHandler<GetExampleUsersRequest, GetExampleUsersResponse>
{
    private readonly IExampleUserRepository _repo;

    public GetExampleUsersHandler(IExampleUserRepository repo) => _repo = repo;

    public async Task<GetExampleUsersResponse> Handle(GetExampleUsersRequest request, CancellationToken cancellationToken)
    {
        var users = await _repo.GetPagedAsync(request.Page, request.PageSize, cancellationToken);
        var total = await _repo.GetCountAsync(cancellationToken);

        var dtos = users
            .Select(u => new ExampleUserDto(
                u.PublicId, u.FullName, u.Email, u.Department,
                u.Notes, u.IsActive, u.CreatedAtUtc, u.UpdatedAtUtc))
            .ToArray();

        return new GetExampleUsersSuccess(dtos, total, request.Page, request.PageSize);
    }
}
