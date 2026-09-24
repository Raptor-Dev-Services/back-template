using Common.Results;
using Microsoft.AspNetCore.Http;
using Shared.Kernel.Errors;
using Shared.Kernel.Results;

namespace Shared.Web.Errors;

/// <summary>
/// Tabla UNICA de status HTTP por tipo de fallo. La usan las dos vias por las que un caso de uso puede
/// fallar -devolver un <see cref="IFailure"/> tipado o lanzar una <see cref="BusinessException"/>- para
/// que el mismo fallo responda siempre lo mismo, sin importar por cual salio (regla backend-architecture:
/// "no mezclar criterios dentro de un mismo API").
///
/// <list type="table">
///   <item><term>400</term><description><see cref="IBadRequestFailure"/> / <see cref="BadRequestException"/>, <see cref="IValidationFailure"/> / <see cref="ValidationException"/>, cualquier <c>BusinessRuleException</c> de Common y el modelo malformado (ADR-0007).</description></item>
///   <item><term>401</term><description><see cref="IUnauthorizedFailure"/> / <see cref="UnauthorizedException"/>.</description></item>
///   <item><term>403</term><description><see cref="IForbiddenFailure"/> / <see cref="ForbiddenException"/>.</description></item>
///   <item><term>404</term><description><see cref="INotFoundFailure"/> / <see cref="NotFoundException"/>.</description></item>
///   <item><term>409</term><description><see cref="IConflictFailure"/> / <see cref="ConflictException"/>.</description></item>
/// </list>
///
/// Un <see cref="IFailure"/> sin ninguna de esas marcas cae a 400: es un fallo de negocio, no un error del
/// servidor, pero quien lo declaro no dijo cual. Conviene marcarlo.
/// </summary>
public static class FailureStatusCodes
{
    public static int For(IFailure failure) => failure switch
    {
        IBadRequestFailure => StatusCodes.Status400BadRequest,
        IUnauthorizedFailure => StatusCodes.Status401Unauthorized,
        IForbiddenFailure => StatusCodes.Status403Forbidden,
        INotFoundFailure => StatusCodes.Status404NotFound,
        IConflictFailure => StatusCodes.Status409Conflict,
        IValidationFailure => StatusCodes.Status400BadRequest,
        _ => StatusCodes.Status400BadRequest,
    };

    /// <summary>
    /// Status de una excepcion, o <c>null</c> si no es de negocio (entonces es un 500 y su mensaje NO se
    /// expone). Los tipos concretos van antes que la base <see cref="Common.Exceptions.BusinessRuleException"/>.
    /// </summary>
    public static int? For(Exception exception) => exception switch
    {
        BadRequestException => StatusCodes.Status400BadRequest,
        UnauthorizedException => StatusCodes.Status401Unauthorized,
        ForbiddenException => StatusCodes.Status403Forbidden,
        NotFoundException => StatusCodes.Status404NotFound,
        ConflictException => StatusCodes.Status409Conflict,
        ValidationException => StatusCodes.Status400BadRequest,
        Common.Exceptions.BusinessRuleException => StatusCodes.Status400BadRequest,
        _ => null,
    };
}
