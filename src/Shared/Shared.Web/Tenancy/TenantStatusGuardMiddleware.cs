using Microsoft.AspNetCore.Http;
using Shared.Kernel.Security;
using Shared.Kernel.Tenancy;
using Shared.Web.Errors;

namespace Shared.Web.Tenancy;

/// <summary>
/// Corta con 403 (envelope) toda peticion autenticada de un tenant suspendido o dado de baja. Va DESPUES de
/// <see cref="TenantContextMiddleware"/>. Las peticiones anonimas (login, health) pasan: el login ya rechaza por su
/// cuenta a un tenant inactivo.
/// </summary>
public sealed class TenantStatusGuardMiddleware(RequestDelegate next)
{
    public const string SuspendedMessage = "La cuenta de tu organizacion esta suspendida. Contacta al administrador de la plataforma.";

    public async Task InvokeAsync(HttpContext context, ITenantStatusProvider tenants)
    {
        if (context.User.Identity?.IsAuthenticated == true
            && long.TryParse(context.User.FindFirst(AppClaimTypes.TenantId)?.Value, out var tenantId)
            && !await tenants.IsActiveAsync(tenantId, context.RequestAborted))
        {
            await ApiEnvelope.WriteFailureAsync(context, StatusCodes.Status403Forbidden, SuspendedMessage);
            return;
        }

        await next(context);
    }
}
