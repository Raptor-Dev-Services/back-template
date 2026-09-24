using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Shared.Web.Errors;

namespace Host.Api.Extensions;

/// <summary>
/// Todo lo que hace que la API tenga UN solo formato de error, el envelope
/// <c>{ data, isSuccess, message, utcTimeStamp }</c>, venga de donde venga el fallo:
///
/// <list type="bullet">
///   <item>una accion que lanza -> <see cref="BusinessExceptionFilter"/> (filtro global de MVC);</item>
///   <item>el modelo que no enlaza (JSON roto, tipo equivocado) -> <c>InvalidModelStateResponseFactory</c>,
///   que responde 400 SIN el detalle del parser (exponia la posicion del byte y el token que fallo);</item>
///   <item>un middleware o un endpoint minimal que lanza -> <see cref="EnvelopeExceptionHandler"/>;</item>
///   <item>una respuesta que sale vacia (401 del esquema JWT, 403 de una policy, 404 de una ruta que no
///   existe, 405) -> paginas de estado que escriben el envelope.</item>
/// </list>
/// </summary>
public static class ApiErrorExtensions
{
    public static IMvcBuilder AddApiErrorHandling(this IMvcBuilder mvc)
    {
        mvc.Services.AddExceptionHandler<EnvelopeExceptionHandler>();

        return mvc
            .AddMvcOptions(options => options.Filters.Add<BusinessExceptionFilter>())
            .ConfigureApiBehaviorOptions(options =>
                options.InvalidModelStateResponseFactory = _ =>
                    new ObjectResult(ApiEnvelope.Fail(ApiEnvelope.DefaultMessageFor(StatusCodes.Status400BadRequest)))
                    {
                        StatusCode = StatusCodes.Status400BadRequest,
                    });
    }

    public static WebApplication UseApiErrorHandling(this WebApplication app)
    {
        // El delegado solo corre si ningun IExceptionHandler manejo la excepcion: es la red de la red.
        app.UseExceptionHandler(new ExceptionHandlerOptions
        {
            ExceptionHandler = context => ApiEnvelope.WriteFailureAsync(
                context,
                StatusCodes.Status500InternalServerError,
                ApiEnvelope.DefaultMessageFor(StatusCodes.Status500InternalServerError)),
        });

        // Solo actua si la respuesta llego sin cuerpo. 499 queda fuera: el cliente ya no esta.
        app.UseStatusCodePages(context =>
        {
            var status = context.HttpContext.Response.StatusCode;
            if (status == BusinessExceptionFilter.StatusClientClosedRequest)
                return Task.CompletedTask;

            return ApiEnvelope.WriteFailureAsync(context.HttpContext, status, ApiEnvelope.DefaultMessageFor(status));
        });

        return app;
    }
}
