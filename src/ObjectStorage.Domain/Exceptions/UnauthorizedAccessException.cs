namespace ObjectStorage.Domain.Exceptions;

public sealed class UnauthorizedAccessException : ObjectStorageException
{
    public UnauthorizedAccessException()
        : base("Authentication required. Provide a valid API key in the X-Api-Key header.")
    {
    }

    public UnauthorizedAccessException(string message)
        : base(message)
    {
    }
}
