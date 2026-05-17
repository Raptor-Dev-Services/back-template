using Application.UseCases.Users.CreateUser.Responses;
using Common.Messaging;

namespace Application.UseCases.Users.CreateUser;

public sealed record CreateUserRequest(
    long   TenantId,
    long   BranchId,
    string FullName,
    string Email,
    string Password,
    string Role) : IRequest<CreateUserResponse>;
