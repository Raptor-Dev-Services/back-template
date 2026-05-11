using Application.Dto.Example.User;
using Common.Messaging;
using Domain.Repositories.Example;

namespace Application.UseCases.Example.GetExampleUser;

public sealed class GetExampleUserHandler : IRequestHandler<GetExampleUserRequest, GetExampleUserResponse>
{
    private readonly IExampleUserRepository _repo;

    public GetExampleUserHandler(IExampleUserRepository repo) => _repo = repo;

    public async Task<GetExampleUserResponse> Handle(GetExampleUserRequest request, CancellationToken cancellationToken)
    {
        var user = await _repo.GetByPublicIdAsync(request.PublicId, cancellationToken);
        if (user is null)
            return new GetExampleUserNotFoundFailure("Usuario no encontrado.");

        return new GetExampleUserSuccess(new ExampleUserDto(
            user.PublicId, user.FullName, user.Email, user.Department,
            user.Notes, user.IsActive, user.CreatedAtUtc, user.UpdatedAtUtc));
    }
}
