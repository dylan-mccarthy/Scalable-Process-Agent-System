namespace ControlPlane.Api.Models;

/// <summary>
/// Represents a deployment of an agent version with replicas and placement configuration.
/// </summary>
public class Deployment
{
    public string DepId { get; set; } = string.Empty;
    public string AgentId { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Env { get; set; } = string.Empty;
    public DeploymentTarget? Target { get; set; }
    public DeploymentStatus? Status { get; set; }
}

/// <summary>
/// Deployment target configuration including replicas and placement.
/// </summary>
public class DeploymentTarget
{
    public int Replicas { get; set; } = 1;
    public Dictionary<string, object>? Placement { get; set; }
}

/// <summary>
/// Deployment status information.
/// </summary>
public class DeploymentStatus
{
    public string State { get; set; } = "pending";
    public int ReadyReplicas { get; set; } = 0;
    public DateTime? LastUpdated { get; set; }
    public string? Message { get; set; }
}

/// <summary>
/// Request to create a new deployment.
/// </summary>
public class CreateDeploymentRequest
{
    public string AgentId { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Env { get; set; } = string.Empty;
    public DeploymentTarget? Target { get; set; }
}

/// <summary>
/// Request to update deployment status.
/// </summary>
public class UpdateDeploymentStatusRequest
{
    public DeploymentStatus? Status { get; set; }
}
