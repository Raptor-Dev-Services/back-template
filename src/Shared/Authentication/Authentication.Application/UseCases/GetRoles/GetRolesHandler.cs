using Authentication.Application.Dto;
using Authentication.Application.UseCases.GetRoles.Responses;
using Authentication.Domain.Repositories;
using Common.Messaging;

namespace Authentication.Application.UseCases.GetRoles;

internal sealed class GetRolesHandler(IRbacRepository rbac) : IRequestHandler<GetRolesRequest, GetRolesResponse>
{
    public async Task<GetRolesResponse> Handle(GetRolesRequest request, CancellationToken cancellationToken)
    {
        var roles = await rbac.ListRolesAsync(request.TenantId, cancellationToken);
        return new GetRolesSuccess([.. roles.Select(r => new RoleDto(r.Role.Code, r.Role.Name, r.Role.IsSystem, r.Permissions))]);
    }
}
