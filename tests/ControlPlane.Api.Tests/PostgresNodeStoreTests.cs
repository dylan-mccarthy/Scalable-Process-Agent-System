using ControlPlane.Api.Data;
using ControlPlane.Api.Models;
using ControlPlane.Api.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace ControlPlane.Api.Tests;

/// <summary>
/// Unit tests for PostgresNodeStore ensuring node registration and heartbeat tracking works correctly.
/// </summary>
public class PostgresNodeStoreTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly PostgresNodeStore _store;

    public PostgresNodeStoreTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
        _store = new PostgresNodeStore(_context);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    #region RegisterNodeAsync Tests

    [Fact]
    public async Task RegisterNodeAsync_WithValidRequest_RegistersNode()
    {
        // Arrange
        var request = new RegisterNodeRequest
        {
            NodeId = "node-1",
            Metadata = new Dictionary<string, object>
            {
                ["region"] = "us-east-1",
                ["version"] = "1.0.0"
            },
            Capacity = new Dictionary<string, object>
            {
                ["slots"] = 8,
                ["memory"] = "16GB"
            }
        };

        // Act
        var result = await _store.RegisterNodeAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.NodeId.Should().Be("node-1");
        result.Metadata.Should().NotBeNull();
        result.Metadata!.Should().ContainKey("region");
        result.Metadata.Should().ContainKey("version");
        result.Capacity.Should().NotBeNull();
        result.Capacity!.Should().ContainKey("slots");
        result.Status.Should().NotBeNull();
        result.HeartbeatAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task RegisterNodeAsync_WithMinimalData_RegistersNode()
    {
        // Arrange
        var request = new RegisterNodeRequest
        {
            NodeId = "node-minimal"
        };

        // Act
        var result = await _store.RegisterNodeAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.NodeId.Should().Be("node-minimal");
        result.Metadata.Should().BeNull();
        result.Capacity.Should().BeNull();
    }

    #endregion

    #region GetNodeAsync Tests

    [Fact]
    public async Task GetNodeAsync_WithExistingNode_ReturnsNode()
    {
        // Arrange
        await _store.RegisterNodeAsync(new RegisterNodeRequest { NodeId = "node-1" });

        // Act
        var result = await _store.GetNodeAsync("node-1");

        // Assert
        result.Should().NotBeNull();
        result!.NodeId.Should().Be("node-1");
    }

    [Fact]
    public async Task GetNodeAsync_WithNonExistentNode_ReturnsNull()
    {
        // Act
        var result = await _store.GetNodeAsync("non-existent-node");

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region GetAllNodesAsync Tests

    [Fact]
    public async Task GetAllNodesAsync_WithNoNodes_ReturnsEmptyList()
    {
        // Act
        var result = await _store.GetAllNodesAsync();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAllNodesAsync_WithMultipleNodes_ReturnsAllNodes()
    {
        // Arrange
        await _store.RegisterNodeAsync(new RegisterNodeRequest { NodeId = "node-1" });
        await _store.RegisterNodeAsync(new RegisterNodeRequest { NodeId = "node-2" });
        await _store.RegisterNodeAsync(new RegisterNodeRequest { NodeId = "node-3" });

        // Act
        var result = await _store.GetAllNodesAsync();

        // Assert
        result.Should().HaveCount(3);
        result.Select(n => n.NodeId).Should().Contain(new[] { "node-1", "node-2", "node-3" });
    }

    #endregion

    #region UpdateHeartbeatAsync Tests

    [Fact]
    public async Task UpdateHeartbeatAsync_WithExistingNode_UpdatesHeartbeatAndStatus()
    {
        // Arrange
        var node = await _store.RegisterNodeAsync(new RegisterNodeRequest { NodeId = "node-1" });
        var originalHeartbeat = node.HeartbeatAt;

        await Task.Delay(100); // Ensure time difference

        var heartbeatRequest = new HeartbeatRequest
        {
            Status = new NodeStatus
            {
                State = "active",
                ActiveRuns = 3,
                AvailableSlots = 5
            }
        };

        // Act
        var result = await _store.UpdateHeartbeatAsync("node-1", heartbeatRequest);

        // Assert
        result.Should().NotBeNull();
        result!.Status.State.Should().Be("active");
        result.Status.ActiveRuns.Should().Be(3);
        result.Status.AvailableSlots.Should().Be(5);
        result.HeartbeatAt.Should().BeAfter(originalHeartbeat);
    }

    [Fact]
    public async Task UpdateHeartbeatAsync_WithNonExistentNode_ReturnsNull()
    {
        // Arrange
        var heartbeatRequest = new HeartbeatRequest
        {
            Status = new NodeStatus { State = "active" }
        };

        // Act
        var result = await _store.UpdateHeartbeatAsync("non-existent-node", heartbeatRequest);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateHeartbeatAsync_MultipleTimes_KeepsLatestHeartbeat()
    {
        // Arrange
        await _store.RegisterNodeAsync(new RegisterNodeRequest { NodeId = "node-1" });

        // Act - Multiple heartbeats
        await _store.UpdateHeartbeatAsync("node-1", new HeartbeatRequest
        {
            Status = new NodeStatus { ActiveRuns = 1 }
        });
        await Task.Delay(50);

        await _store.UpdateHeartbeatAsync("node-1", new HeartbeatRequest
        {
            Status = new NodeStatus { ActiveRuns = 2 }
        });
        await Task.Delay(50);

        var result = await _store.UpdateHeartbeatAsync("node-1", new HeartbeatRequest
        {
            Status = new NodeStatus { ActiveRuns = 3 }
        });

        // Assert
        result.Should().NotBeNull();
        result!.Status.ActiveRuns.Should().Be(3);
    }

    #endregion

    #region DeleteNodeAsync Tests

    [Fact]
    public async Task DeleteNodeAsync_WithExistingNode_DeletesAndReturnsTrue()
    {
        // Arrange
        await _store.RegisterNodeAsync(new RegisterNodeRequest { NodeId = "node-1" });

        // Act
        var result = await _store.DeleteNodeAsync("node-1");

        // Assert
        result.Should().BeTrue();

        var deletedNode = await _store.GetNodeAsync("node-1");
        deletedNode.Should().BeNull();
    }

    [Fact]
    public async Task DeleteNodeAsync_WithNonExistentNode_ReturnsFalse()
    {
        // Act
        var result = await _store.DeleteNodeAsync("non-existent-node");

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region Serialization Tests

    [Fact]
    public async Task RegisterNodeAsync_WithComplexMetadata_SerializesAndDeserializesCorrectly()
    {
        // Arrange
        var request = new RegisterNodeRequest
        {
            NodeId = "node-complex",
            Metadata = new Dictionary<string, object>
            {
                ["region"] = "us-east-1",
                ["az"] = "us-east-1a",
                ["instance_type"] = "m5.large",
                ["tags"] = new Dictionary<string, object>
                {
                    ["environment"] = "production",
                    ["team"] = "platform"
                }
            }
        };

        // Act
        var result = await _store.RegisterNodeAsync(request);

        // Assert - Deep equality check for nested dictionaries
        result.Metadata.Should().ContainKey("region");
        result.Metadata!["region"].ToString().Should().Be("us-east-1");
    }

    [Fact]
    public async Task UpdateHeartbeatAsync_WithComplexStatus_SerializesCorrectly()
    {
        // Arrange
        await _store.RegisterNodeAsync(new RegisterNodeRequest { NodeId = "node-1" });

        var heartbeatRequest = new HeartbeatRequest
        {
            Status = new NodeStatus
            {
                State = "active",
                ActiveRuns = 5,
                AvailableSlots = 3
            }
        };

        // Act
        var result = await _store.UpdateHeartbeatAsync("node-1", heartbeatRequest);

        // Assert
        result.Should().NotBeNull();
        result!.Status.State.Should().Be("active");
        result.Status.ActiveRuns.Should().Be(5);
    }

    #endregion
}
