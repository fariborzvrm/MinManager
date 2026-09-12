using Microsoft.Extensions.Options;
using ObjectStorage.Application.Configuration;
using ObjectStorage.Application.Interfaces;
using ObjectStorage.Application.Services;
using ObjectStorage.Domain.Entities;
using ObjectStorage.Domain.Exceptions;
using ObjectStorage.Domain.Interfaces;
using ObjectStorage.Infrastructure.Persistence;
using ObjectStorage.Infrastructure.Services;
using ObjectStorage.UnitTests.Fakes;

namespace ObjectStorage.UnitTests.Services;

public sealed class ObjectServiceTests
{
    private readonly FakeObjectStorage _objectStorage = new();
    private readonly IObjectMetadataRepository _repository = new InMemoryObjectMetadataRepository();
    private readonly IObjectIdGenerator _idGenerator = new UlidObjectIdGenerator();
    private readonly IObjectKeyGenerator _keyGenerator = new HierarchicalObjectKeyGenerator();
    private readonly ObjectService _service;
    private readonly IServiceIdentity _testIdentity;

    public ObjectServiceTests()
    {
        var options = Options.Create(new StorageOptions
        {
            DefaultService = "testsvc",
            AllowedCategories = ["documents", "images", "temp"],
            MaxFileSizeBytes = 104_857_600,
            AllowedContentTypes = ["*"]
        });
        _service = new ObjectService(_objectStorage, _repository, _idGenerator, _keyGenerator, options);
        _testIdentity = new ServiceIdentity("testsvc", ["testsvc/"]);
    }

    [Fact]
    public async Task UploadAsync_StoresMetadataAndBytes()
    {
        const string content = "hello world";
        await using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content));

        var result = await _service.UploadAsync(stream, _testIdentity, "documents", "test.txt", "text/plain", content.Length);

        Assert.NotNull(result);
        Assert.Equal("test.txt", result.FileName);
        Assert.Equal("text/plain", result.ContentType);
        Assert.Equal(content.Length, result.Size);
        Assert.Contains(result.Id, result.ObjectKey);
        Assert.StartsWith("testsvc/documents/", result.ObjectKey);
        Assert.Single(_objectStorage.Objects);
        Assert.True(_objectStorage.Objects.ContainsKey(result.ObjectKey));
    }

    [Fact]
    public async Task DownloadAsync_ReturnsStoredStream()
    {
        const string content = "download me";
        await using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content));
        var stored = await _service.UploadAsync(stream, _testIdentity, "documents", "doc.pdf", "application/pdf", content.Length);

        var downloaded = await _service.DownloadAsync(stored.Id, _testIdentity);
        using var reader = new StreamReader(downloaded);
        var actual = await reader.ReadToEndAsync();

        Assert.Equal(content, actual);
    }

    [Fact]
    public async Task DownloadAsync_UnknownId_ThrowsObjectNotFoundException()
    {
        await Assert.ThrowsAsync<ObjectNotFoundException>(() => _service.DownloadAsync("missing-id", _testIdentity));
    }

    [Fact]
    public async Task GetMetadataAsync_ReturnsMetadata()
    {
        await using var stream = new MemoryStream([1, 2, 3]);
        var stored = await _service.UploadAsync(stream, _testIdentity, "images", "image.png", "image/png", 3);

        var metadata = await _service.GetMetadataAsync(stored.Id, _testIdentity);

        Assert.Equal(stored.Id, metadata.Id);
        Assert.Equal(stored.ObjectKey, metadata.ObjectKey);
    }

    [Fact]
    public async Task GetMetadataAsync_UnknownId_ThrowsObjectNotFoundException()
    {
        await Assert.ThrowsAsync<ObjectNotFoundException>(() => _service.GetMetadataAsync("missing-id", _testIdentity));
    }

    [Fact]
    public async Task DeleteAsync_RemovesMetadataAndBytes()
    {
        await using var stream = new MemoryStream([1, 2, 3]);
        var stored = await _service.UploadAsync(stream, _testIdentity, "temp", "delete.me", "application/octet-stream", 3);

        await _service.DeleteAsync(stored.Id, _testIdentity);

        Assert.Empty(_objectStorage.Objects);
        await Assert.ThrowsAsync<ObjectNotFoundException>(() => _service.GetMetadataAsync(stored.Id));
    }

    [Fact]
    public async Task DeleteAsync_UnknownId_ThrowsObjectNotFoundException()
    {
        await Assert.ThrowsAsync<ObjectNotFoundException>(() => _service.DeleteAsync("missing-id", _testIdentity));
    }

    [Fact]
    public async Task UploadAsync_CrossServiceAccess_ThrowsForbiddenAccessException()
    {
        var otherServiceIdentity = new ServiceIdentity("othersvc", ["othersvc/"]);
        await using var stream = new MemoryStream([1, 2, 3]);

        // Upload with testsvc identity (creates key "testsvc/documents/...")
        var stored = await _service.UploadAsync(stream, _testIdentity, "documents", "file.txt", "text/plain", 3);

        // Try to download with othersvc identity — should be forbidden
        await Assert.ThrowsAsync<ForbiddenAccessException>(
            () => _service.DownloadAsync(stored.Id, otherServiceIdentity));
    }

    [Fact]
    public async Task DownloadAsync_CrossServiceAccess_ThrowsForbiddenAccessException()
    {
        await using var stream = new MemoryStream([1, 2, 3]);
        var stored = await _service.UploadAsync(stream, _testIdentity, "documents", "file.txt", "text/plain", 3);

        var otherServiceIdentity = new ServiceIdentity("othersvc", ["othersvc/"]);

        await Assert.ThrowsAsync<ForbiddenAccessException>(
            () => _service.DownloadAsync(stored.Id, otherServiceIdentity));
    }

    [Fact]
    public async Task DeleteAsync_CrossServiceAccess_ThrowsForbiddenAccessException()
    {
        await using var stream = new MemoryStream([1, 2, 3]);
        var stored = await _service.UploadAsync(stream, _testIdentity, "documents", "file.txt", "text/plain", 3);

        var otherServiceIdentity = new ServiceIdentity("othersvc", ["othersvc/"]);

        await Assert.ThrowsAsync<ForbiddenAccessException>(
            () => _service.DeleteAsync(stored.Id, otherServiceIdentity));
    }

    [Fact]
    public async Task GeneratePresignedUrlAsync_ReturnsUrlForExistingObject()
    {
        await using var stream = new MemoryStream([1, 2, 3]);
        var stored = await _service.UploadAsync(stream, _testIdentity, "documents", "file.pdf", "application/pdf", 3);

        var url = await _service.GeneratePresignedUrlAsync(stored.Id, _testIdentity, TimeSpan.FromHours(1));

        Assert.NotNull(url);
        Assert.Contains(stored.ObjectKey, url);
    }

    [Fact]
    public async Task GeneratePresignedUrlAsync_UnknownId_ThrowsObjectNotFoundException()
    {
        await Assert.ThrowsAsync<ObjectNotFoundException>(
            () => _service.GeneratePresignedUrlAsync("missing-id", _testIdentity, TimeSpan.FromHours(1)));
    }

    [Fact]
    public async Task GeneratePresignedUrlAsync_CrossServiceAccess_ThrowsForbiddenAccessException()
    {
        await using var stream = new MemoryStream([1, 2, 3]);
        var stored = await _service.UploadAsync(stream, _testIdentity, "documents", "file.txt", "text/plain", 3);

        var otherServiceIdentity = new ServiceIdentity("othersvc", ["othersvc/"]);

        await Assert.ThrowsAsync<ForbiddenAccessException>(
            () => _service.GeneratePresignedUrlAsync(stored.Id, otherServiceIdentity, TimeSpan.FromHours(1)));
    }

    [Fact]
    public async Task GeneratePresignedUrlAsync_ZeroExpiration_ThrowsInvalidRequestException()
    {
        await using var stream = new MemoryStream([1, 2, 3]);
        var stored = await _service.UploadAsync(stream, _testIdentity, "documents", "file.txt", "text/plain", 3);

        await Assert.ThrowsAsync<InvalidRequestException>(
            () => _service.GeneratePresignedUrlAsync(stored.Id, _testIdentity, TimeSpan.Zero));
    }

    [Fact]
    public async Task GeneratePresignedUrlAsync_NegativeExpiration_ThrowsInvalidRequestException()
    {
        await using var stream = new MemoryStream([1, 2, 3]);
        var stored = await _service.UploadAsync(stream, _testIdentity, "documents", "file.txt", "text/plain", 3);

        await Assert.ThrowsAsync<InvalidRequestException>(
            () => _service.GeneratePresignedUrlAsync(stored.Id, _testIdentity, TimeSpan.FromSeconds(-1)));
    }

    [Fact]
    public async Task GeneratePresignedUrlAsync_ExceedsMaxExpiration_ThrowsInvalidRequestException()
    {
        var options = new StorageOptions
        {
            DefaultService = "testsvc",
            AllowedCategories = ["documents"],
            MaxFileSizeBytes = 104_857_600,
            AllowedContentTypes = ["*"],
            MaxPresignedUrlExpirationSeconds = 3600 // 1 hour
        };
        var service = new ObjectService(
            _objectStorage,
            _repository,
            new UlidObjectIdGenerator(),
            new HierarchicalObjectKeyGenerator(),
            Options.Create(options));

        await using var stream = new MemoryStream([1, 2, 3]);
        var stored = await service.UploadAsync(stream, _testIdentity, "documents", "file.txt", "text/plain", 3);

        await Assert.ThrowsAsync<InvalidRequestException>(
            () => service.GeneratePresignedUrlAsync(stored.Id, _testIdentity, TimeSpan.FromHours(2)));
    }
}
