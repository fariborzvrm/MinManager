using ObjectStorage.Domain.Entities;
using ObjectStorage.Infrastructure.Persistence;

namespace ObjectStorage.UnitTests.Persistence;

public sealed class InMemoryObjectMetadataRepositoryTests
{
    private readonly InMemoryObjectMetadataRepository _repository = new();

    [Fact]
    public async Task AddAndGet_RoundTripsObject()
    {
        var obj = new StoredObject("id-1", "key-1", "file.txt", "text/plain", 10);

        await _repository.AddAsync(obj);
        var retrieved = await _repository.GetByIdAsync("id-1");

        Assert.NotNull(retrieved);
        Assert.Equal("id-1", retrieved.Id);
        Assert.Equal("file.txt", retrieved.FileName);
    }

    [Fact]
    public async Task GetByIdAsync_UnknownId_ReturnsNull()
    {
        var retrieved = await _repository.GetByIdAsync("unknown");
        Assert.Null(retrieved);
    }

    [Fact]
    public async Task DeleteAsync_RemovesObject()
    {
        var obj = new StoredObject("id-2", "key-2", "file.txt", "text/plain", 10);
        await _repository.AddAsync(obj);

        await _repository.DeleteAsync("id-2");
        var retrieved = await _repository.GetByIdAsync("id-2");

        Assert.Null(retrieved);
    }
}
