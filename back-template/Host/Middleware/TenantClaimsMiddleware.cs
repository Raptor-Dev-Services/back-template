using Common.MultiTenancy;

namespace Host.Middleware;

/// <summary>
/// Extrae tenant_id del JWT autenticado y lo inyecta en ITenantContextAccessor.
/// Debe ejecutarse después de UseAuthentication().
/// </summary>
public sealed class TenantClaimsMiddleware
{
    private readonly RequestDelegate _next;

    public TenantClaimsMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, ITenantContextAccessor tenantCtx)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var tenantId = context.User.FindFirst("tenant_id")?.Value;
            if (!string.IsNullOrEmpty(tenantId))
                tenantCtx.Current = new TenantContext(tenantId);
        }

        await _next(context);
    }
}
