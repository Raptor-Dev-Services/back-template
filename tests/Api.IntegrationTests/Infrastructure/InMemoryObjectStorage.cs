using System.Collections.Concurrent;
using Shared.Kernel.Storage;

namespace Api.IntegrationTests.Infrastructure;

/// <summary>El almacenamiento de objetos en memoria. La "URL prefirmada" es una URL falsa con la clave, para comprobar que.</summary>
public sealed class InMemoryObjectStorage : IObjectStorage
{
    private readonly ConcurrentDictionary<string, (byte[] Content, string ContentType)> _objects = new(StringComparer.Ordinal);

    public bool Contains(string objectKey) => _objects.ContainsKey(objectKey);

    public async Task UploadAsync(string objectKey, Stream content, long contentLength, string contentType, CancellationToken cancellationToken = default)
    {
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);
        _objects[objectKey] = (buffer.ToArray(), contentType);
    }

    public Task<string> GetPresignedUrlAsync(string objectKey, CancellationToken cancellationToken = default) =>
        Task.FromResult($"https://storage.example.test/{objectKey}?signature=test");

    public Task DeleteAsync(string objectKey, CancellationToken cancellationToken = default)
    {
        _objects.TryRemove(objectKey, out _);
        return Task.CompletedTask;
    }

    public Task PingAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}
