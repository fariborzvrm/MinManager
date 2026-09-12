using System.ComponentModel.DataAnnotations;

namespace ObjectStorage.Api.Contracts;

public sealed record PresignedUrlRequest
{
    [Range(1, int.MaxValue)]
    public int ExpirationSeconds { get; init; }
}
