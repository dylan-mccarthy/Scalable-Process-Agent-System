using ControlPlane.Api.Models;
using ControlPlane.Api.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ControlPlane.Api.Tests;

/// <summary>
/// Tests for MetricsService that provides data for observable gauges
/// </summary>
public class MetricsServiceTests
{
    [Fact]
    public async Task GetActiveRunsCountAsync_ShouldReturnZero_WhenNoRuns()
    {
        // Arrange
        var runStore = new InMemoryRunStore();
        var nodeStore = new InMemoryNodeStore();
        var metricsService = new MetricsService(runStore, nodeStore, NullLogger<MetricsService>.Instance);

        // Act
        var count = await metricsService.GetActiveRunsCountAsync();

        // Assert
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task GetActiveRunsCountAsync_ShouldCountRunningAndPendingRuns()
    {
        // Arrange
        var runStore = new InMemoryRunStore();
        var nodeStore = new InMemoryNodeStore();
        var metricsService = new MetricsService(runStore, nodeStore, NullLogger<MetricsService>.Instance);

        // Create runs with different statuses
        var run1 = await runStore.CreateRunAsync("agent1", "1.0.0");
        var run2 = await runStore.CreateRunAsync("agent2", "1.0.0");
        var run3 = await runStore.CreateRunAsync("agent3", "1.0.0");
        
        // Update statuses
        await runStore.CompleteRunAsync(run1.RunId, new CompleteRunRequest
        {
            Result = new Dictionary<string, object>(),
            Timings = new Dictionary<string, object> { ["duration"] = 100 }
        });

        // run2 and run3 remain in pending state

        // Act
        var count = await metricsService.GetActiveRunsCountAsync();

        // Assert
        Assert.Equal(2, count); // Only pending runs
    }

    [Fact]
    public async Task GetActiveNodesCountAsync_ShouldReturnZero_WhenNoNodes()
    {
        // Arrange
        var runStore = new InMemoryRunStore();
        var nodeStore = new InMemoryNodeStore();
        var metricsService = new MetricsService(runStore, nodeStore, NullLogger<MetricsService>.Instance);

        // Act
        var count = await metricsService.GetActiveNodesCountAsync();

        // Assert
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task GetActiveNodesCountAsync_ShouldCountOnlyActiveNodes()
    {
        // Arrange
        var runStore = new InMemoryRunStore();
        var nodeStore = new InMemoryNodeStore();
        var metricsService = new MetricsService(runStore, nodeStore, NullLogger<MetricsService>.Instance);

        // Register nodes
        await nodeStore.RegisterNodeAsync(new RegisterNodeRequest
        {
            NodeId = "node1",
            Metadata = new Dictionary<string, object> { ["region"] = "us-east-1" },
            Capacity = new Dictionary<string, object> { ["slots"] = 8 }
        });

        await nodeStore.RegisterNodeAsync(new RegisterNodeRequest
        {
            NodeId = "node2",
            Metadata = new Dictionary<string, object> { ["region"] = "us-west-1" },
            Capacity = new Dictionary<string, object> { ["slots"] = 4 }
        });

        // Act
        var count = await metricsService.GetActiveNodesCountAsync();

        // Assert
        Assert.Equal(2, count);
    }

    [Fact]
    public async Task GetActiveNodesCountAsync_ShouldNotCountStaleNodes()
    {
        // Arrange
        var runStore = new InMemoryRunStore();
        var nodeStore = new InMemoryNodeStore();
        var metricsService = new MetricsService(runStore, nodeStore, NullLogger<MetricsService>.Instance);

        // Register a node
        var node = await nodeStore.RegisterNodeAsync(new RegisterNodeRequest
        {
            NodeId = "node1",
            Metadata = new Dictionary<string, object> { ["region"] = "us-east-1" },
            Capacity = new Dictionary<string, object> { ["slots"] = 8 }
        });

        // Manually update the heartbeat to be stale (over 60 seconds ago)
        node.HeartbeatAt = DateTime.UtcNow.AddSeconds(-120);

        // Act
        var count = await metricsService.GetActiveNodesCountAsync();

        // Assert
        Assert.Equal(0, count); // Node should not be counted as active
    }

    [Fact]
    public async Task GetTotalSlotsAsync_ShouldReturnZero_WhenNoNodes()
    {
        // Arrange
        var runStore = new InMemoryRunStore();
        var nodeStore = new InMemoryNodeStore();
        var metricsService = new MetricsService(runStore, nodeStore, NullLogger<MetricsService>.Instance);

        // Act
        var slots = await metricsService.GetTotalSlotsAsync();

        // Assert
        Assert.Equal(0, slots);
    }

    [Fact]
    public async Task GetTotalSlotsAsync_ShouldSumAllActiveNodeSlots()
    {
        // Arrange
        var runStore = new InMemoryRunStore();
        var nodeStore = new InMemoryNodeStore();
        var metricsService = new MetricsService(runStore, nodeStore, NullLogger<MetricsService>.Instance);

        // Register nodes with different slot counts
        await nodeStore.RegisterNodeAsync(new RegisterNodeRequest
        {
            NodeId = "node1",
            Metadata = new Dictionary<string, object> { ["region"] = "us-east-1" },
            Capacity = new Dictionary<string, object> { ["slots"] = 8 }
        });

        await nodeStore.RegisterNodeAsync(new RegisterNodeRequest
        {
            NodeId = "node2",
            Metadata = new Dictionary<string, object> { ["region"] = "us-west-1" },
            Capacity = new Dictionary<string, object> { ["slots"] = 4 }
        });

        await nodeStore.RegisterNodeAsync(new RegisterNodeRequest
        {
            NodeId = "node3",
            Metadata = new Dictionary<string, object> { ["region"] = "eu-west-1" },
            Capacity = new Dictionary<string, object> { ["slots"] = 16 }
        });

        // Act
        var totalSlots = await metricsService.GetTotalSlotsAsync();

        // Assert
        Assert.Equal(28, totalSlots); // 8 + 4 + 16
    }

    [Fact]
    public async Task GetUsedSlotsAsync_ShouldReturnZero_WhenNoActiveRuns()
    {
        // Arrange
        var runStore = new InMemoryRunStore();
        var nodeStore = new InMemoryNodeStore();
        var metricsService = new MetricsService(runStore, nodeStore, NullLogger<MetricsService>.Instance);

        await nodeStore.RegisterNodeAsync(new RegisterNodeRequest
        {
            NodeId = "node1",
            Metadata = new Dictionary<string, object> { ["region"] = "us-east-1" },
            Capacity = new Dictionary<string, object> { ["slots"] = 8 }
        });

        // Act
        var usedSlots = await metricsService.GetUsedSlotsAsync();

        // Assert
        Assert.Equal(0, usedSlots);
    }

    [Fact]
    public async Task GetUsedSlotsAsync_ShouldSumActiveRunsFromAllNodes()
    {
        // Arrange
        var runStore = new InMemoryRunStore();
        var nodeStore = new InMemoryNodeStore();
        var metricsService = new MetricsService(runStore, nodeStore, NullLogger<MetricsService>.Instance);

        // Register nodes
        await nodeStore.RegisterNodeAsync(new RegisterNodeRequest
        {
            NodeId = "node1",
            Metadata = new Dictionary<string, object> { ["region"] = "us-east-1" },
            Capacity = new Dictionary<string, object> { ["slots"] = 8 }
        });

        await nodeStore.RegisterNodeAsync(new RegisterNodeRequest
        {
            NodeId = "node2",
            Metadata = new Dictionary<string, object> { ["region"] = "us-west-1" },
            Capacity = new Dictionary<string, object> { ["slots"] = 4 }
        });

        // Update nodes with active runs
        await nodeStore.UpdateHeartbeatAsync("node1", new HeartbeatRequest
        {
            Status = new NodeStatus
            {
                State = "active",
                ActiveRuns = 3,
                AvailableSlots = 5
            }
        });

        await nodeStore.UpdateHeartbeatAsync("node2", new HeartbeatRequest
        {
            Status = new NodeStatus
            {
                State = "active",
                ActiveRuns = 2,
                AvailableSlots = 2
            }
        });

        // Act
        var usedSlots = await metricsService.GetUsedSlotsAsync();

        // Assert
        Assert.Equal(5, usedSlots); // 3 + 2
    }

    [Fact]
    public async Task GetAvailableSlotsAsync_ShouldReturnTotalSlots_WhenNoActiveRuns()
    {
        // Arrange
        var runStore = new InMemoryRunStore();
        var nodeStore = new InMemoryNodeStore();
        var metricsService = new MetricsService(runStore, nodeStore, NullLogger<MetricsService>.Instance);

        await nodeStore.RegisterNodeAsync(new RegisterNodeRequest
        {
            NodeId = "node1",
            Metadata = new Dictionary<string, object> { ["region"] = "us-east-1" },
            Capacity = new Dictionary<string, object> { ["slots"] = 8 }
        });

        // Act
        var availableSlots = await metricsService.GetAvailableSlotsAsync();

        // Assert
        Assert.Equal(8, availableSlots);
    }

    [Fact]
    public async Task GetAvailableSlotsAsync_ShouldSumAvailableSlotsFromAllNodes()
    {
        // Arrange
        var runStore = new InMemoryRunStore();
        var nodeStore = new InMemoryNodeStore();
        var metricsService = new MetricsService(runStore, nodeStore, NullLogger<MetricsService>.Instance);

        // Register nodes
        await nodeStore.RegisterNodeAsync(new RegisterNodeRequest
        {
            NodeId = "node1",
            Metadata = new Dictionary<string, object> { ["region"] = "us-east-1" },
            Capacity = new Dictionary<string, object> { ["slots"] = 8 }
        });

        await nodeStore.RegisterNodeAsync(new RegisterNodeRequest
        {
            NodeId = "node2",
            Metadata = new Dictionary<string, object> { ["region"] = "us-west-1" },
            Capacity = new Dictionary<string, object> { ["slots"] = 4 }
        });

        // Update nodes with available slots
        await nodeStore.UpdateHeartbeatAsync("node1", new HeartbeatRequest
        {
            Status = new NodeStatus
            {
                State = "active",
                ActiveRuns = 3,
                AvailableSlots = 5
            }
        });

        await nodeStore.UpdateHeartbeatAsync("node2", new HeartbeatRequest
        {
            Status = new NodeStatus
            {
                State = "active",
                ActiveRuns = 2,
                AvailableSlots = 2
            }
        });

        // Act
        var availableSlots = await metricsService.GetAvailableSlotsAsync();

        // Assert
        Assert.Equal(7, availableSlots); // 5 + 2
    }

    [Fact]
    public async Task GetTotalSlotsAsync_ShouldHandleNodesWithoutSlotCapacity()
    {
        // Arrange
        var runStore = new InMemoryRunStore();
        var nodeStore = new InMemoryNodeStore();
        var metricsService = new MetricsService(runStore, nodeStore, NullLogger<MetricsService>.Instance);

        // Register node without slots in capacity
        await nodeStore.RegisterNodeAsync(new RegisterNodeRequest
        {
            NodeId = "node1",
            Metadata = new Dictionary<string, object> { ["region"] = "us-east-1" },
            Capacity = new Dictionary<string, object> { ["cpu"] = "4" } // No slots field
        });

        // Act
        var totalSlots = await metricsService.GetTotalSlotsAsync();

        // Assert
        Assert.Equal(0, totalSlots);
    }

    [Fact]
    public async Task MetricsService_ShouldHandleExceptions_AndReturnZero()
    {
        // Arrange - Create a null store to force exceptions
        var metricsService = new MetricsService(null!, null!, NullLogger<MetricsService>.Instance);

        // Act & Assert - Should not throw, should return 0
        Assert.Equal(0, await metricsService.GetActiveRunsCountAsync());
        Assert.Equal(0, await metricsService.GetActiveNodesCountAsync());
        Assert.Equal(0, await metricsService.GetTotalSlotsAsync());
        Assert.Equal(0, await metricsService.GetUsedSlotsAsync());
        Assert.Equal(0, await metricsService.GetAvailableSlotsAsync());
    }
}
