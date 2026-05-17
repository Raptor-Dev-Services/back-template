using Application.Dto.Users;
using Application.UseCases.Users.GetUser.Responses;
using Common.Messaging;
using Domain.Repositories.Users;

namespace Application.UseCases.Users.GetUser;

public sealed class GetUserHandler : IRequestHandler<GetUserRequest, GetUserResponse>
{
    private readonly IUserRepository _users;

    public GetUserHandler(IUserRepository users) => _users = users;

    public async Task<GetUserResponse> Handle(GetUserRequest request, CancellationToken cancellationToken)
    {
        var user = await _users.GetByPublicIdAsync(request.PublicId, request.TenantId, cancellationToken);
        if (user is null)
            return new GetUserNotFoundFailure("Usuario no encontrado.");

        return new GetUserSuccess(new UserDto(
            user.PublicId, user.FullName, user.Email,
            user.Role, user.IsActive, user.CreatedAtUtc, user.UpdatedAtUtc));
    }
}
