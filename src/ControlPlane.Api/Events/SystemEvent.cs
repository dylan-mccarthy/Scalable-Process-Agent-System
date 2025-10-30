using System.Text.Json.Serialization;

namespace ControlPlane.Api.Events;

/// <summary>
/// Base class for all system events published to NATS JetStream
/// </summary>
public abstract class SystemEvent
{
    /// <summary>
    /// Unique identifier for the event
    /// </summary>
    [JsonPropertyName("eventId")]
    public string EventId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Event type (e.g., "run.state.changed", "node.registered")
    /// </summary>
    [JsonPropertyName("eventType")]
    public abstract string EventType { get; }

    /// <summary>
    /// Timestamp when the event occurred
    /// </summary>
    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Optional correlation ID for tracing
    /// </summary>
    [JsonPropertyName("correlationId")]
    public string? CorrelationId { get; set; }
}

/// <summary>
/// Event published when a run's state changes
/// </summary>
public class RunStateChangedEvent : SystemEvent
{
    public override string EventType => "run.state.changed";

    [JsonPropertyName("runId")]
    public string RunId { get; set; } = string.Empty;

    [JsonPropertyName("agentId")]
    public string? AgentId { get; set; }

    [JsonPropertyName("nodeId")]
    public string? NodeId { get; set; }

    [JsonPropertyName("previousState")]
    public string? PreviousState { get; set; }

    [JsonPropertyName("newState")]
    public string NewState { get; set; } = string.Empty;
}

/// <summary>
/// Event published when a node registers with the control plane
/// </summary>
public class NodeRegisteredEvent : SystemEvent
{
    public override string EventType => "node.registered";

    [JsonPropertyName("nodeId")]
    public string NodeId { get; set; } = string.Empty;

    [JsonPropertyName("region")]
    public string? Region { get; set; }

    [JsonPropertyName("capacity")]
    public int? Capacity { get; set; }
}

/// <summary>
/// Event published when a node sends a heartbeat
/// </summary>
public class NodeHeartbeatEvent : SystemEvent
{
    public override string EventType => "node.heartbeat";

    [JsonPropertyName("nodeId")]
    public string NodeId { get; set; } = string.Empty;

    [JsonPropertyName("activeRuns")]
    public int ActiveRuns { get; set; }

    [JsonPropertyName("availableSlots")]
    public int AvailableSlots { get; set; }
}

/// <summary>
/// Event published when a node disconnects
/// </summary>
public class NodeDisconnectedEvent : SystemEvent
{
    public override string EventType => "node.disconnected";

    [JsonPropertyName("nodeId")]
    public string NodeId { get; set; } = string.Empty;

    [JsonPropertyName("reason")]
    public string? Reason { get; set; }
}

/// <summary>
/// Event published when an agent is deployed
/// </summary>
public class AgentDeployedEvent : SystemEvent
{
    public override string EventType => "agent.deployed";

    [JsonPropertyName("agentId")]
    public string AgentId { get; set; } = string.Empty;

    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    [JsonPropertyName("deploymentId")]
    public string DeploymentId { get; set; } = string.Empty;
}
