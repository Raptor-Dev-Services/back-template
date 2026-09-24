using Authentication.Application.UseCases.GetRoles;
using Common.Messaging;
using Common.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Kernel.Security;
using Shared.Web;
using Shared.Web.Authorization;

namespace Authentication.Presentation.Controllers;

[Route("api/v1/roles")]
public sealed class RolesController(IMediator mediator, ResultViewModel<RolesController> viewModel) : BaseApiController(mediator)
{
    /// <summary>Roles del tenant con sus permisos (para elegir al invitar).</summary>
    [HttpGet]
    [Authorize(Policy = PermissionPolicy.Prefix + KnownPermissions.UsersRead)]
    public async Task<IActionResult> List(CancellationToken cancellationToken = default) =>
        MapResult(await DispatchAsync(new GetRolesRequest(CurrentTenantId), cancellationToken), viewModel);
}
