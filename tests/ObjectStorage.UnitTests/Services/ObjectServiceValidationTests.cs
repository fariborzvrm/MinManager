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

public sealed class ObjectServiceValidationTests
{
    private readonly FakeObjectStorage _objectStorage = new();
    private readonly IObjectMetadataRepository _repository = new InMemoryObjectMetadataRepository();
    private readonly ObjectService _service;
    private readonly IServiceIdentity _testIdentity;

    public ObjectServiceValidationTests()
    {
        var options = new StorageOptions
        {
            DefaultService = "testsvc",
            AllowedCategories = ["documents", "images"],
            MaxFileSizeBytes = 1024,
            AllowedContentTypes = ["application/pdf", "image/png", "text/plain"]
        };
        var optionsWrapper = Options.Create(options);
        _service = new ObjectService(
            _objectStorage,
            _repository,
            new UlidObjectIdGenerator(),
            new HierarchicalObjectKeyGenerator(),
            optionsWrapper);
        _testIdentity = new ServiceIdentity("testsvc", ["testsvc/"]);
    }

    [Fact]
    public async Task UploadAsync_EmptyFile_ThrowsInvalidRequestException()
    {
        await using var stream = new MemoryStream([]);

        await Assert.ThrowsAsync<InvalidRequestException>(
            () => _service.UploadAsync(stream, _testIdentity, "documents", "empty.txt", "text/plain", 0));
    }

    [Fact]
    public async Task UploadAsync_UnknownCategory_ThrowsCategoryNotAllowedException()
    {
        await using var stream = new MemoryStream([1, 2, 3]);

        var ex = await Assert.ThrowsAsync<CategoryNotAllowedException>(
            () => _service.UploadAsync(stream, _testIdentity, "invoices", "file.pdf", "application/pdf", 3));

        Assert.Equal("invoices", ex.Category);
    }

    [Fact]
    public async Task UploadAsync_FileExceedsMaxSize_ThrowsFileTooLargeException()
    {
        await using var stream = new MemoryStream(new byte[2048]);

        var ex = await Assert.ThrowsAsync<FileTooLargeException>(
            () => _service.UploadAsync(stream, _testIdentity, "documents", "big.pdf", "application/pdf", 2048));

        Assert.Equal(2048, ex.ContentLength);
        Assert.Equal(1024, ex.MaxSizeBytes);
    }

    [Fact]
    public async Task UploadAsync_UnsupportedContentType_ThrowsUnsupportedContentTypeException()
    {
        await using var stream = new MemoryStream([1, 2, 3]);

        var ex = await Assert.ThrowsAsync<UnsupportedContentTypeException>(
            () => _service.UploadAsync(stream, _testIdentity, "documents", "file.exe", "application/x-executable", 3));

        Assert.Equal("application/x-executable", ex.ContentType);
    }

    [Fact]
    public async Task UploadAsync_ValidRequest_Succeeds()
    {
        await using var stream = new MemoryStream([1, 2, 3]);

        var result = await _service.UploadAsync(stream, _testIdentity, "documents", "file.pdf", "application/pdf", 3);

        Assert.NotNull(result);
        Assert.Equal("file.pdf", result.FileName);
    }

    [Fact]
    public async Task UploadAsync_EmptyContentType_DefaultsToOctetStream()
    {
        var options = new StorageOptions
        {
            DefaultService = "testsvc",
            AllowedCategories = ["documents"],
            MaxFileSizeBytes = 10240,
            AllowedContentTypes = ["application/pdf", "application/octet-stream"]
        };
        var optionsWrapper = Options.Create(options);
        var service = new ObjectService(
            _objectStorage,
            _repository,
            new UlidObjectIdGenerator(),
            new HierarchicalObjectKeyGenerator(),
            optionsWrapper);

        await using var stream = new MemoryStream([1, 2, 3]);

        var result = await service.UploadAsync(stream, _testIdentity, "documents", "file.pdf", "", 3);

        Assert.NotNull(result);
    }

    [Fact]
    public async Task UploadAsync_AllowedContentType_WildcardPassesAll()
    {
        var options = new StorageOptions
        {
            DefaultService = "testsvc",
            AllowedCategories = ["documents"],
            MaxFileSizeBytes = 10240,
            AllowedContentTypes = ["*"]
        };
        var optionsWrapper = Options.Create(options);
        var service = new ObjectService(
            _objectStorage,
            _repository,
            new UlidObjectIdGenerator(),
            new HierarchicalObjectKeyGenerator(),
            optionsWrapper);

        await using var stream = new MemoryStream([1, 2, 3]);

        var result = await service.UploadAsync(stream, _testIdentity, "documents", "file.exe", "application/x-executable", 3);

        Assert.NotNull(result);
    }

    [Fact]
    public async Task DownloadAsync_UnknownId_ThrowsObjectNotFoundException()
    {
        await Assert.ThrowsAsync<ObjectNotFoundException>(() => _service.DownloadAsync("missing-id", _testIdentity));
    }

    [Fact]
    public async Task DeleteAsync_UnknownId_ThrowsObjectNotFoundException()
    {
        await Assert.ThrowsAsync<ObjectNotFoundException>(() => _service.DeleteAsync("missing-id", _testIdentity));
    }
}
