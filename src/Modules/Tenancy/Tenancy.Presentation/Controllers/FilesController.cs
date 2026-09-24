using Common.Messaging;
using Common.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Shared.Kernel.Security;
using Shared.Kernel.Storage;
using Shared.Web;
using Shared.Web.Authorization;
using Tenancy.Application.UseCases.Files.GetFileUrl;
using Tenancy.Application.UseCases.Files.GetFileUrls;
using Tenancy.Application.UseCases.Files.UploadFile;

namespace Tenancy.Presentation.Controllers;

/// <summary>
/// Archivos del tenant en el almacenamiento de objetos (bucket PRIVADO). La subida devuelve la clave opaca que la
/// entidad guarda y una URL prefirmada para verla ya; para pintar una clave existente se pide su URL aqui. Leer
/// exige ser del tenant dueno de la clave, no solo tener un token valido.
/// </summary>
[Route("api/v1/files")]
public sealed class FilesController(IMediator mediator, ResultViewModel<FilesController> viewModel) : BaseApiController(mediator)
{
    /// <summary>Sube un archivo (multipart/form-data, campo <c>file</c>). 400 si el tipo, la firma o el tamano no valen.</summary>
    [HttpPost]
    [Authorize(Policy = PermissionPolicy.Prefix + KnownPermissions.FilesWrite)]
    [EnableRateLimiting(RateLimitPolicies.Upload)]
    // Un poco mas que el maximo del archivo: el multipart agrega sus cabeceras. El tope real lo aplica el caso de uso.
    [RequestSizeLimit(AllowedFileTypes.MaxBytes + 64 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = AllowedFileTypes.MaxBytes + 64 * 1024)]
    public async Task<IActionResult> Upload(IFormFile? file, CancellationToken cancellationToken = default)
    {
        await using var content = file?.OpenReadStream() ?? Stream.Null;
        var request = new UploadFileRequest(content, file?.Length ?? 0, file?.FileName, file?.ContentType);
        return MapResult(await DispatchAsync(request, cancellationToken), viewModel);
    }

    /// <summary>URL prefirmada para una clave del tenant. 404 si no existe o es de otro tenant (sin distinguir).</summary>
    [HttpGet("{**objectKey}")]
    [Authorize(Policy = PermissionPolicy.Prefix + KnownPermissions.FilesRead)]
    public async Task<IActionResult> GetUrl(string objectKey, CancellationToken cancellationToken = default) =>
        MapResult(await DispatchAsync(new GetFileUrlRequest(objectKey), cancellationToken), viewModel);

    /// <summary>
    /// URLs para varias claves (maximo 50). POST aunque sea lectura: las claves llevan barras y una pagina entera no
    /// cabe con garantias en un query string. Las ajenas o inexistentes simplemente no salen.
    /// </summary>
    [HttpPost("urls")]
    [Authorize(Policy = PermissionPolicy.Prefix + KnownPermissions.FilesRead)]
    public async Task<IActionResult> GetUrls([FromBody] GetFileUrlsBody? body, CancellationToken cancellationToken = default) =>
        MapResult(await DispatchAsync(new GetFileUrlsRequest(body?.ObjectKeys), cancellationToken), viewModel);
}

public sealed record GetFileUrlsBody(IReadOnlyList<string>? ObjectKeys);
