using ObjectStorage.Domain.ValueObjects;

namespace ObjectStorage.Domain.Interfaces;

public interface IObjectStorage
{
    Task<ObjectMetadata> UploadAsync(
        Stream content,
        string objectKey,
        string contentType,
        long contentLength,
        CancellationToken cancellationToken = default);

    Task<Stream> DownloadAsync(
        string objectKey,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        string objectKey,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(
        string objectKey,
        CancellationToken cancellationToken = default);

    Task<string> CreatePresignedUrlAsync(
        string objectKey,
        TimeSpan expiration,
        CancellationToken cancellationToken = default);
}