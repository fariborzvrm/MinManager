using ObjectStorage.Application.Entities;

namespace ObjectStorage.Application.Interfaces;

public interface IIdempotencyRepository
{
    Task<IdempotencyRecord?> GetAsync(string key, CancellationToken cancellationToken = default);
    Task AddAsync(IdempotencyRecord record, CancellationToken cancellationToken = default);
    Task UpdateStatusAsync(string key, string status, string? objectId = null, CancellationToken cancellationToken = default);
    Task RemoveAsync(string key, CancellationToken cancellationToken = default);
}
