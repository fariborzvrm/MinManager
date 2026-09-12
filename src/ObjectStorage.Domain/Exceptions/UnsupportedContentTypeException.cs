namespace ObjectStorage.Domain.Exceptions;

public sealed class UnsupportedContentTypeException : ObjectStorageException
{
    public string ContentType { get; }

    public UnsupportedContentTypeException(string contentType, Exception? innerException = null)
        : base($"Content type '{contentType}' is not supported.", innerException)
    {
        ContentType = contentType;
    }
}
