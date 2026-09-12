using ObjectStorage.Domain.Interfaces;
using ObjectStorage.Domain.ValueObjects;

namespace ObjectStorage.UnitTests.Fakes;

public sealed class FakeObjectStorage : IObjectStorage
{
    private readonly Dictionary<string, byte[]> _objects = new();

    public IReadOnlyDictionary<string, byte[]> Objects => _objects;

    public Task<ObjectMetadata> UploadAsync(
        Stream content,
        string objectKey,
        string contentType,
        long contentLength,
        CancellationToken cancellationToken = default)
    {
        using var ms = new MemoryStream();
        content.CopyTo(ms);
        _objects[objectKey] = ms.ToArray();
        return Task.FromResult(new ObjectMetadata(objectKey, contentLength, contentType));
    }

    public Task<Stream> DownloadAsync(string objectKey, CancellationToken cancellationToken = default)
    {
        if (!_objects.TryGetValue(objectKey, out var bytes))
        {
            throw new Domain.Exceptions.ObjectNotFoundException(objectKey);
        }

        return Task.FromResult<Stream>(new MemoryStream(bytes));
    }

    public Task DeleteAsync(string objectKey, CancellationToken cancellationToken = default)
    {
        _objects.Remove(objectKey);
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(string objectKey, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_objects.ContainsKey(objectKey));
    }

    public Task<string> CreatePresignedUrlAsync(string objectKey, TimeSpan expiration, CancellationToken cancellationToken = default)
    {
        return Task.FromResult($"http://fake-presigned/{objectKey}");
    }
}
