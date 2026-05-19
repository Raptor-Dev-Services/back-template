using Authentication.Application.UseCases.Login;
using Authentication.Application.UseCases.Login.Responses;
using Authentication.Application.UseCases.RefreshToken;
using Authentication.Application.UseCases.RefreshToken.Responses;
using Authentication.Application.UseCases.Register;
using Authentication.Application.UseCases.Register.Responses;
using Authentication.Presentation.RequestBodies;
using Common.Messaging;
using Common.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Shared.Web;

namespace Authentication.Presentation;

[Route("api/auth")]
[AllowAnonymous]
public sealed class AuthController : BaseApiController
{
    private readonly ILogger<AuthController>         _logger;
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

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterBody body, CancellationToken ct = default)
    {
        try
        {
            var result = await Mediator.Send(
                new RegisterRequest(body.Email, body.Password, body.TenantId, body.BranchId, body.FullName, body.Role), ct);
            if (_viewModel.IsSuccess) return Ok(_viewModel);
            if (result is RegisterEmailConflictFailure) return Conflict(_viewModel);
            if (result is RegisterTenantNotFoundFailure) return NotFound(_viewModel);
            return StatusCode(500, _viewModel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en Register");
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
