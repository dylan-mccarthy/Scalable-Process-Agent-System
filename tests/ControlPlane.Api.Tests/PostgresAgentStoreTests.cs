using ControlPlane.Api.Data;
using ControlPlane.Api.Models;
using ControlPlane.Api.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace ControlPlane.Api.Tests;

/// <summary>
/// Unit tests for PostgresAgentStore ensuring data persistence layer works correctly.
/// Uses in-memory database for fast, isolated testing.
/// </summary>
public class PostgresAgentStoreTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly PostgresAgentStore _store;

    public PostgresAgentStoreTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
        _store = new PostgresAgentStore(_context);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    #region CreateAgentAsync Tests

    [Fact]
    public async Task CreateAgentAsync_WithValidRequest_CreatesAgent()
    {
        // Arrange
        var request = new CreateAgentRequest
        {
            Name = "Invoice Classifier",
            Description = "Classifies invoices by vendor",
            Instructions = "Analyze the invoice and extract vendor information"
        };

        // Act
        var result = await _store.CreateAgentAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.AgentId.Should().NotBeNullOrEmpty();
        result.Name.Should().Be(request.Name);
        result.Description.Should().Be(request.Description);
        result.Instructions.Should().Be(request.Instructions);

        var savedAgent = await _context.Agents.FindAsync(result.AgentId);
        savedAgent.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateAgentAsync_WithComplexData_SerializesCorrectly()
    {
        // Arrange
        var request = new CreateAgentRequest
        {
            Name = "Test Agent",
            ModelProfile = new Dictionary<string, object>
            {
                ["provider"] = "azure-openai",
                ["model"] = "gpt-4",
                ["temperature"] = 0.7
            },
            Budget = new AgentBudget
            {
                MaxTokens = 4000,
                MaxDurationSeconds = 60
            },
            Tools = new List<string> { "CodeInterpreter", "FileSearch" },
            Input = new ConnectorConfiguration
            {
                Type = "ServiceBus",
                Config = new Dictionary<string, object>
                {
                    ["connectionString"] = "test-connection"
                }
            },
            Output = new ConnectorConfiguration
            {
                Type = "Http",
                Config = new Dictionary<string, object>
                {
                    ["endpoint"] = "https://api.example.com"
                }
            },
            Metadata = new Dictionary<string, string>
            {
                ["environment"] = "test",
                ["version"] = "1.0"
            }
        };

        // Act
        var result = await _store.CreateAgentAsync(request);

        // Assert
        result.ModelProfile.Should().NotBeNull();
        result.ModelProfile!.Should().ContainKey("provider");
        result.ModelProfile.Should().ContainKey("model");
        result.ModelProfile.Should().ContainKey("temperature");

        result.Budget.Should().NotBeNull();
        result.Budget!.MaxTokens.Should().Be(4000);
        result.Budget.MaxDurationSeconds.Should().Be(60);

        result.Tools.Should().NotBeNull();
        result.Tools!.Should().HaveCount(2);
        result.Tools.Should().Contain(new[] { "CodeInterpreter", "FileSearch" });

        result.Input.Should().NotBeNull();
        result.Input!.Type.Should().Be("ServiceBus");
        result.Input.Config.Should().ContainKey("connectionString");

        result.Output.Should().NotBeNull();
        result.Output!.Type.Should().Be("Http");
        result.Output.Config.Should().ContainKey("endpoint");

        result.Metadata.Should().NotBeNull();
        result.Metadata!.Should().ContainKey("environment");
        result.Metadata.Should().ContainKey("version");
    }

    #endregion

    #region GetAgentAsync Tests

    [Fact]
    public async Task GetAgentAsync_WithExistingAgent_ReturnsAgent()
    {
        // Arrange
        var agent = await _store.CreateAgentAsync(new CreateAgentRequest
        {
            Name = "Test Agent"
        });

        // Act
        var result = await _store.GetAgentAsync(agent.AgentId);

        // Assert
        result.Should().NotBeNull();
        result!.AgentId.Should().Be(agent.AgentId);
        result.Name.Should().Be("Test Agent");
    }

    [Fact]
    public async Task GetAgentAsync_WithNonExistentAgent_ReturnsNull()
    {
        // Act
        var result = await _store.GetAgentAsync("non-existent-id");

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region GetAllAgentsAsync Tests

    [Fact]
    public async Task GetAllAgentsAsync_WithNoAgents_ReturnsEmptyList()
    {
        // Act
        var result = await _store.GetAllAgentsAsync();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAllAgentsAsync_WithMultipleAgents_ReturnsAllAgents()
    {
        // Arrange
        await _store.CreateAgentAsync(new CreateAgentRequest { Name = "Agent 1" });
        await _store.CreateAgentAsync(new CreateAgentRequest { Name = "Agent 2" });
        await _store.CreateAgentAsync(new CreateAgentRequest { Name = "Agent 3" });

        // Act
        var result = await _store.GetAllAgentsAsync();

        // Assert
        result.Should().HaveCount(3);
        result.Select(a => a.Name).Should().Contain(new[] { "Agent 1", "Agent 2", "Agent 3" });
    }

    #endregion

    #region UpdateAgentAsync Tests

    [Fact]
    public async Task UpdateAgentAsync_WithExistingAgent_UpdatesProperties()
    {
        // Arrange
        var agent = await _store.CreateAgentAsync(new CreateAgentRequest
        {
            Name = "Original Name",
            Description = "Original Description"
        });

        var updateRequest = new UpdateAgentRequest
        {
            Name = "Updated Name",
            Description = "Updated Description"
        };

        // Act
        var result = await _store.UpdateAgentAsync(agent.AgentId, updateRequest);

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be("Updated Name");
        result.Description.Should().Be("Updated Description");
    }

    [Fact]
    public async Task UpdateAgentAsync_WithPartialUpdate_OnlyUpdatesSpecifiedFields()
    {
        // Arrange
        var agent = await _store.CreateAgentAsync(new CreateAgentRequest
        {
            Name = "Original Name",
            Description = "Original Description",
            Instructions = "Original Instructions"
        });

        var updateRequest = new UpdateAgentRequest
        {
            Name = "Updated Name"
            // Description and Instructions not specified
        };

        // Act
        var result = await _store.UpdateAgentAsync(agent.AgentId, updateRequest);

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be("Updated Name");
        result.Description.Should().Be("Original Description");
        result.Instructions.Should().Be("Original Instructions");
    }

    [Fact]
    public async Task UpdateAgentAsync_WithNonExistentAgent_ReturnsNull()
    {
        // Arrange
        var updateRequest = new UpdateAgentRequest { Name = "Updated Name" };

        // Act
        var result = await _store.UpdateAgentAsync("non-existent-id", updateRequest);

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region DeleteAgentAsync Tests

    [Fact]
    public async Task DeleteAgentAsync_WithExistingAgent_DeletesAndReturnsTrue()
    {
        // Arrange
        var agent = await _store.CreateAgentAsync(new CreateAgentRequest { Name = "Test Agent" });

        // Act
        var result = await _store.DeleteAgentAsync(agent.AgentId);

        // Assert
        result.Should().BeTrue();

        var deletedAgent = await _store.GetAgentAsync(agent.AgentId);
        deletedAgent.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAgentAsync_WithNonExistentAgent_ReturnsFalse()
    {
        // Act
        var result = await _store.DeleteAgentAsync("non-existent-id");

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region Version Management Tests

    [Fact]
    public async Task CreateVersionAsync_WithValidRequest_CreatesVersion()
    {
        // Arrange
        var agent = await _store.CreateAgentAsync(new CreateAgentRequest { Name = "Test Agent" });
        var versionRequest = new CreateAgentVersionRequest
        {
            Version = "1.0.0",
            Spec = new Agent
            {
                Name = "Test Agent v1",
                Instructions = "Version 1 instructions"
            }
        };

        // Act
        var result = await _store.CreateVersionAsync(agent.AgentId, versionRequest);

        // Assert
        result.Should().NotBeNull();
        result.AgentId.Should().Be(agent.AgentId);
        result.Version.Should().Be("1.0.0");
        result.Spec.Should().NotBeNull();
        result.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task CreateVersionAsync_WithNonExistentAgent_ThrowsException()
    {
        // Arrange
        var versionRequest = new CreateAgentVersionRequest { Version = "1.0.0" };

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await _store.CreateVersionAsync("non-existent-id", versionRequest));
    }

    [Fact]
    public async Task CreateVersionAsync_WithDuplicateVersion_ThrowsException()
    {
        // Arrange
        var agent = await _store.CreateAgentAsync(new CreateAgentRequest { Name = "Test Agent" });
        var versionRequest = new CreateAgentVersionRequest { Version = "1.0.0" };

        await _store.CreateVersionAsync(agent.AgentId, versionRequest);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await _store.CreateVersionAsync(agent.AgentId, versionRequest));
    }

    [Fact]
    public async Task GetVersionAsync_WithExistingVersion_ReturnsVersion()
    {
        // Arrange
        var agent = await _store.CreateAgentAsync(new CreateAgentRequest { Name = "Test Agent" });
        var versionRequest = new CreateAgentVersionRequest { Version = "1.0.0" };
        await _store.CreateVersionAsync(agent.AgentId, versionRequest);

        // Act
        var result = await _store.GetVersionAsync(agent.AgentId, "1.0.0");

        // Assert
        result.Should().NotBeNull();
        result!.Version.Should().Be("1.0.0");
    }

    [Fact]
    public async Task GetVersionAsync_WithNonExistentVersion_ReturnsNull()
    {
        // Arrange
        var agent = await _store.CreateAgentAsync(new CreateAgentRequest { Name = "Test Agent" });

        // Act
        var result = await _store.GetVersionAsync(agent.AgentId, "1.0.0");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetVersionsAsync_WithMultipleVersions_ReturnsOrderedByCreatedAtDescending()
    {
        // Arrange
        var agent = await _store.CreateAgentAsync(new CreateAgentRequest { Name = "Test Agent" });

        await _store.CreateVersionAsync(agent.AgentId, new CreateAgentVersionRequest { Version = "1.0.0" });
        await Task.Delay(100); // Ensure different CreatedAt times
        await _store.CreateVersionAsync(agent.AgentId, new CreateAgentVersionRequest { Version = "1.1.0" });
        await Task.Delay(100);
        await _store.CreateVersionAsync(agent.AgentId, new CreateAgentVersionRequest { Version = "2.0.0" });

        // Act
        var result = await _store.GetVersionsAsync(agent.AgentId);

        // Assert
        result.Should().HaveCount(3);
        var versions = result.ToList();
        versions[0].Version.Should().Be("2.0.0"); // Most recent
        versions[1].Version.Should().Be("1.1.0");
        versions[2].Version.Should().Be("1.0.0"); // Oldest
    }

    [Fact]
    public async Task GetVersionsAsync_WithNoVersions_ReturnsEmptyList()
    {
        // Arrange
        var agent = await _store.CreateAgentAsync(new CreateAgentRequest { Name = "Test Agent" });

        // Act
        var result = await _store.GetVersionsAsync(agent.AgentId);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteVersionAsync_WithExistingVersion_DeletesAndReturnsTrue()
    {
        // Arrange
        var agent = await _store.CreateAgentAsync(new CreateAgentRequest { Name = "Test Agent" });
        await _store.CreateVersionAsync(agent.AgentId, new CreateAgentVersionRequest { Version = "1.0.0" });

        // Act
        var result = await _store.DeleteVersionAsync(agent.AgentId, "1.0.0");

        // Assert
        result.Should().BeTrue();

        var deletedVersion = await _store.GetVersionAsync(agent.AgentId, "1.0.0");
        deletedVersion.Should().BeNull();
    }

    [Fact]
    public async Task DeleteVersionAsync_WithNonExistentVersion_ReturnsFalse()
    {
        // Arrange
        var agent = await _store.CreateAgentAsync(new CreateAgentRequest { Name = "Test Agent" });

        // Act
        var result = await _store.DeleteVersionAsync(agent.AgentId, "1.0.0");

        // Assert
        result.Should().BeFalse();
    }

    #endregion
}
