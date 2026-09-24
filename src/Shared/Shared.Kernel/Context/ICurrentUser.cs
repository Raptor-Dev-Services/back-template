namespace Shared.Kernel.Context;

/// <summary>
/// El usuario autenticado de la peticion en curso. La implementacion lee el claim <c>sub</c> del JWT ya
/// validado; fuera de una peticion (tareas programadas, arranque) no hay usuario y todo es null.
///
/// Lo consumen la auditoria de <c>SaveChanges</c> (quien creo/modifico/borro) y la bitacora de acciones.
/// Nunca se usa para AUTORIZAR: eso lo decide la policy del endpoint sobre los claims del token.
/// </summary>
public interface ICurrentUser
{
    /// <summary>PublicId del usuario, o null si no hay sesion.</summary>
    Guid? UserId { get; }

    /// <summary>Tenant del usuario, o null si no hay sesion.</summary>
    long? TenantId { get; }
}

/// <summary>
/// "Nadie": el actor de lo que corre fuera de una peticion (tareas programadas, arranque, herramientas de
/// diseno de EF). Las marcas de "quien" quedan en null, que es lo honesto.
/// </summary>
public sealed class NoCurrentUser : ICurrentUser
{
    public static readonly NoCurrentUser Instance = new();

    public Guid? UserId => null;
    public long? TenantId => null;
}
