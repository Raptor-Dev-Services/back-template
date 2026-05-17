using Application.UseCases.Auth.Login;
using Application.UseCases.Auth.Login.Responses;
using Application.UseCases.Auth.RefreshToken;
using Application.UseCases.Auth.RefreshToken.Responses;
using Common.Messaging;
using Common.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using WebApi.Base;
using WebApi.EndPoints.Auth.RequestBodies;

namespace WebApi.EndPoints.Auth;

[Route("api/auth")]
[AllowAnonymous]
public sealed class AuthController : BaseApiController
{
    private readonly ILogger<AuthController>     _logger;
    private readonly ResultViewModel<AuthController> _viewModel;

    public AuthController(
        IMediator mediator,
        ILogger<AuthController> logger,
        ResultViewModel<AuthController> viewModel) : base(mediator)
    {
        _logger    = logger;
        _viewModel = viewModel;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginBody body, CancellationToken ct = default)
    {
        try
        {
            var result = await Mediator.Send(new LoginRequest(body.Email, body.Password), ct);
            if (_viewModel.IsSuccess) return Ok(_viewModel);
            return result is LoginInvalidCredentialsFailure
                ? Unauthorized(_viewModel)
                : StatusCode(500, _viewModel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en Login");
            var inner = ex;
            while (inner.InnerException != null) inner = inner.InnerException!;
            return StatusCode(500, _viewModel.Fail(inner.Message));
        }
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenBody body, CancellationToken ct = default)
    {
        try
        {
            var result = await Mediator.Send(new RefreshTokenRequest(body.Token), ct);
            if (_viewModel.IsSuccess) return Ok(_viewModel);
            return result is RefreshTokenInvalidFailure
                ? Unauthorized(_viewModel)
                : StatusCode(500, _viewModel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en Refresh");
            var inner = ex;
            while (inner.InnerException != null) inner = inner.InnerException!;
            return StatusCode(500, _viewModel.Fail(inner.Message));
        }
    }
}
