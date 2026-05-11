using Common.Messaging;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Base;

/// <summary>
/// Clase base para todos los controllers de la API.
/// Expone el <see cref="IMediator"/> de <c>Common.Messaging</c> a las clases derivadas.
/// </summary>
[ApiController]
public abstract class BaseApiController : ControllerBase
{
    /// <summary>Mediator para despachar requests a sus handlers.</summary>
    protected readonly IMediator Mediator;

    /// <summary>
    /// Inicializa una nueva instancia de <see cref="BaseApiController"/>.
    /// </summary>
    /// <param name="mediator">Mediator inyectado por el contenedor DI.</param>
    protected BaseApiController(IMediator mediator) => Mediator = mediator;
}
