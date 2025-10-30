namespace ControlPlane.Api.Models;

/// <summary>
/// Represents an agent definition with configuration for execution, tools, and connectors.
/// </summary>
public class Agent
{
    public string AgentId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Instructions { get; set; } = string.Empty;
    public Dictionary<string, object>? ModelProfile { get; set; }
    public AgentBudget? Budget { get; set; }
    public List<string>? Tools { get; set; }
    public ConnectorConfiguration? Input { get; set; }
    public ConnectorConfiguration? Output { get; set; }
    public Dictionary<string, string>? Metadata { get; set; }
}

/// <summary>
/// Budget constraints for agent execution.
/// </summary>
public class AgentBudget
{
    public int? MaxTokens { get; set; }
    public int? MaxDurationSeconds { get; set; }
}

/// <summary>
/// Connector configuration for input or output.
/// </summary>
public class ConnectorConfiguration
{
    public string Type { get; set; } = string.Empty;
    public Dictionary<string, object>? Config { get; set; }
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
    public string? Description { get; set; }
    public string Instructions { get; set; } = string.Empty;
    public Dictionary<string, object>? ModelProfile { get; set; }
    public AgentBudget? Budget { get; set; }
    public List<string>? Tools { get; set; }
    public ConnectorConfiguration? Input { get; set; }
    public ConnectorConfiguration? Output { get; set; }
    public Dictionary<string, string>? Metadata { get; set; }
}

public class UpdateAgentRequest
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? Instructions { get; set; }
    public Dictionary<string, object>? ModelProfile { get; set; }
    public AgentBudget? Budget { get; set; }
    public List<string>? Tools { get; set; }
    public ConnectorConfiguration? Input { get; set; }
    public ConnectorConfiguration? Output { get; set; }
    public Dictionary<string, string>? Metadata { get; set; }
}
