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

public sealed class ObjectServiceIdempotencyTests
{
    private readonly FakeObjectStorage _objectStorage = new();
    private readonly IObjectMetadataRepository _repository = new InMemoryObjectMetadataRepository();
    private readonly FakeIdempotencyRepository _idempotencyRepo = new();
    private readonly ObjectService _service;
    private readonly IServiceIdentity _testIdentity;

    public ObjectServiceIdempotencyTests()
    {
        var options = Options.Create(new StorageOptions
        {
            DefaultService = "testsvc",
            AllowedCategories = ["documents"],
            MaxFileSizeBytes = 104_857_600,
            AllowedContentTypes = ["*"]
        });
        _service = new ObjectService(
            _objectStorage,
            _repository,
            new UlidObjectIdGenerator(),
            new HierarchicalObjectKeyGenerator(),
            options,
            _idempotencyRepo);
        _testIdentity = new ServiceIdentity("testsvc", ["testsvc/"]);
    }

    [Fact]
    public async Task UploadAsync_WithoutIdempotencyKey_SucceedsWithoutRecord()
    {
        await using var stream = new MemoryStream([1, 2, 3]);

        var result = await _service.UploadAsync(
            stream, _testIdentity, "documents", "file.pdf", "application/pdf", 3);

        Assert.NotNull(result);
        Assert.Empty(_idempotencyRepo.Records);
    }

    [Fact]
    public async Task UploadAsync_WithIdempotencyKey_CreatesRecordAndCompletes()
    {
        await using var stream = new MemoryStream([1, 2, 3]);

        var result = await _service.UploadAsync(
            stream, _testIdentity, "documents", "file.pdf", "application/pdf", 3,
            idempotencyKey: "key-123");

        Assert.NotNull(result);
        Assert.Single(_idempotencyRepo.Records);
        Assert.Equal("Completed", _idempotencyRepo.Records["key-123"].Status);
        Assert.Equal(result.Id, _idempotencyRepo.Records["key-123"].ObjectId);
    }

    [Fact]
    public async Task UploadAsync_DuplicateKeyWhileInProgress_ThrowsInvalidRequestException()
    {
        await using var stream1 = new MemoryStream([1, 2, 3]);
        await _service.UploadAsync(
            stream1, _testIdentity, "documents", "file.pdf", "application/pdf", 3,
            idempotencyKey: "key-dup");

        // Manually set status back to InProgress to simulate in-flight duplicate
        await _idempotencyRepo.UpdateStatusAsync("key-dup", "InProgress");

        await using var stream2 = new MemoryStream([4, 5, 6]);
        await Assert.ThrowsAsync<InvalidRequestException>(
            () => _service.UploadAsync(
                stream2, _testIdentity, "documents", "file2.pdf", "application/pdf", 3,
                idempotencyKey: "key-dup"));
    }

    [Fact]
    public async Task UploadAsync_DuplicateKeyAfterCompleted_ReturnsSameResult()
    {
        await using var stream1 = new MemoryStream([1, 2, 3]);
        var first = await _service.UploadAsync(
            stream1, _testIdentity, "documents", "file.pdf", "application/pdf", 3,
            idempotencyKey: "key-done");

        await using var stream2 = new MemoryStream([4, 5, 6]);
        var second = await _service.UploadAsync(
            stream2, _testIdentity, "documents", "file2.pdf", "application/pdf", 3,
            idempotencyKey: "key-done");

        Assert.Equal(first.Id, second.Id);
        Assert.Equal(first.ObjectKey, second.ObjectKey);
        // Only one object stored (no re-upload)
        Assert.Single(_objectStorage.Objects);
    }

    [Fact]
    public async Task UploadAsync_DuplicateKeyAfterFailed_AllowsRetry()
    {
        var failingStorage = new FailingObjectStorage { ShouldFailUpload = true };
        var service = new ObjectService(
            failingStorage,
            _repository,
            new UlidObjectIdGenerator(),
            new HierarchicalObjectKeyGenerator(),
            Options.Create(new StorageOptions
            {
                DefaultService = "testsvc",
                AllowedCategories = ["documents"],
                MaxFileSizeBytes = 104_857_600,
                AllowedContentTypes = ["*"]
            }),
            _idempotencyRepo);

        // First attempt fails
        await using var stream1 = new MemoryStream([1, 2, 3]);
        await Assert.ThrowsAsync<StorageUnavailableException>(
            () => service.UploadAsync(
                stream1, _testIdentity, "documents", "file.pdf", "application/pdf", 3,
                idempotencyKey: "key-retry"));

        Assert.Equal("Failed", _idempotencyRepo.Records["key-retry"].Status);

        // Second attempt with working storage should succeed
        var workingService = new ObjectService(
            _objectStorage,
            _repository,
            new UlidObjectIdGenerator(),
            new HierarchicalObjectKeyGenerator(),
            Options.Create(new StorageOptions
            {
                DefaultService = "testsvc",
                AllowedCategories = ["documents"],
                MaxFileSizeBytes = 104_857_600,
                AllowedContentTypes = ["*"]
            }),
            _idempotencyRepo);

        await using var stream2 = new MemoryStream([4, 5, 6]);
        var result = await workingService.UploadAsync(
            stream2, _testIdentity, "documents", "file.pdf", "application/pdf", 3,
            idempotencyKey: "key-retry");

        Assert.NotNull(result);
        Assert.Equal("Completed", _idempotencyRepo.Records["key-retry"].Status);
    }

    [Fact]
    public async Task UploadAsync_MinioFailure_SetsMetadataFailed()
    {
        var failingStorage = new FailingObjectStorage { ShouldFailUpload = true };
        var service = new ObjectService(
            failingStorage,
            _repository,
            new UlidObjectIdGenerator(),
            new HierarchicalObjectKeyGenerator(),
            Options.Create(new StorageOptions
            {
                DefaultService = "testsvc",
                AllowedCategories = ["documents"],
                MaxFileSizeBytes = 104_857_600,
                AllowedContentTypes = ["*"]
            }));

        await using var stream = new MemoryStream([1, 2, 3]);
        await Assert.ThrowsAsync<StorageUnavailableException>(
            () => service.UploadAsync(
                stream, _testIdentity, "documents", "file.pdf", "application/pdf", 3));
    }

    [Fact]
    public async Task UploadAsync_MinioFailureWithIdempotencyKey_SetsBothRecordsFailed()
    {
        var failingStorage = new FailingObjectStorage { ShouldFailUpload = true };
        var service = new ObjectService(
            failingStorage,
            _repository,
            new UlidObjectIdGenerator(),
            new HierarchicalObjectKeyGenerator(),
            Options.Create(new StorageOptions
            {
                DefaultService = "testsvc",
                AllowedCategories = ["documents"],
                MaxFileSizeBytes = 104_857_600,
                AllowedContentTypes = ["*"]
            }),
            _idempotencyRepo);

        await using var stream = new MemoryStream([1, 2, 3]);
        await Assert.ThrowsAsync<StorageUnavailableException>(
            () => service.UploadAsync(
                stream, _testIdentity, "documents", "file.pdf", "application/pdf", 3,
                idempotencyKey: "key-fail"));

        Assert.Equal("Failed", _idempotencyRepo.Records["key-fail"].Status);
    }
}
