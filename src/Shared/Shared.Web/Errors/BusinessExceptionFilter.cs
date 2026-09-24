using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;

namespace Shared.Web.Errors;

/// <summary>
/// Filtro global de MVC que traduce las excepciones que escapan de una accion al envelope unico.
///
/// <list type="bullet">
///   <item>Excepcion de negocio (<see cref="Shared.Kernel.Errors.BusinessException"/> o la
///   <c>BusinessRuleException</c> de Common): su status (ver <see cref="FailureStatusCodes"/>) y SU mensaje,
///   que esta escrito para quien opera.</item>
///   <item>Cualquier otra: 500 con un mensaje GENERICO y la excepcion completa al log. Nunca se fuga el
///   mensaje interno ni el stack trace: antes cada controller devolvia <c>inner.Message</c> al cliente, y
///   eso incluia textos de Npgsql con nombres de tabla, de restriccion y valores de otros tenants.</item>
///   <item>Cancelacion del cliente (se fue a otra pantalla): 499 y nivel Debug. No es un error nuestro.</item>
///   <item>Respuesta ya empezada (un stream, una descarga): no se puede escribir el envelope ni cambiar el
///   status; se registra y se cierra.</item>
/// </list>
/// </summary>
public sealed class BusinessExceptionFilter(ILogger<BusinessExceptionFilter> logger) : IExceptionFilter
{
    /// <summary>
    /// 499 (Client Closed Request). No es estandar de la IANA, es la convencion de nginx, y describe
    /// exactamente lo que paso: el cliente cerro la conexion. En la practica nadie lo lee.
    /// </summary>
    public const int StatusClientClosedRequest = 499;

    public void OnException(ExceptionContext context)
    {
        var http = context.HttpContext;

        // La respuesta YA EMPEZO: fijar context.Result haria que MVC intente cambiar el status de una
        // respuesta iniciada, que lanza InvalidOperationException y deja el stream cortado a medias.
        if (http.Response.HasStarted)
        {
            if (IsClientCancellation(context))
                logger.LogDebug("Respuesta cortada por el cliente: {Path}", http.Request.Path);
            else
                logger.LogError(context.Exception, "Error despues de empezar la respuesta en {Path}.", http.Request.Path);

            context.ExceptionHandled = true;
            return;
        }

        // Solo es "del cliente" si el token que se disparo es el del REQUEST. Una cancelacion por un
        // timeout interno si es nuestra y debe seguir siendo un 500 ruidoso.
        if (IsClientCancellation(context))
        {
            logger.LogDebug("Peticion cancelada por el cliente: {Path}", http.Request.Path);
            context.Result = new StatusCodeResult(StatusClientClosedRequest);
            context.ExceptionHandled = true;
            return;
        }

        var status = FailureStatusCodes.For(context.Exception);
        string message;
        if (status is null)
        {
            logger.LogError(context.Exception, "Error inesperado no manejado en {Method} {Path}.",
                http.Request.Method, http.Request.Path);
            status = StatusCodes.Status500InternalServerError;
            message = ApiEnvelope.DefaultMessageFor(status.Value);
        }
        else
        {
            message = context.Exception.Message;
        }

        context.Result = new ObjectResult(ApiEnvelope.Fail(message)) { StatusCode = status };
        context.ExceptionHandled = true;
    }

    private static bool IsClientCancellation(ExceptionContext context) =>
        context.Exception is OperationCanceledException
        && context.HttpContext.RequestAborted.IsCancellationRequested;
}
