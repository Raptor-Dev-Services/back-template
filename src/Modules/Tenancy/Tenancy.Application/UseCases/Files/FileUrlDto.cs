namespace Tenancy.Application.UseCases.Files;

/// <summary>Una clave y su URL prefirmada de lectura (vida corta).</summary>
public sealed record FileUrlDto(string ObjectKey, string Url);

/// <summary>Lo que devuelve la subida: la clave a guardar en la entidad que la use, y una URL para verla ya.</summary>
public sealed record UploadedFileDto(string ObjectKey, string Url, string ContentType, long SizeBytes);

/// <summary>Varias URLs de una vez (una pantalla con N imagenes hace una peticion, no N).</summary>
public sealed record FileUrlsDto(IReadOnlyList<FileUrlDto> Items);
