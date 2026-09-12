namespace ObjectStorage.Domain.Exceptions;

public sealed class CategoryNotAllowedException : ObjectStorageException
{
    public string Category { get; }

    public CategoryNotAllowedException(string category, Exception? innerException = null)
        : base($"Category '{category}' is not allowed. Use one of the configured categories.", innerException)
    {
        Category = category;
    }
}
