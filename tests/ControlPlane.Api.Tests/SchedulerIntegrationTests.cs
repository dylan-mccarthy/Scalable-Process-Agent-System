using ControlPlane.Api.Services;
using ControlPlane.Api.Models;
using Microsoft.Extensions.Logging;
using Moq;

namespace ControlPlane.Api.Tests;

/// <summary>
/// Integration tests for scheduler and lease service interaction
/// </summary>
public class SchedulerIntegrationTests
{
    [Fact]
    public async Task LeaseService_UsesScheduler_ToSelectAppropriateNode()
    {
        // Arrange
        var nodeStore = new InMemoryNodeStore();
        var runStore = new InMemoryRunStore();
        var leaseStore = new Mock<ILeaseStore>();
        var scheduler = new LeastLoadedScheduler(
            nodeStore,
            runStore,
            Mock.Of<ILogger<LeastLoadedScheduler>>());
        var leaseService = new LeaseServiceLogic(
            runStore,
            nodeStore,
            leaseStore.Object,
            Mock.Of<ILogger<LeaseServiceLogic>>(),
            scheduler);

        // Setup nodes with different loads
        var node1 = new RegisterNodeRequest
        {
            NodeId = "node-1",
            Capacity = new Dictionary<string, object> { ["slots"] = 4 },
            Metadata = new Dictionary<string, object> { ["region"] = "us-east-1" }
        };
        var node2 = new RegisterNodeRequest
        {
            NodeId = "node-2",
            Capacity = new Dictionary<string, object> { ["slots"] = 4 },
            Metadata = new Dictionary<string, object> { ["region"] = "us-east-1" }
        };

        await nodeStore.RegisterNodeAsync(node1);
        await nodeStore.RegisterNodeAsync(node2);

        // Update node statuses - node-1 has higher load
        var n1 = await nodeStore.GetNodeAsync("node-1");
        n1!.Status.ActiveRuns = 3;
        n1.Status.AvailableSlots = 1;

        var n2 = await nodeStore.GetNodeAsync("node-2");
        n2!.Status.ActiveRuns = 1;
        n2.Status.AvailableSlots = 3;

        // Add runs to match the node status
        var run1 = await runStore.CreateRunAsync("agent-1", "1.0.0");
        run1.NodeId = "node-1";
        run1.Status = "running";

        var run2 = await runStore.CreateRunAsync("agent-1", "1.0.0");
        run2.NodeId = "node-1";
        run2.Status = "running";

        var run3 = await runStore.CreateRunAsync("agent-1", "1.0.0");
        run3.NodeId = "node-1";
        run3.Status = "assigned";

        var run4 = await runStore.CreateRunAsync("agent-1", "1.0.0");
        run4.NodeId = "node-2";
        run4.Status = "running";

        // Create a pending run
        var pendingRun = await runStore.CreateRunAsync("agent-1", "1.0.0");
        pendingRun.Status = "pending";

        // Setup lease store to succeed for node-2 with the pending run's ID
        leaseStore.Setup(s => s.AcquireLeaseAsync(pendingRun.RunId, "node-2", It.IsAny<int>()))
            .ReturnsAsync(true);

        // Act - Node-2 requests a lease
        var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromSeconds(1));

        const int expectedLeaseCount = 1;
        var leases = new List<ControlPlane.Api.Grpc.Lease>();
        await foreach (var lease in leaseService.GetLeasesAsync("node-2", 5, cts.Token))
        {
            leases.Add(lease);
            if (leases.Count >= expectedLeaseCount)
            {
                cts.Cancel(); // Got the lease we wanted
            }
        }

        // Assert
        Assert.Single(leases);
        Assert.Equal(pendingRun.RunId, leases[0].RunId);
        leaseStore.Verify(s => s.AcquireLeaseAsync(pendingRun.RunId, "node-2", It.IsAny<int>()), Times.Once);
    }

    [Fact]
    public async Task Scheduler_SelectsNodeMatchingRegionConstraint()
    {
        // Arrange
        var nodeStore = new InMemoryNodeStore();
        var runStore = new InMemoryRunStore();
        var scheduler = new LeastLoadedScheduler(
            nodeStore,
            runStore,
            Mock.Of<ILogger<LeastLoadedScheduler>>());

        // Create nodes in different regions
        await nodeStore.RegisterNodeAsync(new RegisterNodeRequest
        {
            NodeId = "node-us",
            Capacity = new Dictionary<string, object> { ["slots"] = 4 },
            Metadata = new Dictionary<string, object> { ["region"] = "us-east-1" }
        });
        await nodeStore.RegisterNodeAsync(new RegisterNodeRequest
        {
            NodeId = "node-eu",
            Capacity = new Dictionary<string, object> { ["slots"] = 4 },
            Metadata = new Dictionary<string, object> { ["region"] = "eu-west-1" }
        });

        // Update statuses to show availability
        var nodeUs = await nodeStore.GetNodeAsync("node-us");
        nodeUs!.Status.AvailableSlots = 4;

        var nodeEu = await nodeStore.GetNodeAsync("node-eu");
        nodeEu!.Status.AvailableSlots = 4;

        var run = new Run
        {
            RunId = "test-run",
            AgentId = "agent-1",
            Status = "pending"
        };

        // Act - Schedule with region constraint
        var constraints = new Dictionary<string, object>
        {
            ["region"] = "eu-west-1"
        };
        var selectedNode = await scheduler.ScheduleRunAsync(run, constraints);

        // Assert
        Assert.Equal("node-eu", selectedNode);
    }

    [Fact]
    public async Task Scheduler_ReturnsLoadInformation()
    {
        // Arrange
        var nodeStore = new InMemoryNodeStore();
        var runStore = new InMemoryRunStore();
        var scheduler = new LeastLoadedScheduler(
            nodeStore,
            runStore,
            Mock.Of<ILogger<LeastLoadedScheduler>>());

        // Create a node with known capacity
        await nodeStore.RegisterNodeAsync(new RegisterNodeRequest
        {
            NodeId = "test-node",
            Capacity = new Dictionary<string, object> { ["slots"] = 8 },
            Metadata = new Dictionary<string, object> { ["region"] = "us-east-1" }
        });

        // Update status
        var node = await nodeStore.GetNodeAsync("test-node");
        node!.Status.ActiveRuns = 3;
        node.Status.AvailableSlots = 5;

        // Add some runs
        var run1 = await runStore.CreateRunAsync("agent-1", "1.0.0");
        run1.NodeId = "test-node";
        run1.Status = "running";

        var run2 = await runStore.CreateRunAsync("agent-1", "1.0.0");
        run2.NodeId = "test-node";
        run2.Status = "running";

        var run3 = await runStore.CreateRunAsync("agent-1", "1.0.0");
        run3.NodeId = "test-node";
        run3.Status = "assigned";

        // Act
        var loadInfo = await scheduler.GetNodeLoadAsync();

        // Assert
        Assert.Contains("test-node", loadInfo.Keys);
        var nodeLoad = loadInfo["test-node"];
        Assert.Equal(8, nodeLoad.TotalSlots);
        Assert.Equal(3, nodeLoad.ActiveRuns);
        Assert.Equal(5, nodeLoad.AvailableSlots);
        Assert.Equal(37.5, nodeLoad.LoadPercentage);
        Assert.True(nodeLoad.HasCapacity);
    }
}
