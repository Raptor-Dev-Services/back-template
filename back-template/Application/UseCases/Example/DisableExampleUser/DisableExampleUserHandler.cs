using Common.Messaging;
using Domain.Repositories.Example;

namespace Application.UseCases.Example.DisableExampleUser;

public sealed class DisableExampleUserHandler : IRequestHandler<DisableExampleUserRequest, DisableExampleUserResponse>
{
    private readonly IExampleUserRepository _repo;

    public DisableExampleUserHandler(IExampleUserRepository repo) => _repo = repo;

    public async Task<DisableExampleUserResponse> Handle(DisableExampleUserRequest request, CancellationToken cancellationToken)
    {
        var disabled = await _repo.DisableAsync(request.PublicId, cancellationToken);
        if (!disabled)
            return new DisableExampleUserNotFoundFailure("Usuario no encontrado.");

        return new DisableExampleUserSuccess();
    }
}
