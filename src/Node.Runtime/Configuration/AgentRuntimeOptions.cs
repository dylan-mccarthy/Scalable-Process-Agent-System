namespace Node.Runtime.Configuration;

/// <summary>
/// Configuration options for agent runtime execution.
/// </summary>
public class AgentRuntimeOptions
{
    /// <summary>
    /// Default maximum number of tokens allowed per agent execution.
    /// </summary>
    public const int DefaultMaxTokens = 4000;

    /// <summary>
    /// Default maximum duration in seconds for agent execution.
    /// </summary>
    public const int DefaultMaxDurationSeconds = 60;

    /// <summary>
    /// Default model to use if not specified in agent definition.
    /// </summary>
    public const string DefaultModelValue = "gpt-4";

    /// <summary>
    /// Default temperature for model if not specified.
    /// </summary>
    public const double DefaultTemperatureValue = 0.7;

    /// <summary>
    /// Maximum tokens allowed per agent execution.
    /// </summary>
    public int MaxTokens { get; set; } = DefaultMaxTokens;

    /// <summary>
    /// Maximum duration in seconds for agent execution.
    /// </summary>
    public int MaxDurationSeconds { get; set; } = DefaultMaxDurationSeconds;

    /// <summary>
    /// Default model to use for agent execution.
    /// </summary>
    public string DefaultModel { get; set; } = DefaultModelValue;

    /// <summary>
    /// Default temperature for model inference.
    /// </summary>
    public double DefaultTemperature { get; set; } = DefaultTemperatureValue;
}
