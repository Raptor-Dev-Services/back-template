using Authentication.Application.UseCases.InviteUser;
using Authentication.Application.UseCases.SetUserLock;
using Authentication.Presentation.RequestBodies;
using Common.Messaging;
using Common.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Kernel.Security;
using Shared.Web;
using Shared.Web.Authorization;

namespace Authentication.Presentation.Controllers;

/// <summary>
/// Administracion de CUENTAS (credenciales) del tenant del administrador. El perfil (nombre, baja) vive en el
/// modulo Users, en <c>/api/v1/users</c>.
/// </summary>
[Route("api/v1/accounts")]
[Authorize(Policy = PermissionPolicy.Prefix + KnownPermissions.UsersManage)]
public sealed class AccountsController(IMediator mediator, ResultViewModel<AccountsController> viewModel) : BaseApiController(mediator)
{
    /// <summary>Invita a un usuario: lo da de alta sin contrasena y le manda un enlace para fijarla.</summary>
    [HttpPost]
    public async Task<IActionResult> Invite([FromBody] InviteUserBody body, CancellationToken cancellationToken = default) =>
        MapResult(await DispatchAsync(
            new InviteUserRequest(CurrentTenantId, CurrentUserPublicId, body.Email, body.FullName, body.RoleCodes), cancellationToken), viewModel);

    /// <summary>Bloquea la cuenta: no inicia sesion ni renueva, y sus sesiones se revocan en el acto.</summary>
    [HttpPost("{id:guid}/lock")]
    public async Task<IActionResult> Lock(Guid id, CancellationToken cancellationToken = default) =>
        MapResult(await DispatchAsync(new SetUserLockRequest(CurrentUserPublicId, id, Locked: true), cancellationToken), viewModel);

    [HttpPost("{id:guid}/unlock")]
    public async Task<IActionResult> Unlock(Guid id, CancellationToken cancellationToken = default) =>
        MapResult(await DispatchAsync(new SetUserLockRequest(CurrentUserPublicId, id, Locked: false), cancellationToken), viewModel);
}
