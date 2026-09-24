using Authentication.Application.UseCases.ChangePassword;
using Authentication.Application.UseCases.GetMyAccount;
using Authentication.Application.UseCases.TwoFactor;
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

    /// <summary>2FA, paso 1: genera un secreto pendiente y la URI otpauth para el QR. No activa nada todavia.</summary>
    [HttpPost("2fa/setup")]
    public async Task<IActionResult> BeginTwoFactorSetup(CancellationToken cancellationToken = default) =>
        MapResult(await DispatchAsync(new BeginTwoFactorSetupRequest(CurrentUserPublicId), cancellationToken), viewModel);

    /// <summary>2FA, paso 2: confirma con un codigo de la app, lo activa y devuelve los codigos de recuperacion UNA vez.</summary>
    [HttpPost("2fa/enable")]
    public async Task<IActionResult> EnableTwoFactor([FromBody] TwoFactorCodeBody body, CancellationToken cancellationToken = default) =>
        MapResult(await DispatchAsync(new EnableTwoFactorRequest(CurrentUserPublicId, body.Code), cancellationToken), viewModel);

    /// <summary>Apaga el 2FA. Exige un codigo vigente (TOTP o de recuperacion): la sesion sola no basta.</summary>
    [HttpPost("2fa/disable")]
    public async Task<IActionResult> DisableTwoFactor([FromBody] TwoFactorCodeBody body, CancellationToken cancellationToken = default) =>
        MapResult(await DispatchAsync(new DisableTwoFactorRequest(CurrentUserPublicId, body.Code), cancellationToken), viewModel);
}
