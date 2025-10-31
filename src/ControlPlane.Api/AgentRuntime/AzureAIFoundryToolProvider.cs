using Microsoft.Extensions.Logging;

namespace ControlPlane.Api.AgentRuntime;

/// <summary>
/// Interface for providing Azure AI Foundry tools to the tool registry.
/// </summary>
public interface IAzureAIFoundryToolProvider
{
    /// <summary>
    /// Registers all Azure AI Foundry built-in tools with the tool registry.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RegisterAzureAIFoundryToolsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the list of available Azure AI Foundry tool types.
    /// </summary>
    IReadOnlyList<string> GetAvailableToolTypes();
}

/// <summary>
/// Provides Azure AI Foundry built-in tools to the tool registry.
/// This service integrates Azure AI Foundry tools with the Microsoft Agent Framework SDK.
/// </summary>
public class AzureAIFoundryToolProvider : IAzureAIFoundryToolProvider
{
    private readonly IToolRegistry _toolRegistry;
    private readonly ILogger<AzureAIFoundryToolProvider> _logger;

    /// <summary>
    /// List of Azure AI Foundry built-in tool types.
    /// </summary>
    private static readonly IReadOnlyList<string> AvailableToolTypes = new List<string>
    {
        "CodeInterpreter",
        "FileSearch",
        "AzureAISearch",
        "BingGrounding",
        "FunctionCalling",
        "AzureFunctions",
        "OpenAPI",
        "BrowserAutomation"
    }.AsReadOnly();

    public AzureAIFoundryToolProvider(
        IToolRegistry toolRegistry,
        ILogger<AzureAIFoundryToolProvider> logger)
    {
        _toolRegistry = toolRegistry ?? throw new ArgumentNullException(nameof(toolRegistry));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public async Task RegisterAzureAIFoundryToolsAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Registering Azure AI Foundry built-in tools with tool registry");

        // Register Code Interpreter tool
        await RegisterCodeInterpreterAsync(cancellationToken);

        // Register File Search tool
        await RegisterFileSearchAsync(cancellationToken);

        // Register Azure AI Search tool
        await RegisterAzureAISearchAsync(cancellationToken);

        // Register Bing Grounding tool
        await RegisterBingGroundingAsync(cancellationToken);

        // Register Function Calling capability
        await RegisterFunctionCallingAsync(cancellationToken);

        // Register Azure Functions tool
        await RegisterAzureFunctionsAsync(cancellationToken);

        // Register OpenAPI tool
        await RegisterOpenAPIAsync(cancellationToken);

        // Register Browser Automation tool
        await RegisterBrowserAutomationAsync(cancellationToken);

        _logger.LogInformation(
            "Successfully registered {ToolCount} Azure AI Foundry tools",
            AvailableToolTypes.Count);
    }

    /// <inheritdoc/>
    public IReadOnlyList<string> GetAvailableToolTypes() => AvailableToolTypes;

    private async Task RegisterCodeInterpreterAsync(CancellationToken cancellationToken)
    {
        var tool = new ToolDefinition
        {
            Name = "CodeInterpreter",
            Description = "Enables agents to write and run Python code in a sandboxed execution environment. " +
                         "Can handle diverse data formats and generate files with data and visuals.",
            Type = "azure-ai-foundry",
            Configuration = new Dictionary<string, object>
            {
                { "provider", "Azure AI Foundry" },
                { "category", "execution" },
                { "capabilities", new[] { "python", "data-analysis", "file-generation" } },
                { "sandboxed", true }
            }
        };

        await _toolRegistry.RegisterToolAsync(tool, cancellationToken);
        _logger.LogDebug("Registered Code Interpreter tool");
    }

    private async Task RegisterFileSearchAsync(CancellationToken cancellationToken)
    {
        var tool = new ToolDefinition
        {
            Name = "FileSearch",
            Description = "Augments agents with knowledge from outside its model, such as proprietary product " +
                         "information or documents. Implements RAG (Retrieval Augmented Generation) with automatic " +
                         "chunking, embedding, and semantic search.",
            Type = "azure-ai-foundry",
            Configuration = new Dictionary<string, object>
            {
                { "provider", "Azure AI Foundry" },
                { "category", "retrieval" },
                { "maxFiles", 10000 },
                { "maxFileSize", "512MB" },
                { "chunkSize", 800 },
                { "chunkOverlap", 400 },
                { "embeddingModel", "text-embedding-3-large" },
                { "maxChunksInContext", 20 }
            }
        };

        await _toolRegistry.RegisterToolAsync(tool, cancellationToken);
        _logger.LogDebug("Registered File Search tool");
    }

    private async Task RegisterAzureAISearchAsync(CancellationToken cancellationToken)
    {
        var tool = new ToolDefinition
        {
            Name = "AzureAISearch",
            Description = "Enterprise search system for high-performance applications. Integrates with Azure OpenAI " +
                         "Service offering vector search, full-text search, and hybrid search capabilities. " +
                         "Ideal for knowledge base insights and information discovery.",
            Type = "azure-ai-foundry",
            Configuration = new Dictionary<string, object>
            {
                { "provider", "Azure AI Foundry" },
                { "category", "retrieval" },
                { "capabilities", new[] { "vector-search", "full-text-search", "hybrid-search" } },
                { "requiresIndex", true }
            }
        };

        await _toolRegistry.RegisterToolAsync(tool, cancellationToken);
        _logger.LogDebug("Registered Azure AI Search tool");
    }

    private async Task RegisterBingGroundingAsync(CancellationToken cancellationToken)
    {
        var tool = new ToolDefinition
        {
            Name = "BingGrounding",
            Description = "Enables agents to use Grounding with Bing Search to access and return information " +
                         "from the internet. Provides real-time web search capabilities for up-to-date information.",
            Type = "azure-ai-foundry",
            Configuration = new Dictionary<string, object>
            {
                { "provider", "Azure AI Foundry" },
                { "category", "retrieval" },
                { "source", "web" },
                { "realtime", true }
            }
        };

        await _toolRegistry.RegisterToolAsync(tool, cancellationToken);
        _logger.LogDebug("Registered Bing Grounding tool");
    }

    private async Task RegisterFunctionCallingAsync(CancellationToken cancellationToken)
    {
        var tool = new ToolDefinition
        {
            Name = "FunctionCalling",
            Description = "Describes the structure of custom functions to an agent and enables them to be called " +
                         "when appropriate during interactions. Supports custom business logic integration.",
            Type = "azure-ai-foundry",
            Configuration = new Dictionary<string, object>
            {
                { "provider", "Azure AI Foundry" },
                { "category", "integration" },
                { "customizable", true },
                { "requiresSchema", true }
            }
        };

        await _toolRegistry.RegisterToolAsync(tool, cancellationToken);
        _logger.LogDebug("Registered Function Calling capability");
    }

    private async Task RegisterAzureFunctionsAsync(CancellationToken cancellationToken)
    {
        var tool = new ToolDefinition
        {
            Name = "AzureFunctions",
            Description = "Leverages Azure Functions to create intelligent, event-driven applications. " +
                         "Enables agents to execute serverless code for synchronous, asynchronous, and " +
                         "long-running operations.",
            Type = "azure-ai-foundry",
            Configuration = new Dictionary<string, object>
            {
                { "provider", "Azure AI Foundry" },
                { "category", "execution" },
                { "executionModes", new[] { "sync", "async", "long-running", "event-driven" } },
                { "serverless", true }
            }
        };

        await _toolRegistry.RegisterToolAsync(tool, cancellationToken);
        _logger.LogDebug("Registered Azure Functions tool");
    }

    private async Task RegisterOpenAPIAsync(CancellationToken cancellationToken)
    {
        var tool = new ToolDefinition
        {
            Name = "OpenAPI",
            Description = "Connects agents to external APIs using OpenAPI 3.0 specification. " +
                         "Enables integration with any REST API that provides an OpenAPI specification.",
            Type = "azure-ai-foundry",
            Configuration = new Dictionary<string, object>
            {
                { "provider", "Azure AI Foundry" },
                { "category", "integration" },
                { "specVersion", "3.0" },
                { "requiresSpec", true },
                { "authSupport", new[] { "none", "api-key", "oauth2" } }
            }
        };

        await _toolRegistry.RegisterToolAsync(tool, cancellationToken);
        _logger.LogDebug("Registered OpenAPI tool");
    }

    private async Task RegisterBrowserAutomationAsync(CancellationToken cancellationToken)
    {
        var tool = new ToolDefinition
        {
            Name = "BrowserAutomation",
            Description = "Performs real-world browser tasks through natural language prompts. " +
                         "Enables automated browsing activities without human intervention.",
            Type = "azure-ai-foundry",
            Configuration = new Dictionary<string, object>
            {
                { "provider", "Azure AI Foundry" },
                { "category", "automation" },
                { "capabilities", new[] { "navigation", "interaction", "data-extraction" } },
                { "headless", true }
            }
        };

        await _toolRegistry.RegisterToolAsync(tool, cancellationToken);
        _logger.LogDebug("Registered Browser Automation tool");
    }
}
