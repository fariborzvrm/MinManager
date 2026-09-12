namespace ObjectStorage.Domain.Exceptions;

public sealed class FileTooLargeException : ObjectStorageException
{
    public long ContentLength { get; }
    public long MaxSizeBytes { get; }

    public FileTooLargeException(long contentLength, long maxSizeBytes, Exception? innerException = null)
        : base($"File size {contentLength} bytes exceeds the maximum allowed size of {maxSizeBytes} bytes.", innerException)
    {
        ContentLength = contentLength;
        MaxSizeBytes = maxSizeBytes;
    }
}
