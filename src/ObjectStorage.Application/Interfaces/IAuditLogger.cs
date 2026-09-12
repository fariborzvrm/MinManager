namespace ObjectStorage.Application.Interfaces;

public interface IAuditLogger
{
    Task LogAsync(
        string serviceName,
        string operation,
        string? objectId,
        string result,
        string correlationId,
        string? ipAddress = null,
        string? metadata = null,
        CancellationToken cancellationToken = default);
}
