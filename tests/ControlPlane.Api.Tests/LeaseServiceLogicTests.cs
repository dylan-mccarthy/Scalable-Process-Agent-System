using ControlPlane.Api.Services;
using ControlPlane.Api.Models;
using ControlPlane.Api.Grpc;
using Microsoft.Extensions.Logging;
using Moq;

namespace ControlPlane.Api.Tests;

public class LeaseServiceLogicTests
{
    private readonly Mock<IRunStore> _mockRunStore;
    private readonly Mock<INodeStore> _mockNodeStore;
    private readonly Mock<ILeaseStore> _mockLeaseStore;
    private readonly Mock<ILogger<LeaseServiceLogic>> _mockLogger;
    private readonly LeaseServiceLogic _service;

    public LeaseServiceLogicTests()
    {
        _mockRunStore = new Mock<IRunStore>();
        _mockNodeStore = new Mock<INodeStore>();
        _mockLeaseStore = new Mock<ILeaseStore>();
        _mockLogger = new Mock<ILogger<LeaseServiceLogic>>();

        _service = new LeaseServiceLogic(
            _mockRunStore.Object,
            _mockNodeStore.Object,
            _mockLeaseStore.Object,
            _mockLogger.Object
        );
    }

    [Fact]
    public async Task AcknowledgeLeaseAsync_ReturnsTrue()
    {
        // Arrange
        var leaseId = "lease-123";
        var nodeId = "node-1";
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // Act
        var result = await _service.AcknowledgeLeaseAsync(leaseId, nodeId, timestamp);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task CompleteRunAsync_SuccessfullyCompletesRun()
    {
        // Arrange
        var leaseId = "lease-123";
        var runId = "run-456";
        var nodeId = "node-1";
        var run = new Run
        {
            RunId = runId,
            NodeId = nodeId,
            AgentId = "agent-1",
            Version = "1.0.0",
            Status = "assigned"
        };

        _mockRunStore.Setup(s => s.GetRunAsync(runId))
            .ReturnsAsync(run);

        _mockRunStore.Setup(s => s.CompleteRunAsync(runId, It.IsAny<CompleteRunRequest>()))
            .ReturnsAsync(run);

        _mockLeaseStore.Setup(s => s.ReleaseLeaseAsync(runId))
            .ReturnsAsync(true);

        var timings = new TimingInfo
        {
            DurationMs = 1000,
            QueueTimeMs = 100,
            ExecutionTimeMs = 900
        };

        var costs = new CostInfo
        {
            TokensIn = 50,
            TokensOut = 100,
            UsdCost = 0.002
        };

        // Act
        var result = await _service.CompleteRunAsync(leaseId, runId, nodeId, null, timings, costs);

        // Assert
        Assert.True(result);
        _mockRunStore.Verify(s => s.CompleteRunAsync(runId, It.IsAny<CompleteRunRequest>()), Times.Once);
        _mockLeaseStore.Verify(s => s.ReleaseLeaseAsync(runId), Times.Once);
    }

    [Fact]
    public async Task CompleteRunAsync_ReturnsFalse_WhenRunNotFound()
    {
        // Arrange
        var leaseId = "lease-123";
        var runId = "run-456";
        var nodeId = "node-1";

        _mockRunStore.Setup(s => s.GetRunAsync(runId))
            .ReturnsAsync((Run?)null);

        // Act
        var result = await _service.CompleteRunAsync(leaseId, runId, nodeId, null, null, null);

        // Assert
        Assert.False(result);
        _mockRunStore.Verify(s => s.CompleteRunAsync(It.IsAny<string>(), It.IsAny<CompleteRunRequest>()), Times.Never);
    }

    [Fact]
    public async Task CompleteRunAsync_ReturnsFalse_WhenNodeMismatch()
    {
        // Arrange
        var leaseId = "lease-123";
        var runId = "run-456";
        var nodeId = "node-1";
        var run = new Run
        {
            RunId = runId,
            NodeId = "node-2", // Different node
            AgentId = "agent-1",
            Version = "1.0.0",
            Status = "assigned"
        };

        _mockRunStore.Setup(s => s.GetRunAsync(runId))
            .ReturnsAsync(run);

        // Act
        var result = await _service.CompleteRunAsync(leaseId, runId, nodeId, null, null, null);

        // Assert
        Assert.False(result);
        _mockRunStore.Verify(s => s.CompleteRunAsync(It.IsAny<string>(), It.IsAny<CompleteRunRequest>()), Times.Never);
    }

    [Fact]
    public async Task FailRunAsync_SuccessfullyFailsRun()
    {
        // Arrange
        var leaseId = "lease-123";
        var runId = "run-456";
        var nodeId = "node-1";
        var run = new Run
        {
            RunId = runId,
            NodeId = nodeId,
            AgentId = "agent-1",
            Version = "1.0.0",
            Status = "assigned"
        };

        _mockRunStore.Setup(s => s.GetRunAsync(runId))
            .ReturnsAsync(run);

        _mockRunStore.Setup(s => s.FailRunAsync(runId, It.IsAny<FailRunRequest>()))
            .ReturnsAsync(run);

        _mockLeaseStore.Setup(s => s.ReleaseLeaseAsync(runId))
            .ReturnsAsync(true);

        var timings = new TimingInfo
        {
            DurationMs = 500,
            QueueTimeMs = 50,
            ExecutionTimeMs = 450
        };

        // Act
        var (success, shouldRetry) = await _service.FailRunAsync(
            leaseId, runId, nodeId, "Test error", "Details", timings, retryable: true);

        // Assert
        Assert.True(success);
        Assert.True(shouldRetry); // Should retry on first failure
        _mockRunStore.Verify(s => s.FailRunAsync(runId, It.IsAny<FailRunRequest>()), Times.Once);
        _mockLeaseStore.Verify(s => s.ReleaseLeaseAsync(runId), Times.Once);
    }

    [Fact]
    public async Task FailRunAsync_ReturnsFalse_WhenRunNotFound()
    {
        // Arrange
        var leaseId = "lease-123";
        var runId = "run-456";
        var nodeId = "node-1";

        _mockRunStore.Setup(s => s.GetRunAsync(runId))
            .ReturnsAsync((Run?)null);

        // Act
        var (success, shouldRetry) = await _service.FailRunAsync(
            leaseId, runId, nodeId, "Test error", null, null, retryable: true);

        // Assert
        Assert.False(success);
        Assert.False(shouldRetry);
        _mockRunStore.Verify(s => s.FailRunAsync(It.IsAny<string>(), It.IsAny<FailRunRequest>()), Times.Never);
    }

    [Fact]
    public async Task FailRunAsync_DoesNotRetry_WhenNotRetryable()
    {
        // Arrange
        var leaseId = "lease-123";
        var runId = "run-456";
        var nodeId = "node-1";
        var run = new Run
        {
            RunId = runId,
            NodeId = nodeId,
            AgentId = "agent-1",
            Version = "1.0.0",
            Status = "assigned"
        };

        _mockRunStore.Setup(s => s.GetRunAsync(runId))
            .ReturnsAsync(run);

        _mockRunStore.Setup(s => s.FailRunAsync(runId, It.IsAny<FailRunRequest>()))
            .ReturnsAsync(run);

        _mockLeaseStore.Setup(s => s.ReleaseLeaseAsync(runId))
            .ReturnsAsync(true);

        // Act
        var (success, shouldRetry) = await _service.FailRunAsync(
            leaseId, runId, nodeId, "Test error", null, null, retryable: false);

        // Assert
        Assert.True(success);
        Assert.False(shouldRetry); // Should not retry when not retryable
    }

    [Fact]
    public async Task GetLeasesAsync_ReturnsEmpty_WhenNodeNotFound()
    {
        // Arrange
        var nodeId = "node-1";
        var maxLeases = 5;

        _mockNodeStore.Setup(s => s.GetNodeAsync(nodeId))
            .ReturnsAsync((Node?)null);

        using var cts = new CancellationTokenSource();
        cts.Cancel(); // Cancel immediately

        // Act
        var leases = new List<Grpc.Lease>();
        await foreach (var lease in _service.GetLeasesAsync(nodeId, maxLeases, cts.Token))
        {
            leases.Add(lease);
        }

        // Assert
        Assert.Empty(leases);
    }

    [Fact]
    public async Task GetLeasesAsync_StreamsLeases_ForPendingRuns()
    {
        // Arrange
        var nodeId = "node-1";
        var maxLeases = 2;
        var node = new Node
        {
            NodeId = nodeId,
            Status = new NodeStatus { State = "active", AvailableSlots = 5 }
        };

        var pendingRun1 = new Run
        {
            RunId = "run-1",
            AgentId = "agent-1",
            Version = "1.0.0",
            Status = "pending",
            NodeId = null
        };

        var pendingRun2 = new Run
        {
            RunId = "run-2",
            AgentId = "agent-2",
            Version = "1.0.0",
            Status = "pending",
            NodeId = null
        };

        _mockNodeStore.Setup(s => s.GetNodeAsync(nodeId))
            .ReturnsAsync(node);

        // First call returns pending runs, second call returns empty to stop the loop
        var callCount = 0;
        _mockRunStore.Setup(s => s.GetAllRunsAsync())
            .ReturnsAsync(() =>
            {
                callCount++;
                return callCount == 1
                    ? new List<Run> { pendingRun1, pendingRun2 }
                    : new List<Run>();
            });

        _mockLeaseStore.Setup(s => s.AcquireLeaseAsync(It.IsAny<string>(), nodeId, It.IsAny<int>()))
            .ReturnsAsync(true);

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromSeconds(1)); // Cancel after 1 second

        // Act
        var leases = new List<Grpc.Lease>();
        try
        {
            await foreach (var lease in _service.GetLeasesAsync(nodeId, maxLeases, cts.Token))
            {
                leases.Add(lease);
                if (leases.Count >= 2)
                {
                    cts.Cancel(); // Cancel after getting 2 leases
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when cancellation token is triggered
        }

        // Assert
        Assert.True(leases.Count >= 2);
        Assert.Contains(leases, l => l.RunId == "run-1");
        Assert.Contains(leases, l => l.RunId == "run-2");
    }
}
