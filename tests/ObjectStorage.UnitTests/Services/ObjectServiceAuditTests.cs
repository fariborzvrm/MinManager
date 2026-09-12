using Microsoft.Extensions.Options;
using ObjectStorage.Application.Configuration;
using ObjectStorage.Application.Interfaces;
using ObjectStorage.Application.Services;
using ObjectStorage.Domain.Entities;
using ObjectStorage.Domain.Interfaces;
using ObjectStorage.Infrastructure.Persistence;
using ObjectStorage.Infrastructure.Services;
using ObjectStorage.UnitTests.Fakes;

namespace ObjectStorage.UnitTests.Services;

public sealed class ObjectServiceAuditTests
{
    private readonly FakeObjectStorage _objectStorage = new();
    private readonly IObjectMetadataRepository _repository = new InMemoryObjectMetadataRepository();
    private readonly IObjectIdGenerator _idGenerator = new UlidObjectIdGenerator();
    private readonly IObjectKeyGenerator _keyGenerator = new HierarchicalObjectKeyGenerator();
    private readonly FakeAuditLogger _auditLogger = new();
    private readonly ObjectService _service;
    private readonly IServiceIdentity _testIdentity;

    public ObjectServiceAuditTests()
    {
        var options = Options.Create(new StorageOptions
        {
            DefaultService = "testsvc",
            AllowedCategories = ["documents", "images", "temp"],
            MaxFileSizeBytes = 104_857_600,
            AllowedContentTypes = ["*"]
        });
        _service = new ObjectService(
            _objectStorage, _repository, _idGenerator, _keyGenerator, options,
            auditLogger: _auditLogger);
        _testIdentity = new ServiceIdentity("testsvc", ["testsvc/"]);
    }

    [Fact]
    public async Task UploadAsync_LogsSuccessAuditEntry()
    {
        const string content = "hello";
        await using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content));

        var result = await _service.UploadAsync(
            stream, _testIdentity, "documents", "test.txt", "text/plain", content.Length,
            correlationId: "corr-001");

        Assert.Single(_auditLogger.Calls);
        var call = _auditLogger.Calls[0];
        Assert.Equal("testsvc", call.ServiceName);
        Assert.Equal("Upload", call.Operation);
        Assert.Equal(result.Id, call.ObjectId);
        Assert.Equal("Success", call.Result);
        Assert.Equal("corr-001", call.CorrelationId);
        Assert.Contains("category=documents", call.Metadata!);
        Assert.Contains("contentType=text/plain", call.Metadata!);
    }

    [Fact]
    public async Task DownloadAsync_LogsSuccessAuditEntry()
    {
        await using var stream = new MemoryStream([1, 2, 3]);
        var stored = await _service.UploadAsync(stream, _testIdentity, "documents", "doc.pdf", "application/pdf", 3);

        _auditLogger.Calls.Clear();
        var downloaded = await _service.DownloadAsync(stored.Id, _testIdentity, correlationId: "corr-002");

        Assert.Single(_auditLogger.Calls);
        var call = _auditLogger.Calls[0];
        Assert.Equal("Download", call.Operation);
        Assert.Equal(stored.Id, call.ObjectId);
        Assert.Equal("Success", call.Result);
        Assert.Equal("corr-002", call.CorrelationId);
        downloaded.Dispose();
    }

    [Fact]
    public async Task GetMetadataAsync_LogsSuccessAuditEntry()
    {
        await using var stream = new MemoryStream([1, 2, 3]);
        var stored = await _service.UploadAsync(stream, _testIdentity, "images", "img.png", "image/png", 3);

        _auditLogger.Calls.Clear();
        await _service.GetMetadataAsync(stored.Id, _testIdentity, correlationId: "corr-003");

        Assert.Single(_auditLogger.Calls);
        var call = _auditLogger.Calls[0];
        Assert.Equal("Metadata", call.Operation);
        Assert.Equal(stored.Id, call.ObjectId);
        Assert.Equal("corr-003", call.CorrelationId);
    }

    [Fact]
    public async Task DeleteAsync_LogsSuccessAuditEntry()
    {
        await using var stream = new MemoryStream([1, 2, 3]);
        var stored = await _service.UploadAsync(stream, _testIdentity, "temp", "file.tmp", "application/octet-stream", 3);

        _auditLogger.Calls.Clear();
        await _service.DeleteAsync(stored.Id, _testIdentity, correlationId: "corr-004");

        Assert.Single(_auditLogger.Calls);
        var call = _auditLogger.Calls[0];
        Assert.Equal("Delete", call.Operation);
        Assert.Equal(stored.Id, call.ObjectId);
        Assert.Equal("Success", call.Result);
        Assert.Equal("corr-004", call.CorrelationId);
    }

    [Fact]
    public async Task GeneratePresignedUrlAsync_LogsSuccessAuditEntry()
    {
        await using var stream = new MemoryStream([1, 2, 3]);
        var stored = await _service.UploadAsync(stream, _testIdentity, "documents", "file.pdf", "application/pdf", 3);

        _auditLogger.Calls.Clear();
        await _service.GeneratePresignedUrlAsync(stored.Id, _testIdentity, TimeSpan.FromHours(1), correlationId: "corr-005");

        Assert.Single(_auditLogger.Calls);
        var call = _auditLogger.Calls[0];
        Assert.Equal("PresignedUrl", call.Operation);
        Assert.Equal(stored.Id, call.ObjectId);
        Assert.Equal("Success", call.Result);
        Assert.Equal("corr-005", call.CorrelationId);
        Assert.Contains("expiresIn=3600", call.Metadata!);
    }

    [Fact]
    public async Task UploadAsync_WithoutAuditLogger_DoesNotThrow()
    {
        var serviceNoAudit = new ObjectService(
            _objectStorage, _repository, _idGenerator, _keyGenerator,
            Options.Create(new StorageOptions
            {
                DefaultService = "testsvc",
                AllowedCategories = ["documents"],
                MaxFileSizeBytes = 104_857_600,
                AllowedContentTypes = ["*"]
            }));

        const string content = "test";
        await using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content));

        var result = await serviceNoAudit.UploadAsync(
            stream, _testIdentity, "documents", "test.txt", "text/plain", content.Length);

        Assert.NotNull(result);
    }

    [Fact]
    public async Task UploadAsync_Failure_LogsFailureAuditEntry()
    {
        var failingStorage = new FailingObjectStorage { ShouldFailUpload = true };
        var service = new ObjectService(
            failingStorage, _repository, _idGenerator, _keyGenerator,
            Options.Create(new StorageOptions
            {
                DefaultService = "testsvc",
                AllowedCategories = ["documents"],
                MaxFileSizeBytes = 104_857_600,
                AllowedContentTypes = ["*"]
            }),
            auditLogger: _auditLogger);

        await using var stream = new MemoryStream([1, 2, 3]);

        await Assert.ThrowsAsync<Domain.Exceptions.StorageUnavailableException>(
            () => service.UploadAsync(stream, _testIdentity, "documents", "fail.txt", "text/plain", 3, correlationId: "corr-fail"));

        Assert.Single(_auditLogger.Calls);
        var call = _auditLogger.Calls[0];
        Assert.Equal("Upload", call.Operation);
        Assert.Equal("Failure", call.Result);
        Assert.Equal("corr-fail", call.CorrelationId);
    }
}
