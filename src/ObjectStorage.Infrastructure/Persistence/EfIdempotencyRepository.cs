using Microsoft.EntityFrameworkCore;
using ObjectStorage.Application.Entities;
using ObjectStorage.Application.Interfaces;

namespace ObjectStorage.Infrastructure.Persistence;

public sealed class EfIdempotencyRepository : IIdempotencyRepository
{
    private readonly ObjectStorageDbContext _dbContext;

    public EfIdempotencyRepository(ObjectStorageDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IdempotencyRecord?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        return await _dbContext.IdempotencyRecords
            .FirstOrDefaultAsync(e => e.Key == key, cancellationToken);
    }

    public async Task AddAsync(IdempotencyRecord record, CancellationToken cancellationToken = default)
    {
        _dbContext.IdempotencyRecords.Add(record);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateStatusAsync(string key, string status, string? objectId = null, CancellationToken cancellationToken = default)
    {
        var record = await _dbContext.IdempotencyRecords
            .FirstOrDefaultAsync(e => e.Key == key, cancellationToken);

        if (record is not null)
        {
            record.Status = status;
            record.ObjectId = objectId;
            if (status is "Completed" or "Failed")
            {
                record.CompletedAt = DateTimeOffset.UtcNow;
            }
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        var record = await _dbContext.IdempotencyRecords
            .FirstOrDefaultAsync(e => e.Key == key, cancellationToken);

        if (record is not null)
        {
            _dbContext.IdempotencyRecords.Remove(record);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
