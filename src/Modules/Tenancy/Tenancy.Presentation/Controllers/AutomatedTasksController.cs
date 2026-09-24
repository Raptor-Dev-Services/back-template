using Common.Messaging;
using Common.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Kernel.Results;
using Shared.Kernel.Security;
using Shared.Web;
using Shared.Web.Authorization;
using Tenancy.Application.UseCases.AutomatedTasks.GetAutomatedTaskHistory;
using Tenancy.Application.UseCases.AutomatedTasks.ListAutomatedTasks;
using Tenancy.Application.UseCases.AutomatedTasks.RunAutomatedTaskNow;
using Tenancy.Application.UseCases.AutomatedTasks.SetAutomatedTaskEnabled;

namespace Tenancy.Presentation.Controllers;

/// <summary>
/// Operacion de las tareas programadas: ver, pausar/reanudar, ejecutar ahora e historial. Exige <c>tasks.manage</c>
/// y ademas ser del tenant operador (lo decide el caso de uso: el permiso solo no basta).
/// </summary>
[Route("api/v1/automated-tasks")]
[Authorize(Policy = PermissionPolicy.Prefix + KnownPermissions.TasksManage)]
public sealed class AutomatedTasksController(IMediator mediator, ResultViewModel<AutomatedTasksController> viewModel)
    : BaseApiController(mediator)
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken = default) =>
        MapResult(await DispatchAsync(new ListAutomatedTasksRequest(), cancellationToken), viewModel);

    [HttpPut("{code}/enabled")]
    public async Task<IActionResult> SetEnabled(string code, [FromBody] SetAutomatedTaskEnabledBody body, CancellationToken cancellationToken = default) =>
        MapResult(await DispatchAsync(new SetAutomatedTaskEnabledRequest(code, body.IsEnabled), cancellationToken), viewModel);

    [HttpPost("{code}/run")]
    public async Task<IActionResult> RunNow(string code, CancellationToken cancellationToken = default) =>
        MapResult(await DispatchAsync(new RunAutomatedTaskNowRequest(code), cancellationToken), viewModel);

    [HttpGet("{code}/runs")]
    public async Task<IActionResult> History(
        string code,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = Paging.DefaultPageSize,
        CancellationToken cancellationToken = default) =>
        MapResult(await DispatchAsync(new GetAutomatedTaskHistoryRequest(code, page, pageSize), cancellationToken), viewModel);
}

public sealed record SetAutomatedTaskEnabledBody(bool IsEnabled);
