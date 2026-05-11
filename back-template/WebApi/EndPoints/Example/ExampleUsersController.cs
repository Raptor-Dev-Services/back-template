using Application.UseCases.Example.DisableExampleUser;
using Application.UseCases.Example.GetExampleUser;
using Application.UseCases.Example.GetExampleUsers;
using Application.UseCases.Example.InsertExampleUser;
using Application.UseCases.Example.UpdateExampleUser;
using Common.Messaging;
using Common.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using WebApi.Base;
using WebApi.EndPoints.Example.RequestBodies;

namespace WebApi.EndPoints.Example;

[Route("api/example/users")]
[Authorize]
public sealed class ExampleUsersController : BaseApiController
{
    private readonly ILogger<ExampleUsersController> _logger;
    private readonly ResultViewModel<ExampleUsersController> _viewModel;

    public ExampleUsersController(
        IMediator mediator,
        ILogger<ExampleUsersController> logger,
        ResultViewModel<ExampleUsersController> viewModel)
        : base(mediator)
    {
        _logger    = logger;
        _viewModel = viewModel;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page     = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        try
        {
            _ = await Mediator.Send(new GetExampleUsersRequest(page, pageSize), ct);
            return _viewModel.IsSuccess ? Ok(_viewModel) : StatusCode(500, _viewModel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en GetAll ExampleUsers");
            var innerEx = ex;
            while (innerEx.InnerException != null) innerEx = innerEx.InnerException!;
            return StatusCode(500, _viewModel.Fail(innerEx.Message));
        }
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct = default)
    {
        try
        {
            _ = await Mediator.Send(new GetExampleUserRequest(id), ct);
            return _viewModel.IsSuccess ? Ok(_viewModel) : StatusCode(500, _viewModel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en GetById ExampleUser");
            var innerEx = ex;
            while (innerEx.InnerException != null) innerEx = innerEx.InnerException!;
            return StatusCode(500, _viewModel.Fail(innerEx.Message));
        }
    }

    [HttpPost]
    public async Task<IActionResult> Insert([FromBody] InsertExampleUserBody body, CancellationToken ct = default)
    {
        try
        {
            _ = await Mediator.Send(new InsertExampleUserRequest(body.FullName, body.Email, body.Department, body.Notes), ct);
            return _viewModel.IsSuccess ? Ok(_viewModel) : StatusCode(500, _viewModel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en Insert ExampleUser");
            var innerEx = ex;
            while (innerEx.InnerException != null) innerEx = innerEx.InnerException!;
            return StatusCode(500, _viewModel.Fail(innerEx.Message));
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateExampleUserBody body, CancellationToken ct = default)
    {
        try
        {
            _ = await Mediator.Send(new UpdateExampleUserRequest(id, body.FullName, body.Department, body.Notes), ct);
            return _viewModel.IsSuccess ? Ok(_viewModel) : StatusCode(500, _viewModel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en Update ExampleUser");
            var innerEx = ex;
            while (innerEx.InnerException != null) innerEx = innerEx.InnerException!;
            return StatusCode(500, _viewModel.Fail(innerEx.Message));
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Disable(Guid id, CancellationToken ct = default)
    {
        try
        {
            _ = await Mediator.Send(new DisableExampleUserRequest(id), ct);
            return _viewModel.IsSuccess ? Ok(_viewModel) : StatusCode(500, _viewModel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en Disable ExampleUser");
            var innerEx = ex;
            while (innerEx.InnerException != null) innerEx = innerEx.InnerException!;
            return StatusCode(500, _viewModel.Fail(innerEx.Message));
        }
    }
}
