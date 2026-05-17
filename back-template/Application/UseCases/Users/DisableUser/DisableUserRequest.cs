using Application.UseCases.Users.DisableUser.Responses;
using Common.Messaging;

namespace Application.UseCases.Users.DisableUser;

public sealed record DisableUserRequest(Guid PublicId, long TenantId) : IRequest<DisableUserResponse>;
