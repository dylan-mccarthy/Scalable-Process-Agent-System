using ControlPlane.Api.AgentRuntime;
using Microsoft.Extensions.Logging.Abstractions;

namespace ControlPlane.Api.Tests;

public class AzureAIFoundryToolProviderTests
{
    private readonly InMemoryToolRegistry _toolRegistry;
    private readonly AzureAIFoundryToolProvider _toolProvider;

    public AzureAIFoundryToolProviderTests()
    {
        _toolRegistry = new InMemoryToolRegistry(NullLogger<InMemoryToolRegistry>.Instance);
        _toolProvider = new AzureAIFoundryToolProvider(
            _toolRegistry,
            NullLogger<AzureAIFoundryToolProvider>.Instance);
    }

    [Fact]
    public async Task RegisterAzureAIFoundryToolsAsync_RegistersAllBuiltInTools()
    {
        // Act
        await _toolProvider.RegisterAzureAIFoundryToolsAsync();

        // Assert
        var allTools = await _toolRegistry.GetAllToolsAsync();
        var toolList = allTools.ToList();

        Assert.Equal(8, toolList.Count); // Should register 8 built-in tools

        // Verify each expected tool is registered
        Assert.Contains(toolList, t => t.Name == "CodeInterpreter");
        Assert.Contains(toolList, t => t.Name == "FileSearch");
        Assert.Contains(toolList, t => t.Name == "AzureAISearch");
        Assert.Contains(toolList, t => t.Name == "BingGrounding");
        Assert.Contains(toolList, t => t.Name == "FunctionCalling");
        Assert.Contains(toolList, t => t.Name == "AzureFunctions");
        Assert.Contains(toolList, t => t.Name == "OpenAPI");
        Assert.Contains(toolList, t => t.Name == "BrowserAutomation");
    }

    [Fact]
    public async Task RegisterAzureAIFoundryToolsAsync_CodeInterpreter_HasCorrectConfiguration()
    {
        // Act
        await _toolProvider.RegisterAzureAIFoundryToolsAsync();
        var tool = await _toolRegistry.GetToolAsync("CodeInterpreter");

        // Assert
        Assert.NotNull(tool);
        Assert.Equal("CodeInterpreter", tool.Name);
        Assert.Equal("azure-ai-foundry", tool.Type);
        Assert.NotNull(tool.Configuration);
        Assert.Equal("Azure AI Foundry", tool.Configuration["provider"]);
        Assert.Equal("execution", tool.Configuration["category"]);
        Assert.True((bool)tool.Configuration["sandboxed"]);
    }

    [Fact]
    public async Task RegisterAzureAIFoundryToolsAsync_FileSearch_HasCorrectConfiguration()
    {
        // Act
        await _toolProvider.RegisterAzureAIFoundryToolsAsync();
        var tool = await _toolRegistry.GetToolAsync("FileSearch");

        // Assert
        Assert.NotNull(tool);
        Assert.Equal("FileSearch", tool.Name);
        Assert.Equal("azure-ai-foundry", tool.Type);
        Assert.NotNull(tool.Configuration);
        Assert.Equal("Azure AI Foundry", tool.Configuration["provider"]);
        Assert.Equal("retrieval", tool.Configuration["category"]);
        Assert.Equal(10000, tool.Configuration["maxFiles"]);
        Assert.Equal(800, tool.Configuration["chunkSize"]);
    }

    [Fact]
    public async Task RegisterAzureAIFoundryToolsAsync_AzureAISearch_HasCorrectConfiguration()
    {
        // Act
        await _toolProvider.RegisterAzureAIFoundryToolsAsync();
        var tool = await _toolRegistry.GetToolAsync("AzureAISearch");

        // Assert
        Assert.NotNull(tool);
        Assert.Equal("AzureAISearch", tool.Name);
        Assert.Equal("azure-ai-foundry", tool.Type);
        Assert.NotNull(tool.Configuration);
        Assert.Equal("retrieval", tool.Configuration["category"]);
        Assert.True((bool)tool.Configuration["requiresIndex"]);
    }

    [Fact]
    public async Task RegisterAzureAIFoundryToolsAsync_BingGrounding_HasCorrectConfiguration()
    {
        // Act
        await _toolProvider.RegisterAzureAIFoundryToolsAsync();
        var tool = await _toolRegistry.GetToolAsync("BingGrounding");

        // Assert
        Assert.NotNull(tool);
        Assert.Equal("BingGrounding", tool.Name);
        Assert.Equal("azure-ai-foundry", tool.Type);
        Assert.NotNull(tool.Configuration);
        Assert.Equal("retrieval", tool.Configuration["category"]);
        Assert.True((bool)tool.Configuration["realtime"]);
    }

    [Fact]
    public async Task RegisterAzureAIFoundryToolsAsync_FunctionCalling_HasCorrectConfiguration()
    {
        // Act
        await _toolProvider.RegisterAzureAIFoundryToolsAsync();
        var tool = await _toolRegistry.GetToolAsync("FunctionCalling");

        // Assert
        Assert.NotNull(tool);
        Assert.Equal("FunctionCalling", tool.Name);
        Assert.Equal("azure-ai-foundry", tool.Type);
        Assert.NotNull(tool.Configuration);
        Assert.Equal("integration", tool.Configuration["category"]);
        Assert.True((bool)tool.Configuration["customizable"]);
    }

    [Fact]
    public async Task RegisterAzureAIFoundryToolsAsync_AzureFunctions_HasCorrectConfiguration()
    {
        // Act
        await _toolProvider.RegisterAzureAIFoundryToolsAsync();
        var tool = await _toolRegistry.GetToolAsync("AzureFunctions");

        // Assert
        Assert.NotNull(tool);
        Assert.Equal("AzureFunctions", tool.Name);
        Assert.Equal("azure-ai-foundry", tool.Type);
        Assert.NotNull(tool.Configuration);
        Assert.Equal("execution", tool.Configuration["category"]);
        Assert.True((bool)tool.Configuration["serverless"]);
    }

    [Fact]
    public async Task RegisterAzureAIFoundryToolsAsync_OpenAPI_HasCorrectConfiguration()
    {
        // Act
        await _toolProvider.RegisterAzureAIFoundryToolsAsync();
        var tool = await _toolRegistry.GetToolAsync("OpenAPI");

        // Assert
        Assert.NotNull(tool);
        Assert.Equal("OpenAPI", tool.Name);
        Assert.Equal("azure-ai-foundry", tool.Type);
        Assert.NotNull(tool.Configuration);
        Assert.Equal("integration", tool.Configuration["category"]);
        Assert.Equal("3.0", tool.Configuration["specVersion"]);
    }

    [Fact]
    public async Task RegisterAzureAIFoundryToolsAsync_BrowserAutomation_HasCorrectConfiguration()
    {
        // Act
        await _toolProvider.RegisterAzureAIFoundryToolsAsync();
        var tool = await _toolRegistry.GetToolAsync("BrowserAutomation");

        // Assert
        Assert.NotNull(tool);
        Assert.Equal("BrowserAutomation", tool.Name);
        Assert.Equal("azure-ai-foundry", tool.Type);
        Assert.NotNull(tool.Configuration);
        Assert.Equal("automation", tool.Configuration["category"]);
        Assert.True((bool)tool.Configuration["headless"]);
    }

    [Fact]
    public void GetAvailableToolTypes_ReturnsExpectedToolTypes()
    {
        // Act
        var toolTypes = _toolProvider.GetAvailableToolTypes();

        // Assert
        Assert.NotNull(toolTypes);
        Assert.Equal(8, toolTypes.Count);
        Assert.Contains("CodeInterpreter", toolTypes);
        Assert.Contains("FileSearch", toolTypes);
        Assert.Contains("AzureAISearch", toolTypes);
        Assert.Contains("BingGrounding", toolTypes);
        Assert.Contains("FunctionCalling", toolTypes);
        Assert.Contains("AzureFunctions", toolTypes);
        Assert.Contains("OpenAPI", toolTypes);
        Assert.Contains("BrowserAutomation", toolTypes);
    }

    [Fact]
    public async Task RegisterAzureAIFoundryToolsAsync_CanBeCalledMultipleTimes()
    {
        // Act
        await _toolProvider.RegisterAzureAIFoundryToolsAsync();
        await _toolProvider.RegisterAzureAIFoundryToolsAsync(); // Register again

        // Assert - Should not throw and tools should still be registered
        var allTools = await _toolRegistry.GetAllToolsAsync();
        Assert.Equal(8, allTools.Count());
    }

    [Fact]
    public async Task RegisteredTools_CanBeAssociatedWithAgents()
    {
        // Arrange
        await _toolProvider.RegisterAzureAIFoundryToolsAsync();
        const string agentId = "test-agent";

        // Act
        await _toolRegistry.AssociateToolWithAgentAsync(agentId, "CodeInterpreter");
        await _toolRegistry.AssociateToolWithAgentAsync(agentId, "FileSearch");
        var agentTools = await _toolRegistry.GetToolsForAgentAsync(agentId);

        // Assert
        Assert.Equal(2, agentTools.Count());
        Assert.Contains(agentTools, t => t.Name == "CodeInterpreter");
        Assert.Contains(agentTools, t => t.Name == "FileSearch");
    }

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenToolRegistryIsNull()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new AzureAIFoundryToolProvider(
                null!,
                NullLogger<AzureAIFoundryToolProvider>.Instance));
    }

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenLoggerIsNull()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new AzureAIFoundryToolProvider(_toolRegistry, null!));
    }

    [Fact]
    public async Task RegisteredTools_HaveDescriptions()
    {
        // Act
        await _toolProvider.RegisterAzureAIFoundryToolsAsync();
        var allTools = await _toolRegistry.GetAllToolsAsync();

        // Assert - All tools should have non-empty descriptions
        foreach (var tool in allTools)
        {
            Assert.NotNull(tool.Description);
            Assert.NotEmpty(tool.Description);
        }
    }

    [Fact]
    public async Task RegisteredTools_AreAzureAIFoundryType()
    {
        // Act
        await _toolProvider.RegisterAzureAIFoundryToolsAsync();
        var allTools = await _toolRegistry.GetAllToolsAsync();

        // Assert - All tools should be of type "azure-ai-foundry"
        foreach (var tool in allTools)
        {
            Assert.Equal("azure-ai-foundry", tool.Type);
        }
    }
}
