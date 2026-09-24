using Authentication.Application.UseCases.BootstrapTenant;
using Authentication.Presentation.RequestBodies;
using Common.Messaging;
using Common.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Shared.Web;

namespace Authentication.Presentation.Controllers;

/// <summary>
/// Aprovisiona un tenant y su PRIMER administrador. Anonimo por JWT (todavia no hay usuario) pero gateado por
/// el secreto <c>Bootstrap:Secret</c>, que viaja en la cabecera <c>X-Bootstrap-Secret</c>: 403 si el bootstrap
/// esta apagado, 401 si el secreto no coincide, 409 si el tenant ya tiene usuarios.
///
/// Un endpoint anonimo protegido por un secreto ESTATICO es superficie de fuerza bruta igual que una
/// contrasena: lleva la misma politica de tasa estricta que el login.
/// </summary>
[Route("api/v1/bootstrap")]
[AllowAnonymous]
[EnableRateLimiting(RateLimitPolicies.Auth)]
public sealed class BootstrapController(IMediator mediator, ResultViewModel<BootstrapController> viewModel) : BaseApiController(mediator)
{
    public const string SecretHeader = "X-Bootstrap-Secret";

    [HttpPost("tenant")]
    public async Task<IActionResult> BootstrapTenant([FromBody] BootstrapTenantBody body, CancellationToken cancellationToken = default)
    {
        var secret = Request.Headers[SecretHeader].ToString();
        var request = new BootstrapTenantRequest(
            secret, body.TenantName, body.TenantSlug, body.AdminEmail, body.AdminPassword, body.AdminFullName);

        return MapResult(await DispatchAsync(request, cancellationToken), viewModel);
    }
}
