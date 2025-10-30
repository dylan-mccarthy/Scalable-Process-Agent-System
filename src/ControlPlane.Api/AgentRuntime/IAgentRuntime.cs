using Microsoft.Agents.AI;

namespace ControlPlane.Api.AgentRuntime;

/// <summary>
/// Interface for executing agents using Microsoft Agent Framework
/// </summary>
public interface IAgentRuntime
{
    /// <summary>
    /// Creates an agent instance from an agent definition
    /// </summary>
    Task<AIAgent> CreateAgentAsync(string agentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes an agent with the given input
    /// </summary>
    Task<AgentExecutionResult> ExecuteAsync(
        AIAgent agent,
        string input,
        Dictionary<string, object>? context = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates agent configuration
    /// </summary>
    Task<bool> ValidateAgentAsync(string agentId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of an agent execution
/// </summary>
public class AgentExecutionResult
{
    public bool Success { get; set; }
    public string? Output { get; set; }
    public string? Error { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }
    public int TokensUsed { get; set; }
    public TimeSpan Duration { get; set; }
}
