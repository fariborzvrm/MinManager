namespace ObjectStorage.Api.Configuration;

public sealed class OpenTelemetryOptions
{
    public const string SectionName = "OpenTelemetry";

    public bool Enabled { get; set; } = true;
    public string ServiceName { get; set; } = "ObjectStorage.Api";
    public string? OtlpEndpoint { get; set; }
    public bool ConsoleExporter { get; set; }
}
