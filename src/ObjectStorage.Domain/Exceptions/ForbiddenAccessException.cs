namespace ObjectStorage.Domain.Exceptions;

public sealed class ForbiddenAccessException : ObjectStorageException
{
    public ForbiddenAccessException(string serviceName, string objectKey)
        : base($"Service '{serviceName}' is not authorized to access object '{objectKey}'.")
    {
        ServiceName = serviceName;
        ObjectKey = objectKey;
    }

    public string ServiceName { get; }
    public string ObjectKey { get; }
}
