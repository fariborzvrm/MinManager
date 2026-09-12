using System.Diagnostics;
using System.Diagnostics.Metrics;
using ObjectStorage.Infrastructure.Telemetry;

namespace ObjectStorage.UnitTests;

public sealed class ObjectStorageTelemetryTests
{
    [Fact]
    public void Meter_HasCorrectName()
    {
        Assert.Equal("ObjectStorage", ObjectStorageMetrics.Meter.Name);
    }

    [Fact]
    public void ActivitySource_HasCorrectName()
    {
        Assert.Equal("ObjectStorage.MinIO", ObjectStorageTracing.ActivitySource.Name);
    }

    [Fact]
    public void UploadSizeBytes_IsNotNull()
    {
        Assert.NotNull(ObjectStorageMetrics.UploadSizeBytes);
    }

    [Fact]
    public void DownloadSizeBytes_IsNotNull()
    {
        Assert.NotNull(ObjectStorageMetrics.DownloadSizeBytes);
    }

    [Fact]
    public void OperationsTotal_IsNotNull()
    {
        Assert.NotNull(ObjectStorageMetrics.OperationsTotal);
    }

    [Fact]
    public void AuthFailuresTotal_IsNotNull()
    {
        Assert.NotNull(ObjectStorageMetrics.AuthFailuresTotal);
    }

    [Fact]
    public void ActivitySource_CanStartActivity()
    {
        // ActivitySource returns null when no listener is subscribed (normal in unit tests)
        using var activity = ObjectStorageTracing.ActivitySource.StartActivity("test_operation");
        // The important thing is that the call doesn't throw; activity may be null without a listener
        Assert.True(activity is null || activity.OperationName == "test_operation");
    }

    [Fact]
    public void UploadSizeBytes_CanRecord()
    {
        ObjectStorageMetrics.UploadSizeBytes.Record(1024,
            new KeyValuePair<string, object?>("content_type", "text/plain"));
    }

    [Fact]
    public void OperationsTotal_CanAdd()
    {
        ObjectStorageMetrics.OperationsTotal.Add(1,
            new KeyValuePair<string, object?>("operation", "upload"),
            new KeyValuePair<string, object?>("result", "success"));
    }

    [Fact]
    public void AuthFailuresTotal_CanAdd()
    {
        ObjectStorageMetrics.AuthFailuresTotal.Add(1,
            new KeyValuePair<string, object?>("exception_type", "ForbiddenAccessException"));
    }
}
