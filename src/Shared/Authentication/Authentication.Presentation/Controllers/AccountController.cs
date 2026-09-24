using Authentication.Application.UseCases.ChangePassword;
using Authentication.Application.UseCases.GetMyAccount;
using Authentication.Presentation.RequestBodies;
using Common.Messaging;
using Common.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Web;

namespace Authentication.Presentation.Controllers;

/// <summary>
/// Autoservicio sobre la PROPIA cuenta. Solo exige sesion, no un permiso: cada quien opera sobre lo suyo,
/// resuelto por el <c>sub</c> del token (regla access-control). Esta en la lista justificada de la prueba de
/// superficie de autorizacion.
/// </summary>
[Route("api/v1/account")]
[Authorize]
public sealed class AccountController(IMediator mediator, ResultViewModel<AccountController> viewModel) : BaseApiController(mediator)
{
    /// <summary>La cuenta propia con roles y permisos vigentes.</summary>
    [HttpGet("me")]
    public async Task<IActionResult> Me(CancellationToken cancellationToken = default) =>
        MapResult(await DispatchAsync(new GetMyAccountRequest(CurrentUserPublicId), cancellationToken), viewModel);

    /// <summary>Cambia la contrasena propia (exige la actual). Cierra todas las sesiones, incluida esta.</summary>
    [HttpPost("password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordBody body, CancellationToken cancellationToken = default) =>
        MapResult(await DispatchAsync(
            new ChangePasswordRequest(CurrentUserPublicId, body.CurrentPassword, body.NewPassword), cancellationToken), viewModel);
}
