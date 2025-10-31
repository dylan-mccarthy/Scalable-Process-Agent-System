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
    public const string DefaultModelValue = "gpt-5-mini";

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

    /// <summary>
    /// Azure AI Foundry configuration.
    /// </summary>
    public AzureAIFoundryOptions? AzureAIFoundry { get; set; }
}

/// <summary>
/// Configuration options for Azure AI Foundry integration.
/// </summary>
public class AzureAIFoundryOptions
{
    /// <summary>
    /// Azure AI Foundry endpoint URL.
    /// Example: https://my-resource.services.ai.azure.com/models
    /// </summary>
    public string? Endpoint { get; set; }

    /// <summary>
    /// API key for authentication. If not provided, DefaultAzureCredential will be used.
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Model deployment name to use for chat completions.
    /// This should match the deployment name in your Azure AI Foundry project.
    /// </summary>
    public string? DeploymentName { get; set; }

    /// <summary>
    /// Whether to use managed identity authentication instead of API key.
    /// When true, DefaultAzureCredential will be used.
    /// </summary>
    public bool UseManagedIdentity { get; set; } = false;
}
