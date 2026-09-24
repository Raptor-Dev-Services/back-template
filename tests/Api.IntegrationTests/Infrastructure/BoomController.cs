using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.IntegrationTests.Infrastructure;

/// <summary>
/// Solo existe en las pruebas (se agrega como application part en <see cref="ApiFactory"/>): lanza una excepcion
/// con el tipo de texto que antes llegaba al cliente -nombre de indice, host de la base- para comprobar que el
/// filtro global responde 500 generico y no lo filtra.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("test/boom")]
public sealed class BoomController : ControllerBase
{
    public const string InternalDetail = "23505: duplicate key value violates unique constraint \"UX_UserCredential_Email\" Host=db-interna";

    [HttpGet]
    public IActionResult Get() => throw new InvalidOperationException("envoltorio", new InvalidOperationException(InternalDetail));
}
