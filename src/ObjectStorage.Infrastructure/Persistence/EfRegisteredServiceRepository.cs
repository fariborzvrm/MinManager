using Microsoft.EntityFrameworkCore;
using ObjectStorage.Infrastructure.Entities;

namespace ObjectStorage.Infrastructure.Persistence;

public sealed class EfRegisteredServiceRepository : IRegisteredServiceRepository
{
    private readonly ObjectStorageDbContext _dbContext;

    public EfRegisteredServiceRepository(ObjectStorageDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<RegisteredService?> GetByApiKeyHashAsync(string apiKeyHash, CancellationToken cancellationToken = default)
    {
        return await _dbContext.RegisteredServices
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.ApiKeyHash == apiKeyHash && s.Active, cancellationToken);
    }

    public async Task<RegisteredService?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        return await _dbContext.RegisteredServices
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Name == name && s.Active, cancellationToken);
    }

    public async Task AddAsync(RegisteredService service, CancellationToken cancellationToken = default)
    {
        _dbContext.RegisteredServices.Add(service);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<RegisteredService>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.RegisteredServices
            .AsNoTracking()
            .OrderBy(s => s.Name)
            .ToListAsync(cancellationToken);
    }
}
