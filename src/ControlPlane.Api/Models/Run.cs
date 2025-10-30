namespace ControlPlane.Api.Models;

public class Run
{
    public string RunId { get; set; } = string.Empty;
    public string AgentId { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string? DeploymentId { get; set; }
    public string? NodeId { get; set; }
    public Dictionary<string, object>? InputRef { get; set; }
    public string Status { get; set; } = "pending";
    public Dictionary<string, object>? Timings { get; set; }
    public Dictionary<string, object>? Costs { get; set; }
    public string? TraceId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class CompleteRunRequest
{
    public Dictionary<string, object>? Result { get; set; }
    public Dictionary<string, object>? Timings { get; set; }
    public Dictionary<string, object>? Costs { get; set; }
}

public class FailRunRequest
{
    public string ErrorMessage { get; set; } = string.Empty;
    public string? ErrorDetails { get; set; }
    public Dictionary<string, object>? Timings { get; set; }
}

public class CancelRunRequest
{
    public string Reason { get; set; } = string.Empty;
}
