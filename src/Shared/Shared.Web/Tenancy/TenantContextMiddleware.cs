using System.Diagnostics;
using Common.MultiTenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Shared.Kernel.Security;

namespace Shared.Web.Tenancy;

/// <summary>
/// Publica el tenant de la peticion en el <see cref="ITenantContextAccessor"/> de Common, que alimenta el
/// filtro global de EF, el GUC de RLS (<c>app.tenant_id</c>) y el enriquecedor de logs.
///
/// <para><b>El tenant sale SOLO del claim <c>tenant_id</c> del JWT ya validado.</b> Nunca del body, del query
/// string ni de un header que controle el cliente: eso seria dejar que cada quien elija de que empresa leer.
/// Por eso va DESPUES de <c>UseAuthentication</c>; una peticion anonima no tiene tenant y cualquier tabla de
/// negocio le devuelve cero filas.</para>
///
/// <para>Se limpia al terminar: el accessor es un <c>AsyncLocal</c> y no debe sobrevivir a la peticion.</para>
/// </summary>
public sealed class TenantContextMiddleware(RequestDelegate next, ILogger<TenantContextMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context, ITenantContextAccessor tenantAccessor)
    {
        var claim = context.User.Identity?.IsAuthenticated == true
            ? context.User.FindFirst(AppClaimTypes.TenantId)?.Value
            : null;

        if (!long.TryParse(claim, out var tenantId) || tenantId <= 0)
        {
            await next(context);
            return;
        }

        var tenant = tenantId.ToString(System.Globalization.CultureInfo.InvariantCulture);
        tenantAccessor.Current = new TenantContext(tenant);
        Activity.Current?.SetTag("tenant.id", tenant);
        try
        {
            using (logger.BeginScope(new Dictionary<string, object> { ["TenantId"] = tenantId }))
                await next(context);
        }
        finally
        {
            tenantAccessor.Current = null;
        }
    }
}
