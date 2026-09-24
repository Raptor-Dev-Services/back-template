namespace Shared.Kernel.Security;

/// <summary>
/// Nombres de los claims que emite el access JWT y que lee el resto del sistema. Un solo lugar: se
/// comparan como TEXTO en tiempo de ejecucion y un nombre mal escrito en un extremo no lo ve el
/// compilador -el claim simplemente "no esta"- y el sintoma es un 403 sin explicacion.
///
/// El Host valida los tokens con <c>MapInboundClaims = false</c>, asi que estos nombres llegan tal cual a
/// <c>HttpContext.User</c> (sin la traduccion a las URIs largas de <c>ClaimTypes</c>).
/// </summary>
public static class AppClaimTypes
{
    /// <summary>PublicId (Guid) del usuario. Nunca el id numerico interno.</summary>
    public const string Subject = "sub";

    /// <summary>Tenant del usuario. La UNICA fuente del tenant de una peticion: nunca el body ni un header.</summary>
    public const string TenantId = "tenant_id";

    public const string Email = "email";

    /// <summary>Nombre a mostrar en la interfaz.</summary>
    public const string Name = "name";

    /// <summary>Codigo de rol (multi-valor).</summary>
    public const string Role = "roles";

    /// <summary>Codigo de permiso RBAC (multi-valor). Lo lee la policy <c>perm:&lt;code&gt;</c>.</summary>
    public const string Permission = "permission";
}
