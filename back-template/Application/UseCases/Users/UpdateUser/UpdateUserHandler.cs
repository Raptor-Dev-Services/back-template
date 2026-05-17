using Application.UseCases.Users.UpdateUser.Responses;
using Common.Messaging;
using Domain.Repositories.Users;

namespace Application.UseCases.Users.UpdateUser;

public sealed class UpdateUserHandler : IRequestHandler<UpdateUserRequest, UpdateUserResponse>
{
    private readonly IUserRepository _users;

    public UpdateUserHandler(IUserRepository users) => _users = users;

    public async Task<UpdateUserResponse> Handle(UpdateUserRequest request, CancellationToken cancellationToken)
    {
        var updated = await _users.UpdateAsync(request.PublicId, request.TenantId, request.FullName, cancellationToken);
        return updated
            ? new UpdateUserSuccess()
            : new UpdateUserNotFoundFailure("Usuario no encontrado.");
    }
}
