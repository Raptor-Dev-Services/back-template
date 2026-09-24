using Authentication.Application.UseCases.Login;
using Authentication.Application.UseCases.RefreshToken;
using Authentication.Application.UseCases.Register;
using Authentication.Presentation.RequestBodies;
using Common.Messaging;
using Common.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Web;

namespace Authentication.Presentation;

[Route("api/v1/auth")]
[AllowAnonymous]
public sealed class AuthController(IMediator mediator, ResultViewModel<AuthController> viewModel)
    : BaseApiController(mediator)
{
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginBody body, CancellationToken cancellationToken = default)
    {
        var response = await DispatchAsync(new LoginRequest(body.Email, body.Password), cancellationToken);
        return MapResult(response, viewModel);
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterBody body, CancellationToken cancellationToken = default)
    {
        var response = await DispatchAsync(
            new RegisterRequest(body.Email, body.Password, body.TenantId, body.FullName, body.Role), cancellationToken);
        return MapResult(response, viewModel);
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenBody body, CancellationToken cancellationToken = default)
    {
        var response = await DispatchAsync(new RefreshTokenRequest(body.Token), cancellationToken);
        return MapResult(response, viewModel);
    }
}
