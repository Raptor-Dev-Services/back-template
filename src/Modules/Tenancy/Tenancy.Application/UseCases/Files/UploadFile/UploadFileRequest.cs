using Common.Messaging;
using Tenancy.Application.UseCases.Files.UploadFile.Responses;

namespace Tenancy.Application.UseCases.Files.UploadFile;

/// <summary>
/// Sube un archivo a nombre del tenant del token. El <paramref name="ContentType"/> lo declara el cliente: el caso de
/// uso lo contrasta con los primeros bytes del contenido.
/// </summary>
public sealed record UploadFileRequest(Stream Content, long Length, string? FileName, string? ContentType)
    : IRequest<UploadFileResponse>;
