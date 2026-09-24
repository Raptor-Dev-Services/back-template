using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Minio;
using Minio.DataModel.Args;
using Minio.Exceptions;
using Shared.Kernel.Errors;
using Shared.Kernel.Storage;

namespace Shared.Infrastructure.Storage;

/// <summary>Configuracion del almacenamiento de objetos (seccion <c>ObjectStorage</c>, variables <c>ObjectStorage__*</c>).</summary>
public sealed class ObjectStorageOptions
{
    public const string SectionName = "ObjectStorage";

    /// <summary>Por donde la API ALCANZA el servidor (en contenedor, <c>minio:9000</c>).</summary>
    public string Endpoint { get; set; } = "localhost:9000";

    /// <summary>
    /// Con que host se FIRMAN las URLs de lectura. La firma S3 v4 incluye el host y no se puede reescribir despues:
    /// tiene que ser el que abre el navegador. Vacio = el mismo <see cref="Endpoint"/> (el caso de <c>dotnet run</c>).
    /// </summary>
    public string? PublicEndpoint { get; set; }

    public string AccessKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public string Bucket { get; set; } = "backtemplate";
    public bool UseSsl { get; set; }

    /// <summary>Region fija: evita que firmar una URL haga un round-trip para averiguarla.</summary>
    public string Region { get; set; } = "us-east-1";

    public int PresignedExpiryMinutes { get; set; } = 15;

    /// <summary>Crear el bucket al arrancar si falta (desarrollo). En produccion el bucket se aprovisiona aparte.</summary>
    public bool CreateBucketIfMissing { get; set; }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(AccessKey) && !string.IsNullOrWhiteSpace(SecretKey);
}

/// <summary>
/// <see cref="IObjectStorage"/> sobre MinIO / S3. Dos clientes: uno para hablar con el servidor y otro, con el host
/// publico, solo para firmar. Los clientes son thread-safe y se comparten (singleton).
/// </summary>
public sealed class MinioObjectStorage : IObjectStorage
{
    private readonly ObjectStorageOptions _options;
    private readonly IMinioClient _client;
    private readonly IMinioClient _signer;

    public MinioObjectStorage(ObjectStorageOptions options)
    {
        _options = options;
        _client = Build(options.Endpoint);
        _signer = string.IsNullOrWhiteSpace(options.PublicEndpoint) ? _client : Build(options.PublicEndpoint);
    }

    private IMinioClient Build(string endpoint) => new MinioClient()
        .WithEndpoint(endpoint)
        .WithCredentials(_options.AccessKey, _options.SecretKey)
        .WithRegion(_options.Region)
        .WithSSL(_options.UseSsl)
        .Build();

    public async Task UploadAsync(string objectKey, Stream content, long contentLength, string contentType, CancellationToken cancellationToken = default)
    {
        EnsureWellFormed(objectKey);
        await _client.PutObjectAsync(new PutObjectArgs()
            .WithBucket(_options.Bucket)
            .WithObject(objectKey)
            .WithStreamData(content)
            .WithObjectSize(contentLength)
            .WithContentType(contentType), cancellationToken);
    }

    public Task<string> GetPresignedUrlAsync(string objectKey, CancellationToken cancellationToken = default)
    {
        // Defensa en profundidad: que ninguna ruta futura pueda firmar una clave habiendo comprobado otra.
        EnsureWellFormed(objectKey);
        return _signer.PresignedGetObjectAsync(new PresignedGetObjectArgs()
            .WithBucket(_options.Bucket)
            .WithObject(objectKey)
            .WithExpiry(Math.Clamp(_options.PresignedExpiryMinutes, 1, 60) * 60));
    }

    public async Task DeleteAsync(string objectKey, CancellationToken cancellationToken = default)
    {
        EnsureWellFormed(objectKey);
        try
        {
            await _client.RemoveObjectAsync(new RemoveObjectArgs().WithBucket(_options.Bucket).WithObject(objectKey), cancellationToken);
        }
        catch (ObjectNotFoundException)
        {
            // Idempotente: el estado deseado ("el objeto no esta") ya se cumple. Otro error (red, permisos) si sube.
        }
    }

    public async Task PingAsync(CancellationToken cancellationToken = default)
    {
        if (!await _client.BucketExistsAsync(new BucketExistsArgs().WithBucket(_options.Bucket), cancellationToken))
            throw new InvalidOperationException($"El bucket '{_options.Bucket}' no existe.");
    }

    internal IMinioClient Client => _client;

    private static void EnsureWellFormed(string objectKey)
    {
        if (!ObjectKeys.IsWellFormed(objectKey))
            throw new ValidationException("La clave del archivo no es valida.");
    }
}

/// <summary>
/// Crea el bucket al arrancar si falta (solo con <see cref="ObjectStorageOptions.CreateBucketIfMissing"/>) y lo deja
/// PRIVADO. No tumba el arranque si el servidor no responde: /health/ready lo reporta.
/// </summary>
public sealed class ObjectStorageBootstrapService(
    IServiceProvider services, ObjectStorageOptions options, ILogger<ObjectStorageBootstrapService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!options.CreateBucketIfMissing || services.GetService<IObjectStorage>() is not MinioObjectStorage storage)
            return;

        try
        {
            var args = new BucketExistsArgs().WithBucket(options.Bucket);
            if (!await storage.Client.BucketExistsAsync(args, cancellationToken))
            {
                await storage.Client.MakeBucketAsync(new MakeBucketArgs().WithBucket(options.Bucket).WithLocation(options.Region), cancellationToken);
                logger.LogInformation("Bucket '{Bucket}' creado (privado).", options.Bucket);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "No se pudo verificar o crear el bucket '{Bucket}'; la subida de archivos fallara hasta que exista.", options.Bucket);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

public static class ObjectStorageServiceCollectionEx
{
    /// <summary>
    /// Almacenamiento de objetos y registro de propiedad. Sin credenciales configuradas la API arranca igual (el resto
    /// no depende de archivos) pero /health/ready lo marca: una API "sana" que no puede subir nada es peor que una
    /// que avisa. En produccion, <c>EnsureProductionSettings</c> exige las credenciales.
    /// </summary>
    public static IServiceCollection AddObjectStorage(this IServiceCollection services, ObjectStorageOptions options)
    {
        services.AddSingleton(options);
        services.AddSingleton<IObjectStorage>(_ => new MinioObjectStorage(options));
        services.AddScoped<IStoredFileRegistry, EfStoredFileRegistry>();
        services.AddHostedService<ObjectStorageBootstrapService>();
        return services;
    }
}
