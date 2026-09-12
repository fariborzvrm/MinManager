using ObjectStorage.Application.Interfaces;

namespace ObjectStorage.UnitTests.Fakes;

public sealed class FakeAuditLogger : IAuditLogger
{
    public List<AuditCall> Calls { get; } = [];

    public Task LogAsync(
        string serviceName,
        string operation,
        string? objectId,
        string result,
        string correlationId,
        string? ipAddress = null,
        string? metadata = null,
        CancellationToken cancellationToken = default)
    {
        Calls.Add(new AuditCall(serviceName, operation, objectId, result, correlationId, ipAddress, metadata));
        return Task.CompletedTask;
    }

    public sealed record AuditCall(
        string ServiceName,
        string Operation,
        string? ObjectId,
        string Result,
        string CorrelationId,
        string? IpAddress,
        string? Metadata);
}
