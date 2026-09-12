using Minio.Exceptions;
using DomainExceptions = ObjectStorage.Domain.Exceptions;

namespace ObjectStorage.Infrastructure.MinIO;

public static class MinioExceptionMapper
{
    /// <summary>
    /// Translates exceptions thrown by the MinIO SDK into domain exceptions so that
    /// application and API layers never depend on MinIO SDK types.
    /// </summary>
    /// <param name="exception">The exception thrown by the SDK.</param>
    /// <param name="objectKey">The object key involved in the failed operation.</param>
    /// <returns>The exception to throw from the storage abstraction.</returns>
    public static Exception Map(Exception exception, string objectKey)
    {
        return exception switch
        {
            ObjectNotFoundException => new DomainExceptions.ObjectNotFoundException(objectKey, exception),
            BucketNotFoundException => new DomainExceptions.StorageUnavailableException(
                "The configured object storage bucket is not available.",
                exception),
            AccessDeniedException => new DomainExceptions.StorageUnavailableException(
                "The object storage credentials are invalid or the request was denied.",
                exception),
            MinioException => new DomainExceptions.StorageUnavailableException(
                "The object storage backend is unavailable or rejected the request.",
                exception),
            DomainExceptions.ObjectStorageException => exception,
            _ => exception,
        };
    }
}