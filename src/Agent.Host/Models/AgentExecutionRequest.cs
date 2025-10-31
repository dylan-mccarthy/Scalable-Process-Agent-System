namespace Agent.Host.Models;

/// <summary>
/// Request to execute an agent in the sandbox process.
/// </summary>
public sealed class AgentExecutionRequest
{
    /// <summary>
    /// Unique identifier for the agent.
    /// </summary>
    public required string AgentId { get; set; }

    /// <summary>
    /// Version of the agent.
    /// </summary>
    public required string Version { get; set; }

    /// <summary>
    /// Name of the agent.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Instructions for the agent.
    /// </summary>
    public required string Instructions { get; set; }

    /// <summary>
    /// Input message for the agent to process.
    /// </summary>
    public required string Input { get; set; }

    /// <summary>
    /// Maximum tokens allowed for this execution.
    /// </summary>
    public int? MaxTokens { get; set; }

    /// <summary>
    /// Maximum duration in seconds for this execution.
    /// </summary>
    public int? MaxDurationSeconds { get; set; }

    /// <summary>
    /// Model profile configuration (optional).
    /// </summary>
    public Dictionary<string, object>? ModelProfile { get; set; }
}
