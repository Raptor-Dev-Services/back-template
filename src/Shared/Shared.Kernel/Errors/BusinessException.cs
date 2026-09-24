using Common.Exceptions;

namespace Shared.Kernel.Errors;

/// <summary>
/// Base de las excepciones de negocio tipadas. El filtro global <c>BusinessExceptionFilter</c> las
/// traduce a su codigo HTTP y al envelope unico <c>{ data, isSuccess, message, utcTimeStamp }</c>.
///
/// <para><b>Cuando lanzar y cuando devolver.</b> Un caso de uso modela sus fallos esperados como
/// variantes tipadas de su <c>Response</c> (<c>INotFoundFailure</c>, <c>IConflictFailure</c>...), que el
/// presenter y <c>MapResult</c> convierten en el mismo envelope. Estas excepciones son para lo que NO es
/// un resultado del caso de uso sino una guarda transversal: una restriccion de unicidad que salta en
/// <c>SaveChanges</c>, un conflicto de concurrencia, un secreto de bootstrap invalido, una regla de
/// dominio violada en lo profundo de una entidad. Las dos vias acaban en la misma respuesta.</para>
///
/// <para>Deriva de <see cref="BusinessRuleException"/> (Common) para que el <c>InteractorPipeline</c> la
/// registre como error de negocio y no como "error critico": un 404 esperado no es una caida.</para>
///
/// <para>El mensaje llega TAL CUAL al cliente: escribelo para quien opera, nunca con detalles internos
/// (ids de otros tenants, nombres de tabla, SQL). Un error inesperado no es una BusinessException: se
/// deja escapar y el filtro responde 500 con un mensaje generico.</para>
/// </summary>
public abstract class BusinessException : BusinessRuleException
{
    protected BusinessException(string message) : base(message) { }
}

/// <summary>Datos de entrada que violan una regla de negocio -> HTTP 422.</summary>
public sealed class ValidationException(string message) : BusinessException(message);

/// <summary>Peticion malformada o no procesable a bajo nivel -> HTTP 400.</summary>
public sealed class BadRequestException(string message) : BusinessException(message);

/// <summary>Credenciales o sesion invalidas -> HTTP 401.</summary>
public sealed class UnauthorizedException(string message) : BusinessException(message);

/// <summary>Autenticado pero sin derecho sobre el recurso o la accion -> HTTP 403.</summary>
public sealed class ForbiddenException(string message) : BusinessException(message);

/// <summary>Registro no encontrado (o no visible para el llamador) -> HTTP 404.</summary>
public sealed class NotFoundException(string message) : BusinessException(message);

/// <summary>Conflicto de estado, duplicado o carrera de concurrencia perdida -> HTTP 409.</summary>
public sealed class ConflictException(string message) : BusinessException(message);
