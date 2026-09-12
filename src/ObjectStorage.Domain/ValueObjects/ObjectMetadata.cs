namespace ObjectStorage.Domain.ValueObjects;

public sealed record ObjectMetadata(
    string ObjectKey,
    long Size,
    string ContentType);