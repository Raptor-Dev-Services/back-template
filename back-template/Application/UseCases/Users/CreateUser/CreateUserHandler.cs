using Application.Dto.Users;
using Application.Services;
using Application.UseCases.Users.CreateUser.Responses;
using Common.Messaging;
using Domain.Repositories.Users;

namespace Application.UseCases.Users.CreateUser;

public sealed class CreateUserHandler : IRequestHandler<CreateUserRequest, CreateUserResponse>
{
    private readonly IUserRepository _users;
    private readonly IPasswordHasher _hasher;

    public CreateUserHandler(IUserRepository users, IPasswordHasher hasher)
    {
        _users  = users;
        _hasher = hasher;
    }

    public async Task<CreateUserResponse> Handle(CreateUserRequest request, CancellationToken cancellationToken)
    {
        var exists = await _users.ExistsByEmailAsync(request.Email, cancellationToken);
        if (exists)
            return new CreateUserEmailConflictFailure("El correo ya está registrado.");

        var hash = _hasher.Hash(request.Password);
        var user = await _users.InsertAsync(
            request.TenantId, request.BranchId,
            request.FullName, request.Email, hash, request.Role,
            cancellationToken);

        return new CreateUserSuccess(new UserDto(
            user.PublicId, user.FullName, user.Email,
            user.Role, user.IsActive, user.CreatedAtUtc, user.UpdatedAtUtc));
    }
}
