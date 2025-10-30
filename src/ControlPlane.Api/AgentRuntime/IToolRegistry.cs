namespace ControlPlane.Api.AgentRuntime;

/// <summary>
/// Interface for managing tools available to agents
/// </summary>
public interface IToolRegistry
{
    /// <summary>
    /// Registers a tool with the registry
    /// </summary>
    Task RegisterToolAsync(ToolDefinition tool, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a specific tool by name
    /// </summary>
    Task<ToolDefinition?> GetToolAsync(string toolName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all tools available for a specific agent
    /// </summary>
    Task<IEnumerable<ToolDefinition>> GetToolsForAgentAsync(string agentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all registered tools
    /// </summary>
    Task<IEnumerable<ToolDefinition>> GetAllToolsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a tool from the registry
    /// </summary>
    Task<bool> UnregisterToolAsync(string toolName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Associates a tool with an agent
    /// </summary>
    Task AssociateToolWithAgentAsync(string agentId, string toolName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes tool association from an agent
    /// </summary>
    Task DisassociateToolFromAgentAsync(string agentId, string toolName, CancellationToken cancellationToken = default);
}

/// <summary>
/// Definition of a tool that can be used by agents
/// </summary>
public class ToolDefinition
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty; // e.g., "function", "api", "connector"
    public Dictionary<string, object>? Parameters { get; set; }
    public Dictionary<string, object>? Configuration { get; set; }
}
