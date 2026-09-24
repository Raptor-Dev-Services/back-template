using Common.ViewModels;
using Microsoft.AspNetCore.Http;

namespace Shared.Web.Errors;

/// <summary>
/// El envelope UNICO de la API: <c>{ data, isSuccess, message, utcTimeStamp }</c>, que es exactamente la
/// forma de <see cref="ResultViewModel{T}"/> de Common. Los endpoints lo devuelven via su presenter; este
/// helper lo arma para las respuestas que NO pasan por un presenter: el filtro de excepciones, la
/// validacion del modelo (400), el limitador de tasa (429), el guard de tenant suspendido (403) y las
/// paginas de estado (401/403/404 sin cuerpo).
///
/// Se construye con el MISMO tipo de Common a proposito, no con un record propio de igual forma: dos
/// tipos "iguales" divergen el dia que alguien agrega un campo a uno solo, y el cliente vuelve a tener
/// que conocer dos formatos de error.
/// </summary>
public static class ApiEnvelope
{
    /// <summary>Envelope de fallo con el mensaje indicado.</summary>
    public static ResultViewModel<object> Fail(string message) => new ResultViewModel<object>().Fail(message);

    /// <summary>Escribe un envelope de fallo con su status. No hace nada si la respuesta ya empezo.</summary>
    public static async Task WriteFailureAsync(HttpContext context, int statusCode, string message)
    {
        if (context.Response.HasStarted)
            return;

        context.Response.StatusCode = statusCode;
        await context.Response.WriteAsJsonAsync(Fail(message), context.RequestAborted);
    }

    /// <summary>
    /// Mensaje generico por status para las respuestas que llegan vacias (401 del esquema JWT, 403 de
    /// una policy, 404 de una ruta que no existe). Nunca incluye el motivo tecnico.
    /// </summary>
    public static string DefaultMessageFor(int statusCode) => statusCode switch
    {
        StatusCodes.Status400BadRequest => "La peticion no tiene un formato valido.",
        StatusCodes.Status401Unauthorized => "Necesitas iniciar sesion para acceder a este recurso.",
        StatusCodes.Status403Forbidden => "No tienes permiso para realizar esta accion.",
        StatusCodes.Status404NotFound => "El recurso solicitado no existe.",
        StatusCodes.Status405MethodNotAllowed => "Metodo HTTP no permitido para este recurso.",
        StatusCodes.Status413PayloadTooLarge => "El cuerpo de la peticion es demasiado grande.",
        StatusCodes.Status415UnsupportedMediaType => "Tipo de contenido no soportado.",
        StatusCodes.Status429TooManyRequests => "Demasiadas solicitudes. Espera un momento e intenta de nuevo.",
        _ when statusCode >= 500 => "Ha ocurrido un error inesperado.",
        _ => "La peticion no pudo completarse.",
    };
}
