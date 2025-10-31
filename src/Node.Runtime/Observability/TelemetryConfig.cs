using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Node.Runtime.Observability;

/// <summary>
/// Centralized configuration for OpenTelemetry instrumentation in Node Runtime
/// </summary>
public static class TelemetryConfig
{
    public const string ServiceName = "Node.Runtime";
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
    public static readonly Counter<long> LeasesReceivedCounter = Meter.CreateCounter<long>(
        "leases_received_total",
        description: "Total number of leases received from control plane");

    public static readonly Counter<long> LeasesAcknowledgedCounter = Meter.CreateCounter<long>(
        "leases_acknowledged_total",
        description: "Total number of leases acknowledged");

    public static readonly Counter<long> LeasesCompletedCounter = Meter.CreateCounter<long>(
        "leases_completed_total",
        description: "Total number of leases completed successfully");

    public static readonly Counter<long> LeasesFailedCounter = Meter.CreateCounter<long>(
        "leases_failed_total",
        description: "Total number of leases that failed");

    public static readonly Counter<long> AgentExecutionsCounter = Meter.CreateCounter<long>(
        "agent_executions_total",
        description: "Total number of agent executions");

    public static readonly Counter<long> AgentExecutionErrorsCounter = Meter.CreateCounter<long>(
        "agent_execution_errors_total",
        description: "Total number of agent execution errors");

    public static readonly Counter<long> LeaseStreamErrorsCounter = Meter.CreateCounter<long>(
        "lease_stream_errors_total",
        description: "Total number of lease stream errors");

    public static readonly Counter<long> LeaseStreamReconnectsCounter = Meter.CreateCounter<long>(
        "lease_stream_reconnects_total",
        description: "Total number of lease stream reconnection attempts");

    public static readonly Counter<long> MessagesDeadLetteredCounter = Meter.CreateCounter<long>(
        "messages_deadlettered_total",
        description: "Total number of messages moved to dead-letter queue");

    // Histograms
    public static readonly Histogram<double> LeaseProcessingDurationHistogram = Meter.CreateHistogram<double>(
        "lease_processing_duration_ms",
        unit: "ms",
        description: "Duration of lease processing in milliseconds");

    public static readonly Histogram<double> AgentExecutionDurationHistogram = Meter.CreateHistogram<double>(
        "agent_execution_duration_ms",
        unit: "ms",
        description: "Duration of agent execution in milliseconds");

    public static readonly Histogram<long> AgentTokensHistogram = Meter.CreateHistogram<long>(
        "agent_tokens_total",
        description: "Total tokens used per agent execution");

    public static readonly Histogram<double> AgentCostHistogram = Meter.CreateHistogram<double>(
        "agent_cost_usd",
        unit: "USD",
        description: "Cost of agent execution in USD");

    // Observable Gauges
    public static ObservableGauge<int>? ActiveLeasesGauge { get; set; }
    public static ObservableGauge<int>? AvailableSlotsGauge { get; set; }
}
