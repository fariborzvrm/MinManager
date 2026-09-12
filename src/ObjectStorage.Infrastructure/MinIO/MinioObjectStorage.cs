using System.Diagnostics;
using System.Threading;
using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;
using ObjectStorage.Domain.Exceptions;
using ObjectStorage.Domain.Interfaces;
using ObjectStorage.Domain.ValueObjects;
using ObjectStorage.Infrastructure.Configuration;
using ObjectStorage.Infrastructure.Telemetry;
using ObjectNotFoundException = Minio.Exceptions.ObjectNotFoundException;

namespace ObjectStorage.Infrastructure.MinIO;

public sealed class MinioObjectStorage : IObjectStorage
{
    private readonly IMinioClient _client;
    private readonly MinioOptions _options;
    private readonly SemaphoreSlim _bucketLock = new(1, 1);
    private bool _bucketEnsured;

    public MinioObjectStorage(IOptions<MinioOptions> options)
    {
        _options = options.Value;
        _client = new MinioClient()
            .WithEndpoint(_options.Endpoint)
            .WithCredentials(_options.AccessKey, _options.SecretKey)
            .WithSSL(_options.UseSSL)
            .Build();
    }

    public async Task<ObjectMetadata> UploadAsync(
        Stream content,
        string objectKey,
        string contentType,
        long contentLength,
        CancellationToken cancellationToken)
    {
        using var activity = ObjectStorageTracing.ActivitySource.StartActivity("MinIO.Upload", ActivityKind.Internal);
        activity?.SetTag("object.storage.operation", "upload");
        activity?.SetTag("object.key", objectKey);
        activity?.SetTag("object.size", contentLength);
        activity?.SetTag("object.content_type", contentType);

        await EnsureBucketExistsAsync(cancellationToken);
        try
        {
            var response = await _client.PutObjectAsync(
                new PutObjectArgs()
                    .WithBucket(_options.Bucket)
                    .WithObject(objectKey)
                    .WithStreamData(content)
                    .WithContentType(contentType)
                    .WithObjectSize(contentLength),
                cancellationToken);

            ObjectStorageMetrics.UploadSizeBytes.Record(contentLength,
                new KeyValuePair<string, object?>("content_type", contentType));

            ObjectStorageMetrics.OperationsTotal.Add(1,
                new KeyValuePair<string, object?>("operation", "upload"),
                new KeyValuePair<string, object?>("result", "success"));

            return new ObjectMetadata(response.ObjectName, response.Size, contentType);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.SetTag("exception.type", ex.GetType().FullName);

            ObjectStorageMetrics.OperationsTotal.Add(1,
                new KeyValuePair<string, object?>("operation", "upload"),
                new KeyValuePair<string, object?>("result", "failure"));

            throw MinioExceptionMapper.Map(ex, objectKey);
        }
    }

    public async Task<Stream> DownloadAsync(string objectKey, CancellationToken cancellationToken)
    {
        using var activity = ObjectStorageTracing.ActivitySource.StartActivity("MinIO.Download", ActivityKind.Internal);
        activity?.SetTag("object.storage.operation", "download");
        activity?.SetTag("object.key", objectKey);

        await EnsureBucketExistsAsync(cancellationToken);
        try
        {
            var buffer = new MemoryStream();

            var args = new GetObjectArgs()
                .WithBucket(_options.Bucket)
                .WithObject(objectKey)
                .WithCallbackStream(stream => stream.CopyTo(buffer));

            await _client.GetObjectAsync(args, cancellationToken);

            buffer.Position = 0;

            ObjectStorageMetrics.DownloadSizeBytes.Record(buffer.Length);

            ObjectStorageMetrics.OperationsTotal.Add(1,
                new KeyValuePair<string, object?>("operation", "download"),
                new KeyValuePair<string, object?>("result", "success"));

            activity?.SetTag("object.size", buffer.Length);

            return buffer;
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.SetTag("exception.type", ex.GetType().FullName);

            ObjectStorageMetrics.OperationsTotal.Add(1,
                new KeyValuePair<string, object?>("operation", "download"),
                new KeyValuePair<string, object?>("result", "failure"));

            throw MinioExceptionMapper.Map(ex, objectKey);
        }
    }

    public async Task DeleteAsync(string objectKey, CancellationToken cancellationToken)
    {
        using var activity = ObjectStorageTracing.ActivitySource.StartActivity("MinIO.Delete", ActivityKind.Internal);
        activity?.SetTag("object.storage.operation", "delete");
        activity?.SetTag("object.key", objectKey);

        await EnsureBucketExistsAsync(cancellationToken);
        try
        {
            await _client.RemoveObjectAsync(
                new RemoveObjectArgs()
                    .WithBucket(_options.Bucket)
                    .WithObject(objectKey),
                cancellationToken);

            ObjectStorageMetrics.OperationsTotal.Add(1,
                new KeyValuePair<string, object?>("operation", "delete"),
                new KeyValuePair<string, object?>("result", "success"));
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.SetTag("exception.type", ex.GetType().FullName);

            ObjectStorageMetrics.OperationsTotal.Add(1,
                new KeyValuePair<string, object?>("operation", "delete"),
                new KeyValuePair<string, object?>("result", "failure"));

            throw MinioExceptionMapper.Map(ex, objectKey);
        }
    }

    public async Task<bool> ExistsAsync(string objectKey, CancellationToken cancellationToken)
    {
        using var activity = ObjectStorageTracing.ActivitySource.StartActivity("MinIO.Exists", ActivityKind.Internal);
        activity?.SetTag("object.storage.operation", "exists");
        activity?.SetTag("object.key", objectKey);

        await EnsureBucketExistsAsync(cancellationToken);
        try
        {
            await _client.StatObjectAsync(
                new StatObjectArgs()
                    .WithBucket(_options.Bucket)
                    .WithObject(objectKey),
                cancellationToken);

            ObjectStorageMetrics.OperationsTotal.Add(1,
                new KeyValuePair<string, object?>("operation", "exists"),
                new KeyValuePair<string, object?>("result", "success"));

            return true;
        }
        catch (ObjectNotFoundException)
        {
            ObjectStorageMetrics.OperationsTotal.Add(1,
                new KeyValuePair<string, object?>("operation", "exists"),
                new KeyValuePair<string, object?>("result", "not_found"));

            return false;
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.SetTag("exception.type", ex.GetType().FullName);

            ObjectStorageMetrics.OperationsTotal.Add(1,
                new KeyValuePair<string, object?>("operation", "exists"),
                new KeyValuePair<string, object?>("result", "failure"));

            throw MinioExceptionMapper.Map(ex, objectKey);
        }
    }

    public async Task<string> CreatePresignedUrlAsync(string objectKey, TimeSpan expiration, CancellationToken cancellationToken)
    {
        using var activity = ObjectStorageTracing.ActivitySource.StartActivity("MinIO.PresignedUrl", ActivityKind.Internal);
        activity?.SetTag("object.storage.operation", "presigned_url");
        activity?.SetTag("object.key", objectKey);
        activity?.SetTag("object.expiration_seconds", expiration.TotalSeconds);

        await EnsureBucketExistsAsync(cancellationToken);
        try
        {
            var expirySeconds = checked((int)expiration.TotalSeconds);
            var url = await _client.PresignedGetObjectAsync(
                new PresignedGetObjectArgs()
                    .WithBucket(_options.Bucket)
                    .WithObject(objectKey)
                    .WithExpiry(expirySeconds));

            ObjectStorageMetrics.OperationsTotal.Add(1,
                new KeyValuePair<string, object?>("operation", "presigned_url"),
                new KeyValuePair<string, object?>("result", "success"));

            return url;
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.SetTag("exception.type", ex.GetType().FullName);

            ObjectStorageMetrics.OperationsTotal.Add(1,
                new KeyValuePair<string, object?>("operation", "presigned_url"),
                new KeyValuePair<string, object?>("result", "failure"));

            throw MinioExceptionMapper.Map(ex, objectKey);
        }
    }

    /// <summary>
    /// Best-effort check that MinIO is reachable and the configured bucket exists.
    /// </summary>
    public async Task<bool> PingAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await _client.BucketExistsAsync(
                new BucketExistsArgs()
                    .WithBucket(_options.Bucket),
                cancellationToken);
        }
        catch
        {
            return false;
        }
    }

    private async Task EnsureBucketExistsAsync(CancellationToken cancellationToken)
    {
        if (_bucketEnsured)
        {
            return;
        }

        await _bucketLock.WaitAsync(cancellationToken);
        try
        {
            if (_bucketEnsured)
            {
                return;
            }

            var exists = await _client.BucketExistsAsync(
                new BucketExistsArgs()
                    .WithBucket(_options.Bucket),
                cancellationToken);

            if (!exists)
            {
                await _client.MakeBucketAsync(
                    new MakeBucketArgs()
                        .WithBucket(_options.Bucket),
                    cancellationToken);
            }

            _bucketEnsured = true;
        }
        finally
        {
            _bucketLock.Release();
        }
    }
}
