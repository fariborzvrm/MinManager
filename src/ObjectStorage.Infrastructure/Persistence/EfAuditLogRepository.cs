using Microsoft.EntityFrameworkCore;
using ObjectStorage.Application.Entities;
using ObjectStorage.Application.Interfaces;

namespace ObjectStorage.Infrastructure.Persistence;

public sealed class EfAuditLogRepository : IAuditLogger
{
    private readonly ObjectStorageDbContext _dbContext;

    public EfAuditLogRepository(ObjectStorageDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task LogAsync(
        string serviceName,
        string operation,
        string? objectId,
        string result,
        string correlationId,
        string? ipAddress = null,
        string? metadata = null,
        CancellationToken cancellationToken = default)
    {
        var entry = new AuditLogEntry
        {
            Timestamp = DateTimeOffset.UtcNow,
            ServiceName = serviceName,
            Operation = operation,
            ObjectId = objectId,
            Result = result,
            CorrelationId = correlationId,
            IpAddress = ipAddress,
            Metadata = metadata
        };

        _dbContext.AuditLogEntries.Add(entry);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
