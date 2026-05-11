using Common.Messaging;
using Domain.Repositories.Example;

namespace Application.UseCases.Example.UpdateExampleUser;

public sealed class UpdateExampleUserHandler : IRequestHandler<UpdateExampleUserRequest, UpdateExampleUserResponse>
{
    private readonly IExampleUserRepository _repo;

    public UpdateExampleUserHandler(IExampleUserRepository repo) => _repo = repo;

    public async Task<UpdateExampleUserResponse> Handle(UpdateExampleUserRequest request, CancellationToken cancellationToken)
    {
        var updated = await _repo.UpdateAsync(
            request.PublicId, request.FullName, request.Department, request.Notes, cancellationToken);

        if (!updated)
            return new UpdateExampleUserNotFoundFailure("Usuario no encontrado.");

        return new UpdateExampleUserSuccess();
    }
}
