namespace ObjectStorage.Api.Contracts;

public sealed record ObjectMetadataResponse(
    string Id,
    string FileName,
    string ContentType,
    long Size,
    DateTimeOffset CreatedAt);
