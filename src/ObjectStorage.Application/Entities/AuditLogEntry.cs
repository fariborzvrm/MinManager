namespace ObjectStorage.Application.Entities;

public sealed class AuditLogEntry
{
    public long Id { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public string ServiceName { get; set; } = string.Empty;
    public string Operation { get; set; } = string.Empty;
    public string? ObjectId { get; set; }
    public string Result { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
    public string? IpAddress { get; set; }
    public string? Metadata { get; set; }
}
