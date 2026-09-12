using System.ComponentModel.DataAnnotations;

namespace ObjectStorage.Application.Configuration;

public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    [Required]
    public string DefaultService { get; set; } = "objectstorage";

    [Required]
    public string[] AllowedCategories { get; set; } = [];

    [Range(1, long.MaxValue)]
    public long MaxFileSizeBytes { get; set; } = 104_857_600; // 100 MB default

    public string[] AllowedContentTypes { get; set; } =
        ["application/pdf", "image/png", "image/jpeg", "text/plain", "application/octet-stream"];

    [Range(1, int.MaxValue)]
    public int MaxPresignedUrlExpirationSeconds { get; set; } = 604800; // 7 days default
}
