using Application.UseCases.Users.UpdateUser.Responses;
using Common.Messaging;

namespace Application.UseCases.Users.UpdateUser;

public sealed record UpdateUserRequest(Guid PublicId, long TenantId, string FullName) : IRequest<UpdateUserResponse>;
