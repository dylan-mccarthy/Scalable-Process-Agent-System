namespace Node.Runtime.Configuration;

/// <summary>
/// Configuration options for the Node Runtime.
/// </summary>
public sealed class NodeRuntimeOptions
{
    /// <summary>
    /// Unique identifier for this node.
    /// </summary>
    public string NodeId { get; set; } = "node-1";

    /// <summary>
    /// URL of the Control Plane API.
    /// </summary>
    public string ControlPlaneUrl { get; set; } = "http://localhost:5109";

    /// <summary>
    /// Maximum number of concurrent leases this node can handle.
    /// </summary>
    public int MaxConcurrentLeases { get; set; } = 5;

    /// <summary>
    /// Interval in seconds between heartbeat updates.
    /// </summary>
    public int HeartbeatIntervalSeconds { get; set; } = 30;

    /// <summary>
    /// Node capacity information.
    /// </summary>
    public NodeCapacity Capacity { get; set; } = new();

    /// <summary>
    /// Node metadata for placement constraints.
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; } = new();
}

/// <summary>
/// Node capacity configuration.
/// </summary>
public sealed class NodeCapacity
{
    /// <summary>
    /// Total number of execution slots available on this node.
    /// </summary>
    public int Slots { get; set; } = 8;

    /// <summary>
    /// CPU allocation (e.g., "4" cores).
    /// </summary>
    public string Cpu { get; set; } = "4";

    /// <summary>
    /// Memory allocation (e.g., "8Gi").
    /// </summary>
    public string Memory { get; set; } = "8Gi";
}
