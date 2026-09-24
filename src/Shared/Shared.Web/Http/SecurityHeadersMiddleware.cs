using Microsoft.AspNetCore.Http;

namespace Shared.Web.Http;

/// <summary>
/// Cabeceras de seguridad de respuesta (OWASP) en TODA respuesta, incluidos 401/429/500. La API sirve JSON, no
/// HTML, asi que la politica es la mas restrictiva posible:
/// <list type="bullet">
///   <item><c>X-Content-Type-Options: nosniff</c>: el navegador no adivina el tipo.</item>
///   <item><c>X-Frame-Options: DENY</c> y <c>frame-ancestors 'none'</c>: anti clickjacking.</item>
///   <item><c>Referrer-Policy: no-referrer</c>, <c>Cross-Origin-Opener-Policy</c>, <c>X-Permitted-Cross-Domain-Policies</c>.</item>
///   <item><c>Content-Security-Policy: default-src 'none'</c>: una respuesta de API no carga recursos. Se omite solo
///   en <c>/swagger</c> (desarrollo), que si carga JS y CSS propios.</item>
/// </list>
/// HSTS no va aqui: lo aplica <c>UseHsts()</c> fuera de desarrollo (requiere HTTPS) y el proxy de borde.
/// </summary>
public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    private const string ApiContentSecurityPolicy = "default-src 'none'; frame-ancestors 'none'; base-uri 'none'; form-action 'none'";

    public Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(static state =>
        {
            var ctx = (HttpContext)state;
            var headers = ctx.Response.Headers;
            headers["X-Content-Type-Options"] = "nosniff";
            headers["X-Frame-Options"] = "DENY";
            headers["Referrer-Policy"] = "no-referrer";
            headers["X-Permitted-Cross-Domain-Policies"] = "none";
            headers["Cross-Origin-Opener-Policy"] = "same-origin";

            if (!ctx.Request.Path.StartsWithSegments("/swagger", StringComparison.OrdinalIgnoreCase))
                headers["Content-Security-Policy"] = ApiContentSecurityPolicy;

            return Task.CompletedTask;
        }, context);

        return next(context);
    }
}
