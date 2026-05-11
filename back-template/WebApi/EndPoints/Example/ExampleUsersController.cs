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

/// <summary>
/// Endpoints CRUD del módulo Example.
/// Despacha cada acción al mediator y retorna el resultado vía <see cref="ResultViewModel{T}"/>.
/// </summary>
[Route("api/example/users")]
// [Authorize]
public sealed class ExampleUsersController : BaseApiController
{
    private readonly ILogger<ExampleUsersController> _logger;
    private readonly ResultViewModel<ExampleUsersController> _viewModel;

    /// <summary>
    /// Inicializa una nueva instancia de <see cref="ExampleUsersController"/>.
    /// </summary>
    /// <param name="mediator">Mediator de <c>Common.Messaging</c>.</param>
    /// <param name="logger">Logger para errores del controller.</param>
    /// <param name="viewModel">ViewModel scoped compartido con los presenters.</param>
    public ExampleUsersController(
        IMediator mediator,
        ILogger<ExampleUsersController> logger,
        ResultViewModel<ExampleUsersController> viewModel)
        : base(mediator)
    {
        _logger    = logger;
        _viewModel = viewModel;
    }

    /// <summary>
    /// Retorna una página de usuarios.
    /// </summary>
    /// <param name="page">Número de página (base 1, por defecto 1).</param>
    /// <param name="pageSize">Registros por página (por defecto 20).</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>200 con la página de usuarios, o 500 en caso de error.</returns>
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

    /// <summary>
    /// Retorna un usuario por su identificador público.
    /// </summary>
    /// <param name="id">Identificador público (UUID) del usuario.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>200 con el usuario, 404 si no existe, o 500 en caso de error.</returns>
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

    /// <summary>
    /// Crea un nuevo usuario.
    /// </summary>
    /// <param name="body">Datos del usuario a crear.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>200 con el usuario creado, 409 si el correo ya existe, o 500 en caso de error.</returns>
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

    /// <summary>
    /// Actualiza los campos editables de un usuario existente.
    /// </summary>
    /// <param name="id">Identificador público del usuario a actualizar.</param>
    /// <param name="body">Nuevos valores para los campos editables.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>200 si fue exitoso, 404 si no existe, o 500 en caso de error.</returns>
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

    /// <summary>
    /// Desactiva (soft-delete) un usuario.
    /// </summary>
    /// <param name="id">Identificador público del usuario a desactivar.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>200 si fue exitoso, 404 si no existe, o 500 en caso de error.</returns>
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
