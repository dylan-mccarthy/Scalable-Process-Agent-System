using System.Diagnostics;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Node.Runtime.Configuration;
using Microsoft.Extensions.Options;

namespace Node.Runtime.Services;

/// <summary>
/// Result of an agent execution.
/// </summary>
public class AgentExecutionResult
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
    /// Additional metadata from the execution.
    /// </summary>
    public Dictionary<string, object>? Metadata { get; set; }

    /// <summary>
    /// Number of input tokens used.
    /// </summary>
    public int TokensIn { get; set; }

    /// <summary>
    /// Number of output tokens used.
    /// </summary>
    public int TokensOut { get; set; }

    /// <summary>
    /// Total duration of the execution.
    /// </summary>
    public TimeSpan Duration { get; set; }

    /// <summary>
    /// Estimated cost in USD.
    /// </summary>
    public double UsdCost { get; set; }
}

/// <summary>
/// Specification for an agent to be executed.
/// </summary>
public class AgentSpec
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
    /// Model profile configuration.
    /// </summary>
    public Dictionary<string, object>? ModelProfile { get; set; }

    /// <summary>
    /// Budget constraints for execution.
    /// </summary>
    public BudgetConstraints? Budget { get; set; }
}

/// <summary>
/// Budget constraints for agent execution.
/// </summary>
public class BudgetConstraints
{
    /// <summary>
    /// Maximum tokens allowed.
    /// </summary>
    public int? MaxTokens { get; set; }

    /// <summary>
    /// Maximum duration in seconds.
    /// </summary>
    public int? MaxDurationSeconds { get; set; }
}

/// <summary>
/// Interface for executing agents using Microsoft Agent Framework.
/// </summary>
public interface IAgentExecutor
{
    /// <summary>
    /// Executes an agent with the given input.
    /// </summary>
    /// <param name="spec">Agent specification containing definition and budget.</param>
    /// <param name="input">Input message for the agent.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result of the agent execution.</returns>
    Task<AgentExecutionResult> ExecuteAsync(
        AgentSpec spec,
        string input,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Service for executing agents using Microsoft Agent Framework SDK.
/// </summary>
public class AgentExecutorService : IAgentExecutor
{
    private readonly AgentRuntimeOptions _options;
    private readonly ILogger<AgentExecutorService> _logger;

    public AgentExecutorService(
        IOptions<AgentRuntimeOptions> options,
        ILogger<AgentExecutorService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<AgentExecutionResult> ExecuteAsync(
        AgentSpec spec,
        string input,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new AgentExecutionResult
        {
            Metadata = new Dictionary<string, object>()
        };

        try
        {
            _logger.LogInformation(
                "Executing agent {AgentId} v{Version} with input: {Input}",
                spec.AgentId, spec.Version, input);

            // Apply budget constraints
            var maxTokens = spec.Budget?.MaxTokens ?? _options.MaxTokens;
            var maxDurationSeconds = spec.Budget?.MaxDurationSeconds ?? _options.MaxDurationSeconds;
            var timeout = TimeSpan.FromSeconds(maxDurationSeconds);

            // Create timeout cancellation token
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(timeout);

            // Get chat client from model profile
            var chatClient = GetChatClient(spec.ModelProfile);

            // Create AI agent with instructions
            var aiAgent = chatClient.CreateAIAgent(
                instructions: spec.Instructions,
                name: spec.Name
            );

            _logger.LogDebug("Created AI agent for {AgentId}", spec.AgentId);

            // Execute the agent with timeout
            var response = await aiAgent.RunAsync(
                input,
                cancellationToken: timeoutCts.Token);

            stopwatch.Stop();

            result.Success = true;
            result.Output = response.Text;
            result.Duration = stopwatch.Elapsed;

            // Extract token usage from response metadata if available
            // Note: Token usage extraction depends on the model provider
            // For now, we'll use estimated values
            result.TokensIn = EstimateTokens(input);
            result.TokensOut = EstimateTokens(response.Text ?? string.Empty);
            result.UsdCost = EstimateCost(result.TokensIn, result.TokensOut);

            _logger.LogInformation(
                "Agent execution completed successfully in {DurationMs}ms (Tokens: {TokensIn}/{TokensOut}, Cost: ${Cost:F4})",
                stopwatch.ElapsedMilliseconds,
                result.TokensIn,
                result.TokensOut,
                result.UsdCost);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Timeout occurred
            stopwatch.Stop();
            result.Success = false;
            result.Error = $"Agent execution exceeded maximum duration of {spec.Budget?.MaxDurationSeconds ?? _options.MaxDurationSeconds} seconds";
            result.Duration = stopwatch.Elapsed;

            _logger.LogWarning(
                "Agent execution timed out after {DurationMs}ms",
                stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            result.Success = false;
            result.Error = ex.Message;
            result.Duration = stopwatch.Elapsed;

            _logger.LogError(ex, "Agent execution failed after {DurationMs}ms", stopwatch.ElapsedMilliseconds);
        }

        return result;
    }

    /// <summary>
    /// Gets a chat client based on the model profile.
    /// This is a placeholder that will be replaced with actual Azure AI Foundry integration.
    /// </summary>
    private IChatClient GetChatClient(Dictionary<string, object>? modelProfile)
    {
        // This will be implemented in E3-T4 (Azure AI Foundry integration)
        // For now, we throw to indicate this needs configuration
        throw new NotImplementedException(
            "Chat client creation needs to be configured with Azure AI Foundry or OpenAI credentials. " +
            "This will be implemented in E3-T4 (Azure AI Foundry integration). " +
            "The agent executor is ready to execute agents once a model provider is configured.");
    }

    /// <summary>
    /// Estimates token count for a given text.
    /// This is a rough approximation (1 token ~= 4 characters for English).
    /// </summary>
    private int EstimateTokens(string text)
    {
        if (string.IsNullOrEmpty(text))
            return 0;

        // Rough approximation: 1 token ≈ 4 characters
        return (int)Math.Ceiling(text.Length / 4.0);
    }

    /// <summary>
    /// Estimates cost based on token usage.
    /// Uses approximate GPT-4 pricing as a reference.
    /// </summary>
    private double EstimateCost(int tokensIn, int tokensOut)
    {
        // Approximate GPT-4 pricing (as of late 2023):
        // $0.03 per 1K input tokens, $0.06 per 1K output tokens
        const double inputCostPer1k = 0.03;
        const double outputCostPer1k = 0.06;

        var inputCost = (tokensIn / 1000.0) * inputCostPer1k;
        var outputCost = (tokensOut / 1000.0) * outputCostPer1k;

        return inputCost + outputCost;
    }
}
