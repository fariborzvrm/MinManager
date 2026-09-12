namespace ObjectStorage.Api.Contracts;

public sealed record PresignedUrlResponse(
    string Url,
    DateTimeOffset ExpiresAt);
