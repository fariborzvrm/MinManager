using Microsoft.AspNetCore.Mvc;
using ObjectStorage.Api.Contracts;
using ObjectStorage.Application.Services;
using ObjectStorage.Domain.Interfaces;
using ObjectStorage.Infrastructure.Middleware;

namespace ObjectStorage.Api.Controllers;

[ApiController]
[Route("api/v1/objects")]
public sealed class ObjectsController : ControllerBase
{
    private readonly ObjectService _objectService;

    public ObjectsController(ObjectService objectService)
    {
        _objectService = objectService;
    }

    [HttpPost]
    [ProducesResponseType(typeof(ObjectMetadataResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType(StatusCodes.Status415UnsupportedMediaType)]
    public async Task<ActionResult<ObjectMetadataResponse>> Upload(
        IFormFile file,
        [FromForm] string category,
        CancellationToken cancellationToken)
    {
        var serviceIdentity = GetServiceIdentity();
        var correlationId = GetCorrelationId();

        var contentType = string.IsNullOrWhiteSpace(file.ContentType)
            ? "application/octet-stream"
            : file.ContentType;

        HttpContext.Request.Headers.TryGetValue("Idempotency-Key", out var idempotencyKey);

        var storedObject = await _objectService.UploadAsync(
            file.OpenReadStream(),
            serviceIdentity,
            category,
            file.FileName,
            contentType,
            file.Length,
            cancellationToken,
            idempotencyKey.FirstOrDefault(),
            correlationId);

        var response = MapToResponse(storedObject);
        return CreatedAtAction(
            nameof(GetMetadata),
            new { id = storedObject.Id },
            response);
    }

    [HttpGet("{id}/download")]
    [ProducesResponseType(typeof(FileStreamResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Download(string id, CancellationToken cancellationToken)
    {
        var serviceIdentity = GetServiceIdentity();
        var correlationId = GetCorrelationId();
        var stream = await _objectService.DownloadAsync(id, serviceIdentity, cancellationToken, correlationId);
        var metadata = await _objectService.GetMetadataAsync(id, serviceIdentity, cancellationToken);
        return File(stream, metadata.ContentType, metadata.FileName);
    }

    [HttpGet("{id}/metadata")]
    [ProducesResponseType(typeof(ObjectMetadataResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ObjectMetadataResponse>> GetMetadata(string id, CancellationToken cancellationToken)
    {
        var serviceIdentity = GetServiceIdentity();
        var correlationId = GetCorrelationId();
        var storedObject = await _objectService.GetMetadataAsync(id, serviceIdentity, cancellationToken, correlationId);
        return Ok(MapToResponse(storedObject));
    }

    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(string id, CancellationToken cancellationToken)
    {
        var serviceIdentity = GetServiceIdentity();
        var correlationId = GetCorrelationId();
        await _objectService.DeleteAsync(id, serviceIdentity, cancellationToken, correlationId);
        return NoContent();
    }

    [HttpPost("{id}/presigned-url")]
    [ProducesResponseType(typeof(PresignedUrlResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PresignedUrlResponse>> GeneratePresignedUrl(
        string id,
        [FromBody] PresignedUrlRequest request,
        CancellationToken cancellationToken)
    {
        var serviceIdentity = GetServiceIdentity();
        var correlationId = GetCorrelationId();
        var expiration = TimeSpan.FromSeconds(request.ExpirationSeconds);
        var url = await _objectService.GeneratePresignedUrlAsync(id, serviceIdentity, expiration, cancellationToken, correlationId);
        var expiresAt = DateTimeOffset.UtcNow.Add(expiration);
        return Ok(new PresignedUrlResponse(url, expiresAt));
    }

    private IServiceIdentity GetServiceIdentity()
    {
        if (HttpContext.Items[ApiKeyMiddleware.ServiceIdentityKey] is not IServiceIdentity identity)
        {
            throw new Domain.Exceptions.UnauthorizedAccessException();
        }
        return identity;
    }

    private string? GetCorrelationId()
    {
        return HttpContext.Items[ApiKeyMiddleware.CorrelationIdKey] as string;
    }

    private static ObjectMetadataResponse MapToResponse(Domain.Entities.StoredObject storedObject) =>
        new(
            storedObject.Id,
            storedObject.FileName,
            storedObject.ContentType,
            storedObject.Size,
            storedObject.CreatedAt);
}
