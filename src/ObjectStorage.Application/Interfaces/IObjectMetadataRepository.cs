using ObjectStorage.Domain.Entities;

namespace ObjectStorage.Application.Interfaces;

public interface IObjectMetadataRepository
{
    Task<StoredObject?> GetByIdAsync(string id, CancellationToken cancellationToken = default);
    Task AddAsync(StoredObject storedObject, CancellationToken cancellationToken = default);
    Task UpdateStatusAsync(string id, StoredObjectStatus status, CancellationToken cancellationToken = default);
    Task DeleteAsync(string id, CancellationToken cancellationToken = default);
}
