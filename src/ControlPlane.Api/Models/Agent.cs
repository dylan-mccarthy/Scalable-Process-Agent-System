namespace ControlPlane.Api.Models;

public class Agent
{
    public string AgentId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Instructions { get; set; } = string.Empty;
    public Dictionary<string, object>? ModelProfile { get; set; }
}

public class AgentVersion
{
    public string AgentId { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public Dictionary<string, object>? Spec { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class CreateAgentRequest
{
    public string Name { get; set; } = string.Empty;
    public string Instructions { get; set; } = string.Empty;
    public Dictionary<string, object>? ModelProfile { get; set; }
}

public class UpdateAgentRequest
{
    public string? Name { get; set; }
    public string? Instructions { get; set; }
    public Dictionary<string, object>? ModelProfile { get; set; }
}
