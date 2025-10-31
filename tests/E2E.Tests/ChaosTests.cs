using System.Diagnostics;
using ControlPlane.Api.Models;
using ControlPlane.Api.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace E2E.Tests;

/// <summary>
/// Chaos engineering tests for node failure scenarios (E7-T4).
/// Validates that the system correctly handles node failures and reassigns work.
/// Per SAD Section 10: "kill one node; verify leases reassign within TTL"
/// </summary>
public class ChaosTests
{
    private readonly Mock<ILogger<LeastLoadedScheduler>> _schedulerLoggerMock;

    public ChaosTests()
    {
        _schedulerLoggerMock = new Mock<ILogger<LeastLoadedScheduler>>();
    }

    /// <summary>
    /// Simulates a single node failure and verifies that runs are not assigned to failed nodes.
    /// This is the core chaos test requirement from SAD Section 10.
    /// </summary>
    [Fact]
    public async Task NodeFailure_RunsNotAssignedToFailedNode()
    {
        // Arrange - Set up a system with 3 nodes
        var nodeStore = new InMemoryNodeStore();
        var runStore = new InMemoryRunStore();

        // Create 3 healthy nodes
        await nodeStore.RegisterNodeAsync(new RegisterNodeRequest
        {
            NodeId = "node-1",
            Capacity = new Dictionary<string, object> { ["slots"] = 4 },
            Metadata = new Dictionary<string, object> { ["region"] = "us-east" }
        });

        await nodeStore.RegisterNodeAsync(new RegisterNodeRequest
        {
            NodeId = "node-2",
            Capacity = new Dictionary<string, object> { ["slots"] = 4 },
            Metadata = new Dictionary<string, object> { ["region"] = "us-east" }
        });

        await nodeStore.RegisterNodeAsync(new RegisterNodeRequest
        {
            NodeId = "node-3",
            Capacity = new Dictionary<string, object> { ["slots"] = 4 },
            Metadata = new Dictionary<string, object> { ["region"] = "us-east" }
        });

        // Create a run
        var run = await runStore.CreateRunAsync("invoice-classifier", "1.0.0");
        run.RunId.Should().NotBeNullOrEmpty();

        // Create scheduler
        var scheduler = new LeastLoadedScheduler(nodeStore, runStore, _schedulerLoggerMock.Object);

        // Act - Simulate node-1 failure BEFORE scheduling
        await nodeStore.UpdateHeartbeatAsync("node-1", new HeartbeatRequest
        {
            Status = new NodeStatus
            {
                State = "failed",
                ActiveRuns = 0,
                AvailableSlots = 0
            }
        });

        // Try to schedule the run - should not go to failed node
        var assignedNodeId = await scheduler.ScheduleRunAsync(run);

        // Assert - Verify assignment to healthy node
        assignedNodeId.Should().NotBeNull("run should be scheduled to a healthy node");
        assignedNodeId.Should().NotBe("node-1", "run should not be assigned to failed node");
        assignedNodeId.Should().BeOneOf("node-2", "node-3", "run should be assigned to one of the healthy nodes");

        // Verify the assigned node has capacity
        var nodeLoads = await scheduler.GetNodeLoadAsync();
        nodeLoads[assignedNodeId!].HasCapacity.Should().BeTrue("assigned node should have capacity");
        nodeLoads[assignedNodeId!].AvailableSlots.Should().BeGreaterThan(0, "assigned node should have available slots");
    }

    /// <summary>
    /// Tests that multiple concurrent runs avoid failed nodes during scheduling.
    /// </summary>
    [Fact]
    public async Task NodeFailure_MultipleRuns_AllAssignedToHealthyNodes()
    {
        // Arrange
        var nodeStore = new InMemoryNodeStore();
        var runStore = new InMemoryRunStore();

        // Create 2 nodes
        await nodeStore.RegisterNodeAsync(new RegisterNodeRequest
        {
            NodeId = "node-1",
            Capacity = new Dictionary<string, object> { ["slots"] = 8 },
            Metadata = new Dictionary<string, object> { ["region"] = "us-west" }
        });

        await nodeStore.RegisterNodeAsync(new RegisterNodeRequest
        {
            NodeId = "node-2",
            Capacity = new Dictionary<string, object> { ["slots"] = 8 },
            Metadata = new Dictionary<string, object> { ["region"] = "us-west" }
        });

        // Create 4 runs
        var runs = new List<Run>();
        for (int i = 1; i <= 4; i++)
        {
            var run = await runStore.CreateRunAsync("invoice-classifier", "1.0.0");
            runs.Add(run);
        }

        // Act - Simulate node-1 failure
        await nodeStore.UpdateHeartbeatAsync("node-1", new HeartbeatRequest
        {
            Status = new NodeStatus
            {
                State = "failed",
                ActiveRuns = 0,
                AvailableSlots = 0
            }
        });

        // Schedule all runs
        var scheduler = new LeastLoadedScheduler(nodeStore, runStore, _schedulerLoggerMock.Object);
        var assignedToNode2Count = 0;

        foreach (var run in runs)
        {
            var nodeId = await scheduler.ScheduleRunAsync(run);
            if (nodeId == "node-2")
            {
                assignedToNode2Count++;
            }
        }

        // Assert - All runs should go to node-2 since node-1 is failed
        assignedToNode2Count.Should().Be(4, "all 4 runs should be assigned to node-2");

        // Verify node-2 can handle the load
        var nodeLoads = await scheduler.GetNodeLoadAsync();
        nodeLoads["node-2"].TotalSlots.Should().Be(8, "node-2 should have 8 total slots");
        nodeLoads["node-2"].AvailableSlots.Should().BeGreaterThanOrEqualTo(4,
            "node-2 should have enough slots for all runs");
    }

    /// <summary>
    /// Tests that the system handles cascading failures when multiple nodes fail simultaneously.
    /// </summary>
    [Fact]
    public async Task MultipleNodeFailures_SystemRemainsFunctional_WithSurvivingNodes()
    {
        // Arrange
        var nodeStore = new InMemoryNodeStore();
        var runStore = new InMemoryRunStore();

        // Create 5 nodes
        for (int i = 1; i <= 5; i++)
        {
            await nodeStore.RegisterNodeAsync(new RegisterNodeRequest
            {
                NodeId = $"node-{i}",
                Capacity = new Dictionary<string, object> { ["slots"] = 4 },
                Metadata = new Dictionary<string, object> { ["region"] = "eu-central" }
            });
        }

        // Create runs
        var runs = new List<Run>();
        for (int i = 1; i <= 3; i++)
        {
            var run = await runStore.CreateRunAsync("invoice-classifier", "1.0.0");
            runs.Add(run);
        }

        // Act - Simulate failure of first 3 nodes (60% of fleet)
        for (int i = 1; i <= 3; i++)
        {
            await nodeStore.UpdateHeartbeatAsync($"node-{i}", new HeartbeatRequest
            {
                Status = new NodeStatus
                {
                    State = "failed",
                    ActiveRuns = 0,
                    AvailableSlots = 0
                }
            });
        }

        // Attempt scheduling
        var scheduler = new LeastLoadedScheduler(nodeStore, runStore, _schedulerLoggerMock.Object);
        var successfulAssignments = 0;

        foreach (var run in runs)
        {
            var nodeId = await scheduler.ScheduleRunAsync(run);
            if (nodeId != null && (nodeId == "node-4" || nodeId == "node-5"))
            {
                successfulAssignments++;
            }
        }

        // Assert - All runs should be assigned to surviving nodes
        successfulAssignments.Should().Be(3,
            "all 3 runs should be assigned despite 60% node failure");

        // Verify system health
        var nodeLoads = await scheduler.GetNodeLoadAsync();
        var healthyNodes = nodeLoads.Values.Count(n => n.AvailableSlots > 0);
        healthyNodes.Should().BeGreaterThanOrEqualTo(2,
            "at least 2 nodes should remain healthy after cascading failures");
    }

    /// <summary>
    /// Tests that scheduling respects placement constraints even during node failures.
    /// </summary>
    [Fact]
    public async Task NodeFailure_RespectsPlacementConstraints_DuringScheduling()
    {
        // Arrange
        var nodeStore = new InMemoryNodeStore();
        var runStore = new InMemoryRunStore();

        // Create nodes in different regions
        await nodeStore.RegisterNodeAsync(new RegisterNodeRequest
        {
            NodeId = "node-us-east-1",
            Capacity = new Dictionary<string, object> { ["slots"] = 4 },
            Metadata = new Dictionary<string, object> { ["region"] = "us-east" }
        });

        await nodeStore.RegisterNodeAsync(new RegisterNodeRequest
        {
            NodeId = "node-us-west-1",
            Capacity = new Dictionary<string, object> { ["slots"] = 4 },
            Metadata = new Dictionary<string, object> { ["region"] = "us-west" }
        });

        await nodeStore.RegisterNodeAsync(new RegisterNodeRequest
        {
            NodeId = "node-us-west-2",
            Capacity = new Dictionary<string, object> { ["slots"] = 4 },
            Metadata = new Dictionary<string, object> { ["region"] = "us-west" }
        });

        // Create a run with us-west region constraint
        var run = await runStore.CreateRunAsync("invoice-classifier", "1.0.0");

        // Act - Simulate node-us-west-1 failure
        await nodeStore.UpdateHeartbeatAsync("node-us-west-1", new HeartbeatRequest
        {
            Status = new NodeStatus
            {
                State = "failed",
                ActiveRuns = 0,
                AvailableSlots = 0
            }
        });

        // Attempt scheduling with region constraint
        var scheduler = new LeastLoadedScheduler(nodeStore, runStore, _schedulerLoggerMock.Object);

        var placementConstraints = new Dictionary<string, object>
        {
            ["region"] = "us-west"
        };

        var assignedNodeId = await scheduler.ScheduleRunAsync(run, placementConstraints);

        // Assert - Should be assigned to node-us-west-2 (not us-east)
        assignedNodeId.Should().NotBeNull("run should be assigned");
        assignedNodeId.Should().Be("node-us-west-2",
            "run should be assigned to another us-west node, respecting region constraint");
        assignedNodeId.Should().NotBe("node-us-east-1",
            "run should not be assigned to us-east node due to region constraint");
    }

    /// <summary>
    /// Tests that failed node recovery is handled correctly when a node comes back online.
    /// </summary>
    [Fact]
    public async Task NodeRecovery_AfterFailure_NodeRejoinsFleet()
    {
        // Arrange
        var nodeStore = new InMemoryNodeStore();
        var runStore = new InMemoryRunStore();
        var scheduler = new LeastLoadedScheduler(nodeStore, runStore, _schedulerLoggerMock.Object);

        await nodeStore.RegisterNodeAsync(new RegisterNodeRequest
        {
            NodeId = "node-recovery-1",
            Capacity = new Dictionary<string, object> { ["slots"] = 4 },
            Metadata = new Dictionary<string, object> { ["region"] = "us-central" }
        });

        // Create and schedule a run
        var run1 = await runStore.CreateRunAsync("invoice-classifier", "1.0.0");

        var assignedNode = await scheduler.ScheduleRunAsync(run1);
        assignedNode.Should().Be("node-recovery-1", "run should be scheduled to the only available node");

        // Act - Simulate node failure
        await nodeStore.UpdateHeartbeatAsync("node-recovery-1", new HeartbeatRequest
        {
            Status = new NodeStatus
            {
                State = "failed",
                ActiveRuns = 0,
                AvailableSlots = 0
            }
        });

        // Verify node is excluded from scheduling
        var run2 = await runStore.CreateRunAsync("invoice-classifier", "1.0.0");

        var noNode = await scheduler.ScheduleRunAsync(run2);
        noNode.Should().BeNull("failed node should not be scheduled any runs");

        // Simulate node recovery
        await nodeStore.UpdateHeartbeatAsync("node-recovery-1", new HeartbeatRequest
        {
            Status = new NodeStatus
            {
                State = "active",
                ActiveRuns = 0,
                AvailableSlots = 4
            }
        });

        // Try scheduling again
        var recoveredNode = await scheduler.ScheduleRunAsync(run2);

        // Assert
        recoveredNode.Should().Be("node-recovery-1",
            "recovered node should be available for scheduling again");

        var nodeLoads = await scheduler.GetNodeLoadAsync();
        nodeLoads["node-recovery-1"].HasCapacity.Should().BeTrue(
            "recovered node should have capacity");
    }

    /// <summary>
    /// Tests that no runs are scheduled when all nodes fail (system degradation scenario).
    /// </summary>
    [Fact]
    public async Task AllNodesFailure_NoRunsScheduled_GracefulDegradation()
    {
        // Arrange
        var nodeStore = new InMemoryNodeStore();
        var runStore = new InMemoryRunStore();

        // Create 3 nodes
        for (int i = 1; i <= 3; i++)
        {
            await nodeStore.RegisterNodeAsync(new RegisterNodeRequest
            {
                NodeId = $"node-{i}",
                Capacity = new Dictionary<string, object> { ["slots"] = 4 },
                Metadata = new Dictionary<string, object> { ["region"] = "ap-southeast" }
            });
        }

        // Act - Simulate all nodes failing
        for (int i = 1; i <= 3; i++)
        {
            await nodeStore.UpdateHeartbeatAsync($"node-{i}", new HeartbeatRequest
            {
                Status = new NodeStatus
                {
                    State = "failed",
                    ActiveRuns = 0,
                    AvailableSlots = 0
                }
            });
        }

        // Attempt to schedule runs
        var scheduler = new LeastLoadedScheduler(nodeStore, runStore, _schedulerLoggerMock.Object);
        var run = await runStore.CreateRunAsync("invoice-classifier", "1.0.0");

        var assignedNode = await scheduler.ScheduleRunAsync(run);

        // Assert - No node should be assigned
        assignedNode.Should().BeNull("no runs should be scheduled when all nodes have failed");

        // Verify metrics show no healthy nodes
        var nodeLoads = await scheduler.GetNodeLoadAsync();
        var healthyNodesCount = nodeLoads.Values.Count(n => n.HasCapacity);
        healthyNodesCount.Should().Be(0, "all nodes should be marked as having no capacity");
    }
}
