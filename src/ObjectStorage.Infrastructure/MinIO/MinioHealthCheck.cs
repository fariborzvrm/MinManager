using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ObjectStorage.Infrastructure.MinIO;

public sealed class MinioHealthCheck : IHealthCheck
{
    private readonly MinioObjectStorage _storage;

    public MinioHealthCheck(MinioObjectStorage storage)
    {
        _storage = storage;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var accessible = await _storage.PingAsync(cancellationToken);

        return accessible
            ? HealthCheckResult.Healthy("MinIO is reachable and the bucket exists.")
            : HealthCheckResult.Unhealthy("MinIO is unreachable or the configured bucket is missing.");
    }
}