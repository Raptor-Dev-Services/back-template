using Shared.Kernel.Results;

namespace Shared.Kernel.Audit;

/// <summary>
/// Bitacora de ACCIONES: quien hizo que, sobre que, cuando y por que. Cualquier modulo la usa sin saber como se
/// persiste. Es para acciones sensibles o destructivas (invitar, bloquear, dar de baja, quitar un segundo factor),
/// no un registro de cada lectura.
///
/// <para><b>Dentro de la transaccion del caso de uso.</b> <see cref="Append"/> agrega la linea al contexto SIN
/// guardar: se persiste con el mismo <c>SaveChanges</c> que la accion. Si la accion falla, no queda una linea que
/// diga que ocurrio; si la linea no se puede escribir, la accion tampoco.</para>
///
/// <para>El tenant y el actor salen del contexto autenticado (el tenant del JWT, o el que fijo un
/// <c>ITenantScope</c>); el llamador no los pasa. El resumen se escribe para quien lo va a leer: sin contrasenas,
/// tokens ni codigos.</para>
/// </summary>
public interface IAuditLog
{
    void Append(string action, string entityType, string? entityId, string summary, string? reason = null);
}

/// <summary>Una linea de la bitacora, tal como se consulta.</summary>
public sealed record AuditEntryDto(
    long Id,
    DateTime OccurredAtUtc,
    string Action,
    string EntityType,
    string? EntityId,
    string Summary,
    string? Reason,
    Guid? ActorUserId);

/// <summary>Lectura paginada de la bitacora del tenant de la peticion (el filtro de tenant y RLS la acotan).</summary>
public interface IAuditLogReader
{
    Task<PagedResult<AuditEntryDto>> GetPageAsync(int page, int pageSize, string? action, CancellationToken cancellationToken = default);
}
