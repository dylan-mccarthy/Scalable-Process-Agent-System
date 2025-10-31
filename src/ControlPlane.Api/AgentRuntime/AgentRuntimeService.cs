using System.Diagnostics;
using ControlPlane.Api.Models;
using ControlPlane.Api.Services;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace ControlPlane.Api.AgentRuntime;

/// <summary>
/// Service for managing agent runtime using Microsoft Agent Framework
/// </summary>
public class AgentRuntimeService : IAgentRuntime
{
    private readonly IAgentStore _agentStore;
    private readonly IToolRegistry _toolRegistry;
    private readonly ILogger<AgentRuntimeService> _logger;
    private readonly AgentRuntimeOptions _options;

    public AgentRuntimeService(
        IAgentStore agentStore,
        IToolRegistry toolRegistry,
        ILogger<AgentRuntimeService> logger,
        AgentRuntimeOptions options)
    {
        _agentStore = agentStore;
        _toolRegistry = toolRegistry;
        _logger = logger;
        _options = options;
    }

    public async Task<AIAgent> CreateAgentAsync(string agentId, CancellationToken cancellationToken = default)
    {
        var agent = await _agentStore.GetAgentAsync(agentId);
        if (agent == null)
        {
            throw new InvalidOperationException($"Agent with ID {agentId} not found");
        }

        _logger.LogInformation("Creating agent runtime for agent {AgentId}", agentId);

        // Get the chat client based on model profile
        var chatClient = GetChatClient(agent.ModelProfile);

        // Create AI agent with instructions
        var aiAgent = chatClient.CreateAIAgent(
            instructions: agent.Instructions,
            name: agent.Name
        );

        // Register tools if any are configured
        var tools = await _toolRegistry.GetToolsForAgentAsync(agentId, cancellationToken);
        if (tools.Any())
        {
            _logger.LogInformation("Registering {ToolCount} tools for agent {AgentId}", tools.Count(), agentId);
            // Tool registration will be implemented based on MAF tool system
        }

        return aiAgent;
    }

    public async Task<AgentExecutionResult> ExecuteAsync(
        AIAgent agent,
        string input,
        Dictionary<string, object>? context = null,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new AgentExecutionResult
        {
            Metadata = new Dictionary<string, object>()
        };

        try
        {
            _logger.LogInformation("Executing agent with input: {Input}", input);

            // Execute the agent with the input message
            var response = await agent.RunAsync(input, cancellationToken: cancellationToken);

            stopwatch.Stop();

            result.Success = true;
            result.Output = response.Text;
            result.Duration = stopwatch.Elapsed;

            // Extract token usage if available from metadata
            if (context != null)
            {
                result.Metadata = context;
            }

            _logger.LogInformation("Agent execution completed successfully in {Duration}ms",
                stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            result.Success = false;
            result.Error = ex.Message;
            result.Duration = stopwatch.Elapsed;

            _logger.LogError(ex, "Agent execution failed");
        }

        return result;
    }

    public async Task<bool> ValidateAgentAsync(string agentId, CancellationToken cancellationToken = default)
    {
        try
        {
            var agent = await _agentStore.GetAgentAsync(agentId);
            if (agent == null)
            {
                _logger.LogWarning("Agent {AgentId} not found during validation", agentId);
                return false;
            }

            // Validate that required fields are present
            if (string.IsNullOrWhiteSpace(agent.Name) || string.IsNullOrWhiteSpace(agent.Instructions))
            {
                _logger.LogWarning("Agent {AgentId} has invalid configuration", agentId);
                return false;
            }

            // Validate model profile
            if (agent.ModelProfile == null || !agent.ModelProfile.ContainsKey("model"))
            {
                _logger.LogWarning("Agent {AgentId} missing model profile", agentId);
                return false;
            }

            _logger.LogInformation("Agent {AgentId} validation successful", agentId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating agent {AgentId}", agentId);
            return false;
        }
    }

    private IChatClient GetChatClient(Dictionary<string, object>? modelProfile)
    {
        // This is a placeholder that will be replaced with actual Azure AI Foundry integration
        // For now, we'll throw to indicate this needs to be configured
        throw new NotImplementedException(
            "Chat client creation needs to be configured with Azure AI Foundry or OpenAI credentials. " +
            "This will be implemented in E3-T4 (Azure AI Foundry integration).");
    }
}

/// <summary>
/// Configuration options for agent runtime
/// </summary>
public class AgentRuntimeOptions
{
    public const string DefaultModelValue = "gpt-4";

    public string? DefaultModel { get; set; } = DefaultModelValue;
    public double DefaultTemperature { get; set; } = 0.7;
    public int MaxTokens { get; set; } = 4000;
    public int MaxDurationSeconds { get; set; } = 60;
}
