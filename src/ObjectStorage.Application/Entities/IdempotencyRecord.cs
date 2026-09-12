namespace ObjectStorage.Application.Entities;

public sealed class IdempotencyRecord
{
    public string Key { get; set; } = string.Empty;
    public string Status { get; set; } = "InProgress";
    public string? ObjectId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }
}
