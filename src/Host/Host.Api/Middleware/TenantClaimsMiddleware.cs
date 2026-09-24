using Common.MultiTenancy;

namespace Host.Api.Middleware;

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
