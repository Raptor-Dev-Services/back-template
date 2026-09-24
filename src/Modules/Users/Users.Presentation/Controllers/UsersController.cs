using Common.Messaging;
using Common.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Shared.Web;
using System.Security.Claims;
using Users.Application.UseCases.DisableUserProfile;
using Users.Application.UseCases.DisableUserProfile.Responses;
using Users.Application.UseCases.GetUserProfile;
using Users.Application.UseCases.GetUserProfile.Responses;
using Users.Application.UseCases.GetUserProfiles;
using Users.Application.UseCases.UpdateUserProfile;
using Users.Application.UseCases.UpdateUserProfile.Responses;
using Users.Presentation.RequestBodies;

namespace Users.Presentation;

[Route("api/users")]
[Authorize]
public sealed class UsersController : BaseApiController
{
    private readonly ILogger<UsersController>         _logger;
    private readonly ResultViewModel<UsersController> _viewModel;

    public UsersController(
        IMediator mediator,
        ILogger<UsersController> logger,
        ResultViewModel<UsersController> viewModel) : base(mediator)
    {
        _logger    = logger;
        _viewModel = viewModel;
    }

    private long CurrentTenantId =>
        long.TryParse(User.FindFirstValue("tenant_id"), out var id) ? id : 0;

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page     = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        try
        {
            _ = await Mediator.Send(new GetUserProfilesRequest(CurrentTenantId, page, pageSize), ct);
            return _viewModel.IsSuccess ? Ok(_viewModel) : StatusCode(500, _viewModel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en GetAll Users");
            var inner = ex;
            while (inner.InnerException != null) inner = inner.InnerException!;
            return StatusCode(500, _viewModel.Fail(inner.Message));
        }
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct = default)
    {
        try
        {
            var result = await Mediator.Send(new GetUserProfileRequest(id, CurrentTenantId), ct);
            if (_viewModel.IsSuccess) return Ok(_viewModel);
            return result is GetUserProfileNotFoundFailure ? NotFound(_viewModel) : StatusCode(500, _viewModel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en GetById User");
            var inner = ex;
            while (inner.InnerException != null) inner = inner.InnerException!;
            return StatusCode(500, _viewModel.Fail(inner.Message));
        }
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateUserProfileBody body, CancellationToken ct = default)
    {
        try
        {
            var result = await Mediator.Send(new UpdateUserProfileRequest(id, CurrentTenantId, body.FullName), ct);
            if (_viewModel.IsSuccess) return Ok(_viewModel);
            return result is UpdateUserProfileNotFoundFailure ? NotFound(_viewModel) : StatusCode(500, _viewModel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en Update User");
            var inner = ex;
            while (inner.InnerException != null) inner = inner.InnerException!;
            return StatusCode(500, _viewModel.Fail(inner.Message));
        }
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Disable(Guid id, CancellationToken ct = default)
    {
        try
        {
            var result = await Mediator.Send(new DisableUserProfileRequest(id, CurrentTenantId), ct);
            if (_viewModel.IsSuccess) return Ok(_viewModel);
            return result is DisableUserProfileNotFoundFailure ? NotFound(_viewModel) : StatusCode(500, _viewModel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en Disable User");
            var inner = ex;
            while (inner.InnerException != null) inner = inner.InnerException!;
            return StatusCode(500, _viewModel.Fail(inner.Message));
        }
    }
}
