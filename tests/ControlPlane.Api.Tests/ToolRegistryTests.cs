using ControlPlane.Api.AgentRuntime;
using Microsoft.Extensions.Logging.Abstractions;

namespace ControlPlane.Api.Tests;

public class ToolRegistryTests
{
    private readonly InMemoryToolRegistry _registry;

    public ToolRegistryTests()
    {
        _registry = new InMemoryToolRegistry(NullLogger<InMemoryToolRegistry>.Instance);
    }

    [Fact]
    public async Task RegisterTool_SuccessfullyRegistersTool()
    {
        // Arrange
        var tool = new ToolDefinition
        {
            Name = "test-tool",
            Description = "A test tool",
            Type = "function",
            Parameters = new Dictionary<string, object> { { "param1", "value1" } }
        };

        // Act
        await _registry.RegisterToolAsync(tool);
        var retrieved = await _registry.GetToolAsync("test-tool");

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal("test-tool", retrieved.Name);
        Assert.Equal("A test tool", retrieved.Description);
        Assert.Equal("function", retrieved.Type);
    }

    [Fact]
    public async Task RegisterTool_ThrowsException_WhenNameIsEmpty()
    {
        // Arrange
        var tool = new ToolDefinition
        {
            Name = "",
            Description = "Invalid tool"
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            async () => await _registry.RegisterToolAsync(tool));
    }

    [Fact]
    public async Task GetAllTools_ReturnsAllRegisteredTools()
    {
        // Arrange
        await _registry.RegisterToolAsync(new ToolDefinition { Name = "tool1", Description = "Tool 1", Type = "function" });
        await _registry.RegisterToolAsync(new ToolDefinition { Name = "tool2", Description = "Tool 2", Type = "api" });
        await _registry.RegisterToolAsync(new ToolDefinition { Name = "tool3", Description = "Tool 3", Type = "connector" });

        // Act
        var tools = await _registry.GetAllToolsAsync();

        // Assert
        Assert.Equal(3, tools.Count());
        Assert.Contains(tools, t => t.Name == "tool1");
        Assert.Contains(tools, t => t.Name == "tool2");
        Assert.Contains(tools, t => t.Name == "tool3");
    }

    [Fact]
    public async Task UnregisterTool_RemovesTool_WhenExists()
    {
        // Arrange
        var tool = new ToolDefinition { Name = "to-remove", Description = "Will be removed", Type = "function" };
        await _registry.RegisterToolAsync(tool);

        // Act
        var result = await _registry.UnregisterToolAsync("to-remove");
        var retrieved = await _registry.GetToolAsync("to-remove");

        // Assert
        Assert.True(result);
        Assert.Null(retrieved);
    }

    [Fact]
    public async Task UnregisterTool_ReturnsFalse_WhenToolNotFound()
    {
        // Act
        var result = await _registry.UnregisterToolAsync("non-existent");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task AssociateToolWithAgent_CreatesAssociation()
    {
        // Arrange
        var tool = new ToolDefinition { Name = "agent-tool", Description = "Agent specific tool", Type = "function" };
        await _registry.RegisterToolAsync(tool);

        // Act
        await _registry.AssociateToolWithAgentAsync("agent-1", "agent-tool");
        var agentTools = await _registry.GetToolsForAgentAsync("agent-1");

        // Assert
        Assert.Single(agentTools);
        Assert.Equal("agent-tool", agentTools.First().Name);
    }

    [Fact]
    public async Task AssociateToolWithAgent_ThrowsException_WhenToolNotRegistered()
    {
        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await _registry.AssociateToolWithAgentAsync("agent-1", "non-existent-tool"));
    }

    [Fact]
    public async Task DisassociateToolFromAgent_RemovesAssociation()
    {
        // Arrange
        var tool = new ToolDefinition { Name = "agent-tool", Description = "Agent specific tool", Type = "function" };
        await _registry.RegisterToolAsync(tool);
        await _registry.AssociateToolWithAgentAsync("agent-1", "agent-tool");

        // Act
        await _registry.DisassociateToolFromAgentAsync("agent-1", "agent-tool");
        var agentTools = await _registry.GetToolsForAgentAsync("agent-1");

        // Assert
        Assert.Empty(agentTools);
    }

    [Fact]
    public async Task GetToolsForAgent_ReturnsEmpty_WhenNoToolsAssociated()
    {
        // Act
        var tools = await _registry.GetToolsForAgentAsync("agent-without-tools");

        // Assert
        Assert.Empty(tools);
    }

    [Fact]
    public async Task UnregisterTool_RemovesFromAllAgentAssociations()
    {
        // Arrange
        var tool = new ToolDefinition { Name = "shared-tool", Description = "Shared tool", Type = "function" };
        await _registry.RegisterToolAsync(tool);
        await _registry.AssociateToolWithAgentAsync("agent-1", "shared-tool");
        await _registry.AssociateToolWithAgentAsync("agent-2", "shared-tool");

        // Act
        await _registry.UnregisterToolAsync("shared-tool");
        var agent1Tools = await _registry.GetToolsForAgentAsync("agent-1");
        var agent2Tools = await _registry.GetToolsForAgentAsync("agent-2");

        // Assert
        Assert.Empty(agent1Tools);
        Assert.Empty(agent2Tools);
    }
}
