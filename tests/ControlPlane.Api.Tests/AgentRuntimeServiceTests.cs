using ControlPlane.Api.AgentRuntime;
using ControlPlane.Api.Models;
using ControlPlane.Api.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace ControlPlane.Api.Tests;

public class AgentRuntimeServiceTests
{
    private readonly InMemoryAgentStore _agentStore;
    private readonly InMemoryToolRegistry _toolRegistry;
    private readonly AgentRuntimeService _runtimeService;

    public AgentRuntimeServiceTests()
    {
        _agentStore = new InMemoryAgentStore();
        _toolRegistry = new InMemoryToolRegistry(NullLogger<InMemoryToolRegistry>.Instance);
        var options = new AgentRuntimeOptions
        {
            DefaultModel = "gpt-4",
            DefaultTemperature = 0.7,
            MaxTokens = 4000,
            MaxDurationSeconds = 60
        };
        _runtimeService = new AgentRuntimeService(
            _agentStore,
            _toolRegistry,
            NullLogger<AgentRuntimeService>.Instance,
            options);
    }

    [Fact]
    public async Task ValidateAgentAsync_ReturnsTrue_ForValidAgent()
    {
        // Arrange
        var agent = await _agentStore.CreateAgentAsync(new CreateAgentRequest
        {
            Name = "Test Agent",
            Instructions = "Test instructions",
            ModelProfile = new Dictionary<string, object>
            {
                { "model", "gpt-4" },
                { "temperature", 0.7 }
            }
        });

        // Act
        var isValid = await _runtimeService.ValidateAgentAsync(agent.AgentId);

        // Assert
        Assert.True(isValid);
    }

    [Fact]
    public async Task ValidateAgentAsync_ReturnsFalse_WhenAgentNotFound()
    {
        // Act
        var isValid = await _runtimeService.ValidateAgentAsync("non-existent-agent");

        // Assert
        Assert.False(isValid);
    }

    [Fact]
    public async Task ValidateAgentAsync_ReturnsFalse_WhenNameIsEmpty()
    {
        // Arrange - Create agent then update to invalid state
        var agent = await _agentStore.CreateAgentAsync(new CreateAgentRequest
        {
            Name = "temp",
            Instructions = "temp",
            ModelProfile = new Dictionary<string, object> { { "model", "gpt-4" } }
        });

        // Update to invalid state (empty name)
        await _agentStore.UpdateAgentAsync(agent.AgentId, new UpdateAgentRequest { Name = "" });

        // Act
        var isValid = await _runtimeService.ValidateAgentAsync(agent.AgentId);

        // Assert
        Assert.False(isValid);
    }

    [Fact]
    public async Task ValidateAgentAsync_ReturnsFalse_WhenInstructionsAreEmpty()
    {
        // Arrange - Create agent then update to invalid state
        var agent = await _agentStore.CreateAgentAsync(new CreateAgentRequest
        {
            Name = "Test Agent",
            Instructions = "Valid"
        });
        await _agentStore.UpdateAgentAsync(agent.AgentId, new UpdateAgentRequest
        {
            Instructions = ""
        });

        // Act
        var isValid = await _runtimeService.ValidateAgentAsync(agent.AgentId);

        // Assert
        Assert.False(isValid);
    }

    [Fact]
    public async Task ValidateAgentAsync_ReturnsFalse_WhenModelProfileIsMissing()
    {
        // Arrange
        var agent = await _agentStore.CreateAgentAsync(new CreateAgentRequest
        {
            Name = "Test Agent",
            Instructions = "Test instructions",
            ModelProfile = null
        });

        // Act
        var isValid = await _runtimeService.ValidateAgentAsync(agent.AgentId);

        // Assert
        Assert.False(isValid);
    }

    [Fact]
    public async Task ValidateAgentAsync_ReturnsFalse_WhenModelProfileMissingModelKey()
    {
        // Arrange
        var agent = await _agentStore.CreateAgentAsync(new CreateAgentRequest
        {
            Name = "Test Agent",
            Instructions = "Test instructions",
            ModelProfile = new Dictionary<string, object> { { "temperature", 0.7 } } // Missing "model" key
        });

        // Act
        var isValid = await _runtimeService.ValidateAgentAsync(agent.AgentId);

        // Assert
        Assert.False(isValid);
    }

    [Fact]
    public async Task CreateAgentAsync_ThrowsException_WhenAgentNotFound()
    {
        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await _runtimeService.CreateAgentAsync("non-existent-agent"));
    }

    [Fact]
    public async Task CreateAgentAsync_ThrowsNotImplementedException_WhenChatClientNotConfigured()
    {
        // Arrange
        var agent = await _agentStore.CreateAgentAsync(new CreateAgentRequest
        {
            Name = "Test Agent",
            Instructions = "Test instructions",
            ModelProfile = new Dictionary<string, object>
            {
                { "model", "gpt-4" },
                { "temperature", 0.7 }
            }
        });

        // Act & Assert
        // The CreateAgentAsync will throw NotImplementedException because we haven't configured
        // Azure AI Foundry credentials (which is expected for this MVP phase)
        await Assert.ThrowsAsync<NotImplementedException>(
            async () => await _runtimeService.CreateAgentAsync(agent.AgentId));
    }
}
