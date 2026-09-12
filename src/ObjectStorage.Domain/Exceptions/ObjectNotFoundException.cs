namespace ObjectStorage.Domain.Exceptions;

public sealed class ObjectNotFoundException : ObjectStorageException
{
    public string ObjectKey { get; }

    public ObjectNotFoundException(string objectKey, Exception? innerException = null)
        : base($"Object '{objectKey}' was not found in storage.", innerException)
    {
        ObjectKey = objectKey;
    }
}