namespace ObjectStorage.Domain.Exceptions;

public sealed class InvalidRequestException : ObjectStorageException
{
    public InvalidRequestException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
