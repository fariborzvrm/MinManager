namespace ObjectStorage.Domain.Exceptions;

public sealed class StorageUnavailableException : ObjectStorageException
{
    public StorageUnavailableException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}