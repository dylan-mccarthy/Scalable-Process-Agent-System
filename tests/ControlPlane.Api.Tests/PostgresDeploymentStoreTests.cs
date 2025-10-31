using ControlPlane.Api.Data;
using ControlPlane.Api.Models;
using ControlPlane.Api.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace ControlPlane.Api.Tests;

/// <summary>
/// Unit tests for PostgresDeploymentStore ensuring deployment lifecycle management works correctly.
/// </summary>
public class PostgresDeploymentStoreTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly PostgresDeploymentStore _store;

    public PostgresDeploymentStoreTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
        _store = new PostgresDeploymentStore(_context);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    #region CreateDeploymentAsync Tests

    [Fact]
    public async Task CreateDeploymentAsync_WithValidRequest_CreatesDeployment()
    {
        // Arrange
        var request = new CreateDeploymentRequest
        {
            AgentId = "agent-1",
            Version = "1.0.0",
            Env = "production",
            Target = new DeploymentTarget
            {
                Replicas = 3,
                Placement = new Dictionary<string, object>
                {
                    ["slotBudget"] = 8
                }
            }
        };

        // Act
        var result = await _store.CreateDeploymentAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.DepId.Should().NotBeNullOrEmpty();
        result.AgentId.Should().Be("agent-1");
        result.Version.Should().Be("1.0.0");
        result.Env.Should().Be("production");
        result.Target.Should().NotBeNull();
        result.Target!.Replicas.Should().Be(3);
        result.Status.Should().NotBeNull();
        result.Status!.State.Should().Be("pending");
    }

    [Fact]
    public async Task CreateDeploymentAsync_WithMinimalRequest_UsesDefaults()
    {
        // Arrange
        var request = new CreateDeploymentRequest
        {
            AgentId = "agent-1",
            Version = "1.0.0",
            Env = "dev"
        };

        // Act
        var result = await _store.CreateDeploymentAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Target.Should().NotBeNull();
        result.Target!.Replicas.Should().Be(1); // Default value
    }

    #endregion

    #region GetDeploymentAsync Tests

    [Fact]
    public async Task GetDeploymentAsync_WithExistingDeployment_ReturnsDeployment()
    {
        // Arrange
        var deployment = await _store.CreateDeploymentAsync(new CreateDeploymentRequest
        {
            AgentId = "agent-1",
            Version = "1.0.0",
            Env = "dev"
        });

        // Act
        var result = await _store.GetDeploymentAsync(deployment.DepId);

        // Assert
        result.Should().NotBeNull();
        result!.DepId.Should().Be(deployment.DepId);
        result.AgentId.Should().Be("agent-1");
    }

    [Fact]
    public async Task GetDeploymentAsync_WithNonExistentDeployment_ReturnsNull()
    {
        // Act
        var result = await _store.GetDeploymentAsync("non-existent-id");

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region GetAllDeploymentsAsync Tests

    [Fact]
    public async Task GetAllDeploymentsAsync_WithNoDeployments_ReturnsEmptyList()
    {
        // Act
        var result = await _store.GetAllDeploymentsAsync();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAllDeploymentsAsync_WithMultipleDeployments_ReturnsAllDeployments()
    {
        // Arrange
        await _store.CreateDeploymentAsync(new CreateDeploymentRequest
        {
            AgentId = "agent-1",
            Version = "1.0.0",
            Env = "dev"
        });
        await _store.CreateDeploymentAsync(new CreateDeploymentRequest
        {
            AgentId = "agent-2",
            Version = "2.0.0",
            Env = "prod"
        });

        // Act
        var result = await _store.GetAllDeploymentsAsync();

        // Assert
        result.Should().HaveCount(2);
    }

    #endregion

    #region GetDeploymentsByAgentAsync Tests

    [Fact]
    public async Task GetDeploymentsByAgentAsync_WithMatchingAgent_ReturnsDeployments()
    {
        // Arrange
        await _store.CreateDeploymentAsync(new CreateDeploymentRequest
        {
            AgentId = "agent-1",
            Version = "1.0.0",
            Env = "dev"
        });
        await _store.CreateDeploymentAsync(new CreateDeploymentRequest
        {
            AgentId = "agent-1",
            Version = "1.1.0",
            Env = "staging"
        });
        await _store.CreateDeploymentAsync(new CreateDeploymentRequest
        {
            AgentId = "agent-2",
            Version = "1.0.0",
            Env = "prod"
        });

        // Act
        var result = await _store.GetDeploymentsByAgentAsync("agent-1");

        // Assert
        result.Should().HaveCount(2);
        result.All(d => d.AgentId == "agent-1").Should().BeTrue();
    }

    [Fact]
    public async Task GetDeploymentsByAgentAsync_WithNoMatchingAgent_ReturnsEmptyList()
    {
        // Act
        var result = await _store.GetDeploymentsByAgentAsync("non-existent-agent");

        // Assert
        result.Should().BeEmpty();
    }

    #endregion

    #region UpdateDeploymentStatusAsync Tests

    [Fact]
    public async Task UpdateDeploymentStatusAsync_WithExistingDeployment_UpdatesStatus()
    {
        // Arrange
        var deployment = await _store.CreateDeploymentAsync(new CreateDeploymentRequest
        {
            AgentId = "agent-1",
            Version = "1.0.0",
            Env = "dev"
        });

        var updateRequest = new UpdateDeploymentStatusRequest
        {
            Status = new DeploymentStatus
            {
                State = "running",
                ReadyReplicas = 2,
                LastUpdated = DateTime.UtcNow
            }
        };

        // Act
        var result = await _store.UpdateDeploymentStatusAsync(deployment.DepId, updateRequest);

        // Assert
        result.Should().NotBeNull();
        result!.Status.Should().NotBeNull();
        result.Status!.State.Should().Be("running");
        result.Status.ReadyReplicas.Should().Be(2);
        result.Status.LastUpdated.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task UpdateDeploymentStatusAsync_WithNonExistentDeployment_ReturnsNull()
    {
        // Arrange
        var updateRequest = new UpdateDeploymentStatusRequest
        {
            Status = new DeploymentStatus { State = "running" }
        };

        // Act
        var result = await _store.UpdateDeploymentStatusAsync("non-existent-id", updateRequest);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateDeploymentStatusAsync_MultipleTimes_KeepsLatestStatus()
    {
        // Arrange
        var deployment = await _store.CreateDeploymentAsync(new CreateDeploymentRequest
        {
            AgentId = "agent-1",
            Version = "1.0.0",
            Env = "dev"
        });

        // Act - Update status multiple times
        await _store.UpdateDeploymentStatusAsync(deployment.DepId, new UpdateDeploymentStatusRequest
        {
            Status = new DeploymentStatus { State = "deploying", ReadyReplicas = 0 }
        });

        await _store.UpdateDeploymentStatusAsync(deployment.DepId, new UpdateDeploymentStatusRequest
        {
            Status = new DeploymentStatus { State = "running", ReadyReplicas = 1 }
        });

        var result = await _store.UpdateDeploymentStatusAsync(deployment.DepId, new UpdateDeploymentStatusRequest
        {
            Status = new DeploymentStatus { State = "running", ReadyReplicas = 3 }
        });

        // Assert
        result.Should().NotBeNull();
        result!.Status!.State.Should().Be("running");
        result.Status.ReadyReplicas.Should().Be(3);
    }

    #endregion

    #region DeleteDeploymentAsync Tests

    [Fact]
    public async Task DeleteDeploymentAsync_WithExistingDeployment_DeletesAndReturnsTrue()
    {
        // Arrange
        var deployment = await _store.CreateDeploymentAsync(new CreateDeploymentRequest
        {
            AgentId = "agent-1",
            Version = "1.0.0",
            Env = "dev"
        });

        // Act
        var result = await _store.DeleteDeploymentAsync(deployment.DepId);

        // Assert
        result.Should().BeTrue();

        var deletedDeployment = await _store.GetDeploymentAsync(deployment.DepId);
        deletedDeployment.Should().BeNull();
    }

    [Fact]
    public async Task DeleteDeploymentAsync_WithNonExistentDeployment_ReturnsFalse()
    {
        // Act
        var result = await _store.DeleteDeploymentAsync("non-existent-id");

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region Complex Scenarios

    [Fact]
    public async Task CreateDeploymentAsync_WithComplexTarget_SerializesCorrectly()
    {
        // Arrange
        var request = new CreateDeploymentRequest
        {
            AgentId = "agent-1",
            Version = "1.0.0",
            Env = "production",
            Target = new DeploymentTarget
            {
                Replicas = 5,
                Placement = new Dictionary<string, object>
                {
                    ["slotBudget"] = 16,
                    ["resources"] = new Dictionary<string, object>
                    {
                        ["cpu"] = "2000m",
                        ["memory"] = "4Gi"
                    },
                    ["affinity"] = new Dictionary<string, object>
                    {
                        ["region"] = new List<string> { "us-east-1", "us-west-2" }
                    }
                }
            }
        };

        // Act
        var result = await _store.CreateDeploymentAsync(request);

        // Assert
        result.Target.Should().NotBeNull();
        result.Target!.Replicas.Should().Be(5);
        result.Target.Placement.Should().ContainKey("slotBudget");
        result.Target.Placement.Should().ContainKey("resources");
    }

    #endregion
}
