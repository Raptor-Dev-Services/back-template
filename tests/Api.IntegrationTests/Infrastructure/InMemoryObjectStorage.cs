using System.Collections.Concurrent;
using Shared.Kernel.Storage;

namespace Api.IntegrationTests.Infrastructure;

/// <summary>El almacenamiento de objetos en memoria. La "URL prefirmada" es una URL falsa con la clave, para comprobar que.</summary>
public sealed class InMemoryObjectStorage : IObjectStorage
{
    private readonly ConcurrentDictionary<string, (byte[] Content, string ContentType, DateTime LastModifiedUtc)> _objects = new(StringComparer.Ordinal);

    public bool Contains(string objectKey) => _objects.ContainsKey(objectKey);

    public async Task UploadAsync(string objectKey, Stream content, long contentLength, string contentType, CancellationToken cancellationToken = default)
    {
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);
        _objects[objectKey] = (buffer.ToArray(), contentType, DateTime.UtcNow);
    }

    public Task<string> GetPresignedUrlAsync(string objectKey, CancellationToken cancellationToken = default) =>
        Task.FromResult($"https://storage.example.test/{objectKey}?signature=test");

    public Task DeleteAsync(string objectKey, CancellationToken cancellationToken = default)
    {
        _objects.TryRemove(objectKey, out _);
        return Task.CompletedTask;
    }

    public Task PingAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public async IAsyncEnumerable<StoredObjectInfo> ListAllAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (var (key, value) in _objects)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return new StoredObjectInfo(key, value.LastModifiedUtc);
        }
        await Task.CompletedTask;
    }

    /// <summary>Pone un objeto directo en el "bucket", sin pasar por la API y con la antiguedad indicada.</summary>
    public void Put(string objectKey, TimeSpan age) =>
        _objects[objectKey] = ([1, 2, 3], "image/png", DateTime.UtcNow - age);

    /// <summary>Envejece un objeto que ya esta (por ejemplo, uno subido por la API).</summary>
    public void Age(string objectKey, TimeSpan age) =>
        _objects[objectKey] = _objects[objectKey] with { LastModifiedUtc = DateTime.UtcNow - age };
}
