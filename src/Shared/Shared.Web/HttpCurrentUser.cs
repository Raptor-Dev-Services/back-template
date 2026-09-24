using Microsoft.AspNetCore.Http;
using Shared.Kernel.Context;
using Shared.Kernel.Security;

namespace Shared.Web;

/// <summary>
/// <see cref="ICurrentUser"/> leido del JWT ya validado de la peticion en curso. Fuera de una peticion (una
/// tarea programada, el arranque) no hay <c>HttpContext</c> y todo es null, que es lo correcto: nadie actuo.
/// </summary>
public sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public Guid? UserId =>
        Guid.TryParse(User?.FindFirst(AppClaimTypes.Subject)?.Value, out var id) ? id : null;

    public long? TenantId =>
        long.TryParse(User?.FindFirst(AppClaimTypes.TenantId)?.Value, out var id) ? id : null;

    private System.Security.Claims.ClaimsPrincipal? User =>
        accessor.HttpContext?.User is { Identity.IsAuthenticated: true } user ? user : null;
}
