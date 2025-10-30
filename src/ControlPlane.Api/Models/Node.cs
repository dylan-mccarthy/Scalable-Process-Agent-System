namespace ControlPlane.Api.Models;

public class Node
{
    public string NodeId { get; set; } = string.Empty;
    public Dictionary<string, object>? Metadata { get; set; }
    public Dictionary<string, object>? Capacity { get; set; }
    public NodeStatus Status { get; set; } = new();
    public DateTime HeartbeatAt { get; set; } = DateTime.UtcNow;
}

public class NodeStatus
{
    public string State { get; set; } = "active";
    public int ActiveRuns { get; set; } = 0;
    public int AvailableSlots { get; set; } = 0;
}

public class RegisterNodeRequest
{
    public string NodeId { get; set; } = string.Empty;
    public Dictionary<string, object>? Metadata { get; set; }
    public Dictionary<string, object>? Capacity { get; set; }
}

public class HeartbeatRequest
{
    public NodeStatus Status { get; set; } = new();
}
