using Common.Messaging;
using Common.Results;
using Common.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Shared.Kernel.Security;
using Shared.Web.Errors;

namespace Shared.Web;

/// <summary>
/// Base de todos los controllers. Un controller solo arma el <c>Request</c>, lo despacha y traduce el
/// <c>Response</c> a HTTP: nada de logica de negocio, nada de <c>try/catch</c>.
///
/// <para><b>Flujo.</b> <see cref="DispatchAsync{TResponse}"/> manda el request por el mediador de Common; el
/// <c>InteractorPipeline</c> ejecuta el handler y publica su <c>Response</c>, que el presenter del caso de
/// uso vuelca en el <see cref="ResultViewModel{T}"/> del controller. <see cref="MapResult{T}"/> devuelve ese
/// envelope con el status que corresponde al tipo de fallo (tabla unica en <see cref="FailureStatusCodes"/>).</para>
///
/// <para><b>Errores.</b> No se capturan excepciones aqui. Una de negocio la traduce el filtro global; una
/// inesperada sale como 500 generico y su detalle va al log, nunca al cliente.</para>
/// </summary>
[ApiController]
public abstract class BaseApiController(IMediator mediator) : ControllerBase
{
    protected IMediator Mediator { get; } = mediator;

    /// <summary>Despacha un caso de uso por el mediador (y su pipeline).</summary>
    protected Task<TResponse> DispatchAsync<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken) =>
        Mediator.Send(request, cancellationToken);

    /// <summary>
    /// Traduce el response de un caso de uso al envelope con su status: 200 si es exito, el de la tabla
    /// si es fallo. El envelope ya lo lleno el presenter.
    /// </summary>
    protected IActionResult MapResult<T>(IResponse response, ResultViewModel<T> viewModel) =>
        response is IFailure failure
            ? StatusCode(FailureStatusCodes.For(failure), viewModel)
            : Ok(viewModel);

    /// <summary>Tenant del usuario autenticado, del claim verificado del JWT (nunca del body).</summary>
    protected long CurrentTenantId =>
        long.TryParse(User.FindFirst(AppClaimTypes.TenantId)?.Value, out var id) ? id : 0L;

    /// <summary>PublicId del usuario autenticado, del claim <c>sub</c>.</summary>
    protected Guid CurrentUserPublicId =>
        Guid.TryParse(User.FindFirst(AppClaimTypes.Subject)?.Value, out var id) ? id : Guid.Empty;

    /// <summary>IP del cliente tal como la ve el servidor (la real si ForwardedHeaders esta configurado).</summary>
    protected string? ClientIp => HttpContext.Connection.RemoteIpAddress?.ToString();
}
