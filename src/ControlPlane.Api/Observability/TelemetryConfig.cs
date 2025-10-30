using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace ControlPlane.Api.Observability;

/// <summary>
/// Centralized configuration for OpenTelemetry instrumentation
/// </summary>
public static class TelemetryConfig
{
    public const string ServiceName = "ControlPlane.Api";
    public const string ServiceVersion = "1.0.0";

    /// <summary>
    /// Activity source for distributed tracing
    /// </summary>
    public static readonly ActivitySource ActivitySource = new(ServiceName, ServiceVersion);

    /// <summary>
    /// Meter for custom metrics
    /// </summary>
    public static readonly Meter Meter = new(ServiceName, ServiceVersion);

    // Counters
    public static readonly Counter<long> RunsStartedCounter = Meter.CreateCounter<long>(
        "runs_started_total",
        description: "Total number of runs started");

    public static readonly Counter<long> RunsCompletedCounter = Meter.CreateCounter<long>(
        "runs_completed_total",
        description: "Total number of runs completed successfully");

    public static readonly Counter<long> RunsFailedCounter = Meter.CreateCounter<long>(
        "runs_failed_total",
        description: "Total number of runs failed");

    public static readonly Counter<long> RunsCancelledCounter = Meter.CreateCounter<long>(
        "runs_cancelled_total",
        description: "Total number of runs cancelled");

    public static readonly Counter<long> NodesRegisteredCounter = Meter.CreateCounter<long>(
        "nodes_registered_total",
        description: "Total number of nodes registered");

    public static readonly Counter<long> NodesDisconnectedCounter = Meter.CreateCounter<long>(
        "nodes_disconnected_total",
        description: "Total number of nodes disconnected");

    public static readonly Counter<long> LeasesGrantedCounter = Meter.CreateCounter<long>(
        "leases_granted_total",
        description: "Total number of leases granted to nodes");

    public static readonly Counter<long> LeasesReleasedCounter = Meter.CreateCounter<long>(
        "leases_released_total",
        description: "Total number of leases released");

    public static readonly Counter<long> SchedulingAttemptsCounter = Meter.CreateCounter<long>(
        "scheduling_attempts_total",
        description: "Total number of scheduling attempts");

    public static readonly Counter<long> SchedulingFailuresCounter = Meter.CreateCounter<long>(
        "scheduling_failures_total",
        description: "Total number of scheduling failures");

    // Histograms
    public static readonly Histogram<double> RunDurationHistogram = Meter.CreateHistogram<double>(
        "run_duration_ms",
        unit: "ms",
        description: "Duration of run execution in milliseconds");

    public static readonly Histogram<double> SchedulingDurationHistogram = Meter.CreateHistogram<double>(
        "scheduling_duration_ms",
        unit: "ms",
        description: "Duration of scheduling operations in milliseconds");

    public static readonly Histogram<long> RunTokensHistogram = Meter.CreateHistogram<long>(
        "run_tokens",
        description: "Number of tokens used per run");

    public static readonly Histogram<double> RunCostHistogram = Meter.CreateHistogram<double>(
        "run_cost_usd",
        unit: "USD",
        description: "Cost of run execution in USD");

    // Observable Gauges
    public static ObservableGauge<int>? ActiveRunsGauge { get; set; }
    public static ObservableGauge<int>? ActiveNodesGauge { get; set; }
    public static ObservableGauge<int>? TotalSlotsGauge { get; set; }
    public static ObservableGauge<int>? UsedSlotsGauge { get; set; }
    public static ObservableGauge<int>? AvailableSlotsGauge { get; set; }
}
