namespace Node.Runtime.Configuration;

/// <summary>
/// Configuration options for OpenTelemetry.
/// </summary>
public sealed class OpenTelemetryOptions
{
    /// <summary>
    /// Service name for telemetry.
    /// </summary>
    public string ServiceName { get; set; } = "Node.Runtime";

    /// <summary>
    /// Service version.
    /// </summary>
    public string ServiceVersion { get; set; } = "1.0.0";

    /// <summary>
    /// OTLP exporter configuration.
    /// </summary>
    public OtlpExporterOptions OtlpExporter { get; set; } = new();

    /// <summary>
    /// Console exporter configuration.
    /// </summary>
    public ConsoleExporterOptions ConsoleExporter { get; set; } = new();

    /// <summary>
    /// Traces configuration.
    /// </summary>
    public TracesOptions Traces { get; set; } = new();

    /// <summary>
    /// Metrics configuration.
    /// </summary>
    public MetricsOptions Metrics { get; set; } = new();
}

/// <summary>
/// OTLP exporter configuration.
/// </summary>
public sealed class OtlpExporterOptions
{
    /// <summary>
    /// OTLP endpoint URL.
    /// </summary>
    public string Endpoint { get; set; } = "http://localhost:4317";

    /// <summary>
    /// Protocol (grpc or http/protobuf).
    /// </summary>
    public string Protocol { get; set; } = "grpc";
}

/// <summary>
/// Console exporter configuration.
/// </summary>
public sealed class ConsoleExporterOptions
{
    /// <summary>
    /// Whether the console exporter is enabled.
    /// </summary>
    public bool Enabled { get; set; } = false;
}

/// <summary>
/// Traces configuration.
/// </summary>
public sealed class TracesOptions
{
    /// <summary>
    /// Whether tracing is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Sampling ratio (0.0 to 1.0).
    /// </summary>
    public double SamplingRatio { get; set; } = 1.0;
}

/// <summary>
/// Metrics configuration.
/// </summary>
public sealed class MetricsOptions
{
    /// <summary>
    /// Whether metrics are enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Export interval in milliseconds.
    /// </summary>
    public int ExportIntervalMilliseconds { get; set; } = 60000;
}
