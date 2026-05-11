using Application.Dto.Example.User;
using Common.Messaging;
using Domain.Repositories.Example;

namespace Application.UseCases.Example.InsertExampleUser;

public sealed class InsertExampleUserHandler : IRequestHandler<InsertExampleUserRequest, InsertExampleUserResponse>
{
    private readonly IExampleUserRepository _repo;

    public InsertExampleUserHandler(IExampleUserRepository repo) => _repo = repo;

    public async Task<InsertExampleUserResponse> Handle(InsertExampleUserRequest request, CancellationToken cancellationToken)
    {
        if (await _repo.ExistsByEmailAsync(request.Email, cancellationToken))
            return new InsertExampleUserConflictFailure("El email ya está registrado.");

        var user = await _repo.InsertAsync(
            request.FullName, request.Email, request.Department, request.Notes, cancellationToken);

        return new InsertExampleUserSuccess(new ExampleUserDto(
            user.PublicId, user.FullName, user.Email, user.Department,
            user.Notes, user.IsActive, user.CreatedAtUtc, user.UpdatedAtUtc));
    }
}
