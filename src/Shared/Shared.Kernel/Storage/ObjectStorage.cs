namespace Shared.Kernel.Storage;

/// <summary>
/// Almacenamiento de objetos (compatible S3; MinIO en desarrollo). El binario vive en el store; la base guarda solo
/// la clave opaca y QUIEN es su dueno (<see cref="IStoredFileRegistry"/>).
///
/// <para>El bucket es PRIVADO: las lecturas se sirven con URLs prefirmadas de vida corta, nunca con una URL publica
/// anonima. Firmar una URL NO autoriza nada por si mismo: antes de pedirla, el caso de uso comprueba que la clave
/// pertenece al tenant de la peticion.</para>
/// </summary>
public interface IObjectStorage
{
    Task UploadAsync(string objectKey, Stream content, long contentLength, string contentType, CancellationToken cancellationToken = default);

    /// <summary>URL prefirmada de lectura (GET), valida por unos minutos.</summary>
    Task<string> GetPresignedUrlAsync(string objectKey, CancellationToken cancellationToken = default);

    /// <summary>Idempotente: borrar una clave que no existe no es un error.</summary>
    Task DeleteAsync(string objectKey, CancellationToken cancellationToken = default);

    /// <summary>Round-trip ligero al servidor (lo usa /health/ready). Lanza si no responde.</summary>
    Task PingAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// TODOS los objetos del bucket, con su ultima modificacion. Es para conciliar contra el registro de propiedad
    /// (la purga de huerfanos), nunca para servir un listado a un usuario: no filtra por tenant.
    /// </summary>
    IAsyncEnumerable<StoredObjectInfo> ListAllAsync(CancellationToken cancellationToken = default);
}

/// <summary>Un objeto tal como lo ve el store, sin saber quien es su dueno.</summary>
public sealed record StoredObjectInfo(string ObjectKey, DateTime LastModifiedUtc);

/// <summary>Un archivo registrado, tal como lo ve su dueno.</summary>
public sealed record StoredFileDto(string ObjectKey, string ContentType, long SizeBytes, string? FileName, DateTime CreatedAtUtc);

/// <summary>
/// Registro de propiedad de los objetos subidos. Sin esta tabla, "tener un JWT valido" y "ser el dueno del archivo"
/// serian indistinguibles: cualquier usuario de cualquier tenant podria pedir la URL de cualquier clave.
///
/// <para>Las filas son del tenant (filtro de EF + RLS): <see cref="FindAsync"/> y <see cref="FilterOwnedAsync"/> solo
/// ven las del tenant de la peticion, sin que el llamador pase el tenant.</para>
/// </summary>
public interface IStoredFileRegistry
{
    /// <summary>Agrega el registro SIN guardar: se persiste con el <c>SaveChanges</c> del caso de uso.</summary>
    void Add(string objectKey, string contentType, long sizeBytes, string? fileName);

    Task<StoredFileDto?> FindAsync(string objectKey, CancellationToken cancellationToken = default);

    /// <summary>De las claves pedidas, las que son del tenant de la peticion.</summary>
    Task<IReadOnlyCollection<string>> FilterOwnedAsync(IReadOnlyCollection<string> objectKeys, CancellationToken cancellationToken = default);
}

/// <summary>
/// Claves de objeto: <c>{tenantId}/{yyyy}/{MM}/{guid}{ext}</c>. El prefijo del tenant permite listar o purgar lo de un
/// cliente de un tiron; el GUID (122 bits) la hace no enumerable; la extension sale del TIPO validado, nunca del
/// nombre que mando el cliente.
/// </summary>
public static class ObjectKeys
{
    public const int MaxLength = 200;

    public static string NewFor(long tenantId, string extension, DateTime nowUtc) =>
        $"{tenantId}/{nowUtc:yyyy}/{nowUtc:MM}/{Guid.NewGuid():N}{extension}";

    /// <summary>
    /// Rechaza lo que ninguna clave legitima lleva: segmentos punto (la firma normaliza <c>a/../b</c> a <c>b</c>, asi
    /// que se firmaria una clave DISTINTA de la comprobada), barras invertidas, vacios o longitud excesiva.
    /// </summary>
    public static bool IsWellFormed(string? objectKey) =>
        !string.IsNullOrWhiteSpace(objectKey)
        && objectKey.Length <= MaxLength
        && !objectKey.Contains('\\')
        && !objectKey.StartsWith('/')
        && objectKey.Split('/').All(segment => segment.Length > 0 && segment is not "." and not "..");

    /// <summary>
    /// El tenant del prefijo de una clave bien formada. False si la clave no sigue el formato de <see cref="NewFor"/>
    /// (un objeto que no subio esta API): quien concilia NO debe tocarlo.
    /// </summary>
    public static bool TryGetTenantId(string? objectKey, out long tenantId)
    {
        tenantId = 0;
        if (!IsWellFormed(objectKey))
            return false;

        var slash = objectKey!.IndexOf('/');
        return slash > 0
            && long.TryParse(objectKey.AsSpan(0, slash), System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture, out tenantId)
            && tenantId > 0;
    }
}

/// <summary>
/// Tipos de archivo admitidos, con su firma de bytes. El <c>Content-Type</c> lo declara el cliente y se puede
/// falsear; los primeros bytes no. Un archivo cuyo contenido no coincide con el tipo declarado se rechaza.
/// </summary>
public static class AllowedFileTypes
{
    public const long MaxBytes = 5 * 1024 * 1024;

    private static readonly Dictionary<string, (string Extension, Func<ReadOnlyMemory<byte>, bool> Matches)> Types =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["image/png"] = (".png", h => StartsWith(h, 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A)),
            ["image/jpeg"] = (".jpg", h => StartsWith(h, 0xFF, 0xD8, 0xFF)),
            ["image/gif"] = (".gif", h => StartsWith(h, (byte)'G', (byte)'I', (byte)'F', (byte)'8')),
            ["image/webp"] = (".webp", h => StartsWith(h, (byte)'R', (byte)'I', (byte)'F', (byte)'F')
                                          && h.Length >= 12 && h.Span[8..12].SequenceEqual("WEBP"u8)),
            ["application/pdf"] = (".pdf", h => StartsWith(h, (byte)'%', (byte)'P', (byte)'D', (byte)'F', (byte)'-')),
        };

    /// <summary>Bytes de cabecera que hacen falta para reconocer cualquier tipo admitido.</summary>
    public const int HeaderLength = 12;

    public static IReadOnlyCollection<string> ContentTypes => Types.Keys;

    /// <summary>La extension canonica si el tipo esta admitido y la cabecera coincide; null si no.</summary>
    public static string? ExtensionFor(string? contentType, ReadOnlyMemory<byte> header) =>
        contentType is not null && Types.TryGetValue(contentType, out var type) && type.Matches(header)
            ? type.Extension
            : null;

    public static bool IsAllowedType(string? contentType) => contentType is not null && Types.ContainsKey(contentType);

    private static bool StartsWith(ReadOnlyMemory<byte> header, params byte[] signature) =>
        header.Length >= signature.Length && header.Span[..signature.Length].SequenceEqual(signature);
}
