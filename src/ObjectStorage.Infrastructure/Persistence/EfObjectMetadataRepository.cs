using Microsoft.EntityFrameworkCore;
using ObjectStorage.Application.Interfaces;
using ObjectStorage.Domain.Entities;

namespace ObjectStorage.Infrastructure.Persistence;

public sealed class EfObjectMetadataRepository : IObjectMetadataRepository
{
    private readonly ObjectStorageDbContext _dbContext;

    public EfObjectMetadataRepository(ObjectStorageDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<StoredObject?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.StoredObjects
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id && e.Status == StoredObjectStatus.Active, cancellationToken);
    }

    public async Task AddAsync(StoredObject storedObject, CancellationToken cancellationToken = default)
    {
        _dbContext.StoredObjects.Add(storedObject);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateStatusAsync(string id, StoredObjectStatus status, CancellationToken cancellationToken = default)
    {
        var storedObject = await _dbContext.StoredObjects
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

        if (storedObject is not null)
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
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        var storedObject = await _dbContext.StoredObjects
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

        if (storedObject is not null)
        {
            storedObject.MarkDeleted();
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
