using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace ObjectStorage.Infrastructure.Telemetry;

public static class ObjectStorageMetrics
{
    public const string MeterName = "ObjectStorage";

    public static readonly Meter Meter = new(MeterName);

    public static readonly Histogram<long> UploadSizeBytes =
        Meter.CreateHistogram<long>(
            "objectstorage.upload.size_bytes",
            unit: "By",
            description: "Size of uploaded objects in bytes");

    public static readonly Histogram<long> DownloadSizeBytes =
        Meter.CreateHistogram<long>(
            "objectstorage.download.size_bytes",
            unit: "By",
            description: "Size of downloaded objects in bytes");

    public static readonly Counter<long> OperationsTotal =
        Meter.CreateCounter<long>(
            "objectstorage.operations.total",
            description: "Total number of storage operations");

    public static readonly Counter<long> AuthFailuresTotal =
        Meter.CreateCounter<long>(
            "objectstorage.auth_failures.total",
            description: "Total number of authorization failures");
}

public static class ObjectStorageTracing
{
    public const string ActivitySourceName = "ObjectStorage.MinIO";
    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);
}
