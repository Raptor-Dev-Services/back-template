using Common.Messaging;
using Common.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Kernel.Results;
using Shared.Kernel.Security;
using Shared.Web;
using Shared.Web.Authorization;
using Tenancy.Application.UseCases.GetAuditLog;

namespace Tenancy.Presentation.Controllers;

[Route("api/v1/audit-log")]
public sealed class AuditLogController(IMediator mediator, ResultViewModel<AuditLogController> viewModel) : BaseApiController(mediator)
{
    /// <summary>Bitacora de acciones del tenant, de la mas reciente a la mas antigua.</summary>
    [HttpGet]
    [Authorize(Policy = PermissionPolicy.Prefix + KnownPermissions.AuditRead)]
    public async Task<IActionResult> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = Paging.DefaultPageSize,
        [FromQuery] string? action = null,
        CancellationToken cancellationToken = default) =>
        MapResult(await DispatchAsync(new GetAuditLogRequest(page, pageSize, action), cancellationToken), viewModel);
}
