namespace ObjectStorage.Domain.Entities;

public sealed class StoredObject
{
    public string Id { get; }
    public string ObjectKey { get; }
    public string FileName { get; }
    public string ContentType { get; }
    public long Size { get; }
    public DateTimeOffset CreatedAt { get; }
    public StoredObjectStatus Status { get; private set; }

    public StoredObject(
        string id,
        string objectKey,
        string fileName,
        string contentType,
        long size)
        : this(id, objectKey, fileName, contentType, size, DateTimeOffset.UtcNow)
    {
    }

    public StoredObject(
        string id,
        string objectKey,
        string fileName,
        string contentType,
        long size,
        DateTimeOffset createdAt)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("Id is required.", nameof(id));
        if (string.IsNullOrWhiteSpace(objectKey))
            throw new ArgumentException("ObjectKey is required.", nameof(objectKey));
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("FileName is required.", nameof(fileName));

        Id = id;
        ObjectKey = objectKey;
        FileName = fileName;
        ContentType = contentType;
        Size = size;
        CreatedAt = createdAt;
        Status = StoredObjectStatus.Pending;
    }

    public void MarkActive() => Status = StoredObjectStatus.Active;
    public void MarkFailed() => Status = StoredObjectStatus.Failed;
    public void MarkDeleted() => Status = StoredObjectStatus.Deleted;
}

public enum StoredObjectStatus
{
    Pending,
    Active,
    Failed,
    Deleted
}
