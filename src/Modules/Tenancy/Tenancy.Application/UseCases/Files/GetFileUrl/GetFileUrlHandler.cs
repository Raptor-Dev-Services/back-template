using Common.Messaging;
using Shared.Kernel.Storage;
using Tenancy.Application.UseCases.Files.GetFileUrl.Responses;

namespace Tenancy.Application.UseCases.Files.GetFileUrl;

/// <summary>
/// Un JWT valido no basta: la clave tiene que ser del tenant de la peticion. 404 (nunca 403) si no existe o es
/// ajena, para no confirmar que una clave de otro tenant existe.
/// </summary>
internal sealed class GetFileUrlHandler(IObjectStorage storage, IStoredFileRegistry registry)
    : IRequestHandler<GetFileUrlRequest, GetFileUrlResponse>
{
    public async Task<GetFileUrlResponse> Handle(GetFileUrlRequest request, CancellationToken cancellationToken)
    {
        if (!ObjectKeys.IsWellFormed(request.ObjectKey)
            || await registry.FindAsync(request.ObjectKey, cancellationToken) is null)
            return new GetFileUrlNotFoundFailure("Archivo no encontrado.");

        var url = await storage.GetPresignedUrlAsync(request.ObjectKey, cancellationToken);
        return new GetFileUrlSuccess(new FileUrlDto(request.ObjectKey, url));
    }
}
