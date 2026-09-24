using Common.Messaging;
using Microsoft.Extensions.Logging;
using Shared.Kernel.Context;
using Shared.Kernel.Storage;
using Tenancy.Application.UseCases.Files.UploadFile.Responses;

namespace Tenancy.Application.UseCases.Files.UploadFile;

/// <summary>
/// Valida tamano, tipo declarado y firma de bytes; sube con una clave opaca con prefijo del tenant y registra al
/// dueno. Si el registro falla despues de subir, borra el objeto (mejor esfuerzo): un objeto sin dueno no lo puede
/// leer nadie, pero ocupa espacio para siempre.
/// </summary>
internal sealed class UploadFileHandler(
    IObjectStorage storage,
    IStoredFileRegistry registry,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    ILogger<UploadFileHandler> logger) : IRequestHandler<UploadFileRequest, UploadFileResponse>
{
    public async Task<UploadFileResponse> Handle(UploadFileRequest request, CancellationToken cancellationToken)
    {
        if (currentUser.TenantId is not { } tenantId)
            return new UploadFileForbiddenFailure("Subir archivos requiere una sesion de un tenant.");
        if (request.Length <= 0)
            return new UploadFileValidationFailure("Archivo vacio o ausente.");
        if (request.Length > AllowedFileTypes.MaxBytes)
            return new UploadFileValidationFailure($"El archivo excede el tamano maximo ({AllowedFileTypes.MaxBytes / (1024 * 1024)} MB).");
        if (!AllowedFileTypes.IsAllowedType(request.ContentType))
            return new UploadFileValidationFailure("Tipo de archivo no permitido (solo imagenes PNG, JPEG, GIF, WebP o PDF).");

        var content = await EnsureSeekableAsync(request.Content, cancellationToken);
        var header = new byte[AllowedFileTypes.HeaderLength];
        var read = await content.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false, cancellationToken);
        content.Position = 0;

        var extension = AllowedFileTypes.ExtensionFor(request.ContentType, header.AsMemory(0, read));
        if (extension is null)
            return new UploadFileValidationFailure("El contenido del archivo no corresponde al tipo declarado.");

        var contentType = request.ContentType!.ToLowerInvariant();
        var objectKey = ObjectKeys.NewFor(tenantId, extension, DateTime.UtcNow);
        await storage.UploadAsync(objectKey, content, request.Length, contentType, cancellationToken);

        try
        {
            registry.Add(objectKey, contentType, request.Length, SanitizeFileName(request.FileName));
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            await TryDeleteOrphanAsync(objectKey);
            throw;
        }

        var url = await storage.GetPresignedUrlAsync(objectKey, cancellationToken);
        return new UploadFileSuccess(new UploadedFileDto(objectKey, url, contentType, request.Length));
    }

    private static async Task<Stream> EnsureSeekableAsync(Stream content, CancellationToken cancellationToken)
    {
        if (content.CanSeek)
            return content;

        var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);
        buffer.Position = 0;
        return buffer;
    }

    /// <summary>Solo para mostrar: sin rutas, sin caracteres de control, acotado.</summary>
    private static string? SanitizeFileName(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return null;

        var name = Path.GetFileName(fileName.Replace('\\', '/'));
        name = new string([.. name.Where(c => !char.IsControl(c))]).Trim();
        return name.Length == 0 ? null : name.Length > 255 ? name[..255] : name;
    }

    private async Task TryDeleteOrphanAsync(string objectKey)
    {
        try
        {
            await storage.DeleteAsync(objectKey, CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "No se pudo borrar el objeto huerfano {ObjectKey} tras fallar su registro.", objectKey);
        }
    }
}
