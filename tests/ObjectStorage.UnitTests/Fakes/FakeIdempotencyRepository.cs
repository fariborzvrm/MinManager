using System.Collections.Concurrent;
using ObjectStorage.Application.Entities;
using ObjectStorage.Application.Interfaces;

namespace ObjectStorage.UnitTests.Fakes;

public sealed class FakeIdempotencyRepository : IIdempotencyRepository
{
    private readonly ConcurrentDictionary<string, IdempotencyRecord> _store = new();

    public IReadOnlyDictionary<string, IdempotencyRecord> Records => _store;

    public Task<IdempotencyRecord?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        _store.TryGetValue(key, out var record);
        return Task.FromResult(record);
    }

    public Task AddAsync(IdempotencyRecord record, CancellationToken cancellationToken = default)
    {
        _store[record.Key] = record;
        return Task.CompletedTask;
    }

    public Task UpdateStatusAsync(string key, string status, string? objectId = null, CancellationToken cancellationToken = default)
    {
        if (_store.TryGetValue(key, out var record))
        {
            record.Status = status;
            record.ObjectId = objectId;
            if (status is "Completed" or "Failed")
            {
                record.CompletedAt = DateTimeOffset.UtcNow;
            }
        }
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        _store.TryRemove(key, out _);
        return Task.CompletedTask;
    }
}
