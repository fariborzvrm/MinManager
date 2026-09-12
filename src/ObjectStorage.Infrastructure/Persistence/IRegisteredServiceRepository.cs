using ObjectStorage.Infrastructure.Entities;

namespace ObjectStorage.Infrastructure.Persistence;

public interface IRegisteredServiceRepository
{
    Task<RegisteredService?> GetByApiKeyHashAsync(string apiKeyHash, CancellationToken cancellationToken = default);
    Task<RegisteredService?> GetByNameAsync(string name, CancellationToken cancellationToken = default);
    Task AddAsync(RegisteredService service, CancellationToken cancellationToken = default);
    Task<List<RegisteredService>> GetAllAsync(CancellationToken cancellationToken = default);
}
