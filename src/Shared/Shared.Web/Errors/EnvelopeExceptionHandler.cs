using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Shared.Web.Errors;

/// <summary>
/// Ultima red para las excepciones que NO pasan por MVC -las que lanza un middleware (el guard de tenant,
/// el propio pipeline de auth), un endpoint minimal o un health check- y que por tanto el
/// <see cref="BusinessExceptionFilter"/> nunca ve. Mismo criterio que el filtro: excepcion de negocio con
/// su status y su mensaje; cualquier otra, 500 generico y la excepcion al log.
///
/// Sustituye al <c>UseCoreProblemDetails</c> de Common, que respondia <c>application/problem+json</c>: con
/// los dos, la API tenia DOS formatos de error y el cliente tenia que conocer ambos.
/// </summary>
public sealed class EnvelopeExceptionHandler(ILogger<EnvelopeExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is OperationCanceledException && context.RequestAborted.IsCancellationRequested)
        {
            logger.LogDebug("Peticion cancelada por el cliente: {Path}", context.Request.Path);
            if (!context.Response.HasStarted)
                context.Response.StatusCode = BusinessExceptionFilter.StatusClientClosedRequest;
            return true;
        }

        var status = FailureStatusCodes.For(exception);
        if (status is null)
        {
            logger.LogError(exception, "Error inesperado fuera de MVC en {Method} {Path}.",
                context.Request.Method, context.Request.Path);
            await ApiEnvelope.WriteFailureAsync(context, StatusCodes.Status500InternalServerError,
                ApiEnvelope.DefaultMessageFor(StatusCodes.Status500InternalServerError));
            return true;
        }

        await ApiEnvelope.WriteFailureAsync(context, status.Value, exception.Message);
        return true;
    }
}
