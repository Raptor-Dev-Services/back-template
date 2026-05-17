using Application.UseCases.Users.DisableUser.Responses;
using Common.Messaging;
using Domain.Repositories.Users;

namespace Application.UseCases.Users.DisableUser;

public sealed class DisableUserHandler : IRequestHandler<DisableUserRequest, DisableUserResponse>
{
    private readonly IUserRepository _users;

    public DisableUserHandler(IUserRepository users) => _users = users;

    public async Task<DisableUserResponse> Handle(DisableUserRequest request, CancellationToken cancellationToken)
    {
        var disabled = await _users.DisableAsync(request.PublicId, request.TenantId, cancellationToken);
        return disabled
            ? new DisableUserSuccess()
            : new DisableUserNotFoundFailure("Usuario no encontrado.");
    }
}
