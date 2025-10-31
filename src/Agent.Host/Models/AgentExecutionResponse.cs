namespace Agent.Host.Models;

/// <summary>
/// Response from agent execution in the sandbox process.
/// </summary>
public sealed class AgentExecutionResponse
{
    /// <summary>
    /// Indicates if the execution was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// The output from the agent execution.
    /// </summary>
    public string? Output { get; set; }

    /// <summary>
    /// Error message if execution failed.
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// Number of input tokens used.
    /// </summary>
    public int TokensIn { get; set; }

    /// <summary>
    /// Number of output tokens used.
    /// </summary>
    public int TokensOut { get; set; }

    /// <summary>
    /// Total duration of the execution in milliseconds.
    /// </summary>
    public long DurationMs { get; set; }

    /// <summary>
    /// Estimated cost in USD.
    /// </summary>
    public double UsdCost { get; set; }
}
