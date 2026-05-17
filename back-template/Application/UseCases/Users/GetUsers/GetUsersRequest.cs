using Application.UseCases.Users.GetUsers.Responses;
using Common.Messaging;

namespace Application.UseCases.Users.GetUsers;

public sealed record GetUsersRequest(long TenantId, int Page, int PageSize) : IRequest<GetUsersResponse>;
