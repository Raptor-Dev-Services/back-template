using Application.UseCases.Users.GetUser.Responses;
using Common.Messaging;

namespace Application.UseCases.Users.GetUser;

public sealed record GetUserRequest(Guid PublicId, long TenantId) : IRequest<GetUserResponse>;
