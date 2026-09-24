using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Shared.Web.Http;

/// <summary>
/// Un id de correlacion por peticion, publicado para los logs y devuelto en la respuesta (<c>X-Correlation-Id</c>)
/// para que quien reporta un fallo pueda citarlo.
///
/// <para>Reemplaza al <c>UseCorrelationId</c> de Common, que copiaba el header del cliente TAL CUAL al log y a la
/// respuesta. El header es entrada externa y hostil: se sanea (alfanumericos y <c>- _ . :</c>) y se recorta a 128
/// caracteres, o un cliente inyecta saltos de linea en los logs (log forging) o infla cada evento. Sin header
/// valido se usa el TraceId W3C de la peticion: correlacion y traza distribuida son el mismo valor.</para>
///
/// Va PRIMERO en el pipeline: todo lo que se registre despues (auth, limitador, errores) queda correlacionado.
/// </summary>
public sealed class CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
{
    public const string HeaderName = "X-Correlation-Id";
    public const string ItemsKey = "CorrelationId";
    private const int MaxLength = 128;

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = Sanitize(context.Request.Headers[HeaderName].ToString());
        if (correlationId.Length == 0)
            correlationId = Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier;

        context.Items[ItemsKey] = correlationId;
        Activity.Current?.SetTag("correlation_id", correlationId);

        // OnStarting: la cabecera se fija justo antes de escribir, tambien en un 401, un 429 o un 500.
        context.Response.OnStarting(static state =>
        {
            var ctx = (HttpContext)state;
            if (ctx.Items[ItemsKey] is string id)
                ctx.Response.Headers[HeaderName] = id;
            return Task.CompletedTask;
        }, context);

        using (logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId }))
            await next(context);
    }

    public static string Sanitize(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        var source = value.Length > MaxLength ? value.AsSpan(0, MaxLength) : value.AsSpan();
        Span<char> buffer = stackalloc char[source.Length];
        var length = 0;
        foreach (var c in source)
        {
            if (char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.' or ':')
                buffer[length++] = c;
        }

        return length == 0 ? string.Empty : new string(buffer[..length]);
    }
}
