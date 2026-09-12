using Microsoft.Extensions.Options;
using ObjectStorage.Application.Configuration;
using ObjectStorage.Application.Interfaces;
using ObjectStorage.Domain.Entities;
using ObjectStorage.Domain.Exceptions;
using ObjectStorage.Domain.Interfaces;
using ObjectStorage.Domain.ValueObjects;

namespace ObjectStorage.Application.Services;

public sealed class ObjectService
{
    private readonly IObjectStorage _objectStorage;
    private readonly IObjectMetadataRepository _metadataRepository;
    private readonly IObjectIdGenerator _idGenerator;
    private readonly IObjectKeyGenerator _keyGenerator;
    private readonly StorageOptions _storageOptions;
    private readonly IIdempotencyRepository? _idempotencyRepository;
    private readonly IAuditLogger? _auditLogger;

    public ObjectService(
        IObjectStorage objectStorage,
        IObjectMetadataRepository metadataRepository,
        IObjectIdGenerator idGenerator,
        IObjectKeyGenerator keyGenerator,
        IOptions<StorageOptions> storageOptions,
        IIdempotencyRepository? idempotencyRepository = null,
        IAuditLogger? auditLogger = null)
    {
        _objectStorage = objectStorage;
        _metadataRepository = metadataRepository;
        _idGenerator = idGenerator;
        _keyGenerator = keyGenerator;
        _storageOptions = storageOptions.Value;
        _idempotencyRepository = idempotencyRepository;
        _auditLogger = auditLogger;
    }

    public async Task<StoredObject> UploadAsync(
        Stream content,
        IServiceIdentity serviceIdentity,
        string category,
        string fileName,
        string contentType,
        long contentLength,
        CancellationToken cancellationToken = default,
        string? idempotencyKey = null,
        string? correlationId = null)
    {
        var service = serviceIdentity.Name;
        ValidateUploadRequest(service, category, fileName, contentType, contentLength);

        if (!string.IsNullOrEmpty(idempotencyKey) && _idempotencyRepository is not null)
        {
            var existing = await _idempotencyRepository.GetAsync(idempotencyKey, cancellationToken);
            if (existing is not null)
            {
                var result = await HandleIdempotencyRetryAsync(existing, idempotencyKey, serviceIdentity, cancellationToken);
                if (result is not null)
                {
                    return result;
                }
                // Failed record removed — fall through to create new InProgress record
            }

            await _idempotencyRepository.AddAsync(new Entities.IdempotencyRecord
            {
                Key = idempotencyKey,
                Status = "InProgress",
                CreatedAt = DateTimeOffset.UtcNow
            }, cancellationToken);
        }

        var id = _idGenerator.Generate();
        var objectKey = _keyGenerator.Generate(service, category, id, fileName);

        if (!serviceIdentity.IsAuthorizedForObject(objectKey))
        {
            throw new ForbiddenAccessException(serviceIdentity.Name, objectKey);
        }

        var metadata = new StoredObject(id, objectKey, fileName, contentType, contentLength);
        await _metadataRepository.AddAsync(metadata, cancellationToken);

        try
        {
            await _objectStorage.UploadAsync(
                content,
                objectKey,
                contentType,
                contentLength,
                cancellationToken);

            metadata.MarkActive();
            await _metadataRepository.UpdateStatusAsync(id, StoredObjectStatus.Active, cancellationToken);

            if (!string.IsNullOrEmpty(idempotencyKey) && _idempotencyRepository is not null)
            {
                await _idempotencyRepository.UpdateStatusAsync(idempotencyKey, "Completed", id, cancellationToken);
            }

            await AuditLogAsync(service, "Upload", id, "Success", correlationId,
                metadata: $"category={category};contentType={contentType};size={contentLength}");

            return metadata;
        }
        catch
        {
            metadata.MarkFailed();
            await _metadataRepository.UpdateStatusAsync(id, StoredObjectStatus.Failed, cancellationToken);

            if (!string.IsNullOrEmpty(idempotencyKey) && _idempotencyRepository is not null)
            {
                await _idempotencyRepository.UpdateStatusAsync(idempotencyKey, "Failed", id, cancellationToken);
            }

            await AuditLogAsync(service, "Upload", id, "Failure", correlationId,
                metadata: $"category={category};contentType={contentType};size={contentLength}");

            throw;
        }
    }

    private async Task<StoredObject?> HandleIdempotencyRetryAsync(
        Entities.IdempotencyRecord existing,
        string idempotencyKey,
        IServiceIdentity serviceIdentity,
        CancellationToken cancellationToken)
    {
        switch (existing.Status)
        {
            case "InProgress":
                throw new InvalidRequestException("A request with this idempotency key is already in progress.");

            case "Completed":
                if (existing.ObjectId is null)
                {
                    throw new InvalidOperationException("Completed idempotency record has no ObjectId.");
                }

                var completedMetadata = await _metadataRepository.GetByIdAsync(existing.ObjectId, cancellationToken);
                if (completedMetadata is null)
                {
                    throw new ObjectNotFoundException(existing.ObjectId);
                }
                AuthorizeAccess(serviceIdentity, completedMetadata);
                return completedMetadata;

            case "Failed":
                await _idempotencyRepository!.RemoveAsync(idempotencyKey, cancellationToken);
                return null;

            default:
                throw new InvalidRequestException($"Unknown idempotency status: {existing.Status}");
        }
    }

    public async Task<Stream> DownloadAsync(string id, IServiceIdentity serviceIdentity, CancellationToken cancellationToken = default, string? correlationId = null)
    {
        var metadata = await GetMetadataAsync(id, cancellationToken);
        AuthorizeAccess(serviceIdentity, metadata);

        var stream = await _objectStorage.DownloadAsync(metadata.ObjectKey, cancellationToken);

        await AuditLogAsync(serviceIdentity.Name, "Download", id, "Success", correlationId);

        return stream;
    }

    public async Task<StoredObject> GetMetadataAsync(string id, CancellationToken cancellationToken = default)
    {
        var metadata = await _metadataRepository.GetByIdAsync(id, cancellationToken);
        if (metadata is null)
        {
            throw new ObjectNotFoundException(id);
        }

        return metadata;
    }

    public async Task<StoredObject> GetMetadataAsync(string id, IServiceIdentity serviceIdentity, CancellationToken cancellationToken = default, string? correlationId = null)
    {
        var metadata = await GetMetadataAsync(id, cancellationToken);
        AuthorizeAccess(serviceIdentity, metadata);

        await AuditLogAsync(serviceIdentity.Name, "Metadata", id, "Success", correlationId);

        return metadata;
    }

    public async Task DeleteAsync(string id, IServiceIdentity serviceIdentity, CancellationToken cancellationToken = default, string? correlationId = null)
    {
        var metadata = await GetMetadataAsync(id, cancellationToken);
        AuthorizeAccess(serviceIdentity, metadata);
        await _objectStorage.DeleteAsync(metadata.ObjectKey, cancellationToken);
        await _metadataRepository.DeleteAsync(id, cancellationToken);

        await AuditLogAsync(serviceIdentity.Name, "Delete", id, "Success", correlationId);
    }

    public async Task<string> GeneratePresignedUrlAsync(
        string id,
        IServiceIdentity serviceIdentity,
        TimeSpan expiration,
        CancellationToken cancellationToken = default,
        string? correlationId = null)
    {
        if (expiration <= TimeSpan.Zero)
        {
            throw new InvalidRequestException("Presigned URL expiration must be greater than zero.");
        }

        if (expiration.TotalSeconds > _storageOptions.MaxPresignedUrlExpirationSeconds)
        {
            throw new InvalidRequestException(
                $"Presigned URL expiration exceeds maximum allowed ({_storageOptions.MaxPresignedUrlExpirationSeconds} seconds).");
        }

        var metadata = await GetMetadataAsync(id, cancellationToken);
        AuthorizeAccess(serviceIdentity, metadata);
        var url = await _objectStorage.CreatePresignedUrlAsync(metadata.ObjectKey, expiration, cancellationToken);

        await AuditLogAsync(serviceIdentity.Name, "PresignedUrl", id, "Success", correlationId,
            metadata: $"expiresIn={expiration.TotalSeconds}s");

        return url;
    }

    private static void AuthorizeAccess(IServiceIdentity serviceIdentity, StoredObject metadata)
    {
        if (!serviceIdentity.IsAuthorizedForObject(metadata.ObjectKey))
        {
            throw new ForbiddenAccessException(serviceIdentity.Name, metadata.ObjectKey);
        }
    }

    private async Task AuditLogAsync(
        string serviceName,
        string operation,
        string? objectId,
        string result,
        string? correlationId,
        string? metadata = null)
    {
        if (_auditLogger is null) return;

        await _auditLogger.LogAsync(
            serviceName,
            operation,
            objectId,
            result,
            correlationId ?? string.Empty,
            metadata: metadata);
    }

    private void ValidateUploadRequest(
        string service,
        string category,
        string fileName,
        string contentType,
        long contentLength)
    {
        if (contentLength <= 0)
        {
            throw new InvalidRequestException("File content must not be empty.");
        }

        if (string.IsNullOrWhiteSpace(category))
        {
            throw new InvalidRequestException("Category is required.");
        }

        if (!_storageOptions.AllowedCategories.Contains(category, StringComparer.OrdinalIgnoreCase))
        {
            throw new CategoryNotAllowedException(category);
        }

        if (contentLength > _storageOptions.MaxFileSizeBytes)
        {
            throw new FileTooLargeException(contentLength, _storageOptions.MaxFileSizeBytes);
        }

        var effectiveContentType = string.IsNullOrWhiteSpace(contentType)
            ? "application/octet-stream"
            : contentType;

        if (!_storageOptions.AllowedContentTypes.Contains("*", StringComparer.OrdinalIgnoreCase) &&
            !_storageOptions.AllowedContentTypes.Contains(effectiveContentType, StringComparer.OrdinalIgnoreCase))
        {
            throw new UnsupportedContentTypeException(effectiveContentType);
        }
    }
}
