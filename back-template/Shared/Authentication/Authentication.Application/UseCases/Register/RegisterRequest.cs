using Authentication.Application.UseCases.Register.Responses;
using Common.Messaging;

namespace Authentication.Application.UseCases.Register;

public sealed record RegisterRequest(
    string Email,
    string Password,
    long   TenantId,
    long   BranchId,
    string FullName,
    string Role) : IRequest<RegisterResponse>;
