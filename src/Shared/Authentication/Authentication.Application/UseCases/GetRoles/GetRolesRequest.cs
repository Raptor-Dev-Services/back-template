using Authentication.Application.UseCases.GetRoles.Responses;
using Common.Messaging;

namespace Authentication.Application.UseCases.GetRoles;

public sealed record GetRolesRequest(long TenantId) : IRequest<GetRolesResponse>;
