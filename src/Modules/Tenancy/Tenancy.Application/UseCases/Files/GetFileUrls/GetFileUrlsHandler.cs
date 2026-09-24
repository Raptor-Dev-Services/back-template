using Common.Messaging;
using Shared.Kernel.Storage;
using Tenancy.Application.UseCases.Files.GetFileUrls.Responses;

namespace Tenancy.Application.UseCases.Files.GetFileUrls;

/// <summary>
/// Misma condicion de propiedad que <c>GetFileUrl</c>, en lote. Las claves ajenas o inexistentes NO salen en la
/// respuesta, sin distinguir un caso del otro (la misma informacion que da el 404 de una en una). Devolver la lista
/// parcial mantiene util la pantalla cuando una sola clave esta rota.
/// </summary>
internal sealed class GetFileUrlsHandler(IObjectStorage storage, IStoredFileRegistry registry)
    : IRequestHandler<GetFileUrlsRequest, GetFileUrlsResponse>
{
    /// <summary>Firmar es barato pero no gratis: sin tope, una peticion pediria miles de firmas.</summary>
    public const int MaxKeys = 50;

    public async Task<GetFileUrlsResponse> Handle(GetFileUrlsRequest request, CancellationToken cancellationToken)
    {
        var requested = (request.ObjectKeys ?? [])
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .Select(k => k.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (requested.Count > MaxKeys)
            return new GetFileUrlsValidationFailure($"Maximo {MaxKeys} claves por peticion.");

        var wellFormed = requested.Where(ObjectKeys.IsWellFormed).ToList();
        var owned = new HashSet<string>(await registry.FilterOwnedAsync(wellFormed, cancellationToken), StringComparer.Ordinal);

        var items = new List<FileUrlDto>(owned.Count);
        foreach (var key in wellFormed.Where(owned.Contains))
            items.Add(new FileUrlDto(key, await storage.GetPresignedUrlAsync(key, cancellationToken)));

        return new GetFileUrlsSuccess(new FileUrlsDto(items));
    }
}
