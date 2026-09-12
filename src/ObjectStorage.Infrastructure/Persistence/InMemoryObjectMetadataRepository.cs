using System.Collections.Concurrent;
using ObjectStorage.Application.Interfaces;
using ObjectStorage.Domain.Entities;

namespace ObjectStorage.Infrastructure.Persistence;

public sealed class InMemoryObjectMetadataRepository : IObjectMetadataRepository
{
    private readonly ConcurrentDictionary<string, StoredObject> _store = new();

    public Task<StoredObject?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        _store.TryGetValue(id, out var storedObject);
        return Task.FromResult(storedObject);
    }

    public Task AddAsync(StoredObject storedObject, CancellationToken cancellationToken = default)
    {
        _store[storedObject.Id] = storedObject;
        return Task.CompletedTask;
    }

    public Task UpdateStatusAsync(string id, StoredObjectStatus status, CancellationToken cancellationToken = default)
    {
        if (_store.TryGetValue(id, out var storedObject))
        {
            switch (status)
            {
                case StoredObjectStatus.Active:
                    storedObject.MarkActive();
                    break;
                case StoredObjectStatus.Failed:
                    storedObject.MarkFailed();
                    break;
                case StoredObjectStatus.Deleted:
                    storedObject.MarkDeleted();
                    break;
            }
        }
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        _store.TryRemove(id, out _);
        return Task.CompletedTask;
    }
}
