namespace ObjectStorage.Domain.Exceptions;

public abstract class ObjectStorageException : Exception
{
    protected ObjectStorageException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}