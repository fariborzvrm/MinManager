using ObjectStorage.Domain.Exceptions;
using ObjectStorage.Domain.Interfaces;
using ObjectStorage.Domain.ValueObjects;

namespace ObjectStorage.UnitTests.Fakes;

public sealed class FailingObjectStorage : IObjectStorage
{
    public bool ShouldFailUpload { get; set; }

    public Task<ObjectMetadata> UploadAsync(
        Stream content,
        string objectKey,
        string contentType,
        long contentLength,
        CancellationToken cancellationToken = default)
    {
        if (ShouldFailUpload)
        {
            throw new StorageUnavailableException("MinIO upload failed intentionally.");
        }

        return Task.FromResult(new ObjectMetadata(objectKey, contentLength, contentType));
    }

    public Task<Stream> DownloadAsync(string objectKey, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task DeleteAsync(string objectKey, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<bool> ExistsAsync(string objectKey, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<string> CreatePresignedUrlAsync(string objectKey, TimeSpan expiration, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}
