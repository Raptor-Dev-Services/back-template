using Application.UseCases.Users.CreateUser;
using Application.UseCases.Users.CreateUser.Responses;
using Application.UseCases.Users.DisableUser;
using Application.UseCases.Users.DisableUser.Responses;
using Application.UseCases.Users.GetUser;
using Application.UseCases.Users.GetUser.Responses;
using Application.UseCases.Users.GetUsers;
using Application.UseCases.Users.UpdateUser;
using Application.UseCases.Users.UpdateUser.Responses;
using Common.Messaging;
using Common.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using WebApi.Base;
using WebApi.EndPoints.Users.RequestBodies;

namespace WebApi.EndPoints.Users;

[Route("api/users")]
[Authorize]
public sealed class UsersController : BaseApiController
{
    private readonly ILogger<UsersController>        _logger;
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

    private long CurrentBranchId =>
        long.TryParse(User.FindFirstValue("branch_id"), out var id) ? id : 0;

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page     = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        try
        {
            _ = await Mediator.Send(new GetUsersRequest(CurrentTenantId, page, pageSize), ct);
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
            var result = await Mediator.Send(new GetUserRequest(id, CurrentTenantId), ct);
            if (_viewModel.IsSuccess) return Ok(_viewModel);
            return result is GetUserNotFoundFailure
                ? NotFound(_viewModel)
                : StatusCode(500, _viewModel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en GetById User");
            var inner = ex;
            while (inner.InnerException != null) inner = inner.InnerException!;
            return StatusCode(500, _viewModel.Fail(inner.Message));
        }
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create([FromBody] CreateUserBody body, CancellationToken ct = default)
    {
        try
        {
            var result = await Mediator.Send(
                new CreateUserRequest(CurrentTenantId, CurrentBranchId, body.FullName, body.Email, body.Password, body.Role), ct);
            if (_viewModel.IsSuccess) return Ok(_viewModel);
            return result is CreateUserEmailConflictFailure
                ? Conflict(_viewModel)
                : StatusCode(500, _viewModel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en Create User");
            var inner = ex;
            while (inner.InnerException != null) inner = inner.InnerException!;
            return StatusCode(500, _viewModel.Fail(inner.Message));
        }
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateUserBody body, CancellationToken ct = default)
    {
        try
        {
            var result = await Mediator.Send(new UpdateUserRequest(id, CurrentTenantId, body.FullName), ct);
            if (_viewModel.IsSuccess) return Ok(_viewModel);
            return result is UpdateUserNotFoundFailure
                ? NotFound(_viewModel)
                : StatusCode(500, _viewModel);
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
            var result = await Mediator.Send(new DisableUserRequest(id, CurrentTenantId), ct);
            if (_viewModel.IsSuccess) return Ok(_viewModel);
            return result is DisableUserNotFoundFailure
                ? NotFound(_viewModel)
                : StatusCode(500, _viewModel);
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
