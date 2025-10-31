using ControlPlane.Api.Services;
using ControlPlane.Api.Models;
using Microsoft.Extensions.Logging;
using Moq;

namespace ControlPlane.Api.Tests;

public class LeastLoadedSchedulerTests
{
    private readonly Mock<INodeStore> _mockNodeStore;
    private readonly Mock<IRunStore> _mockRunStore;
    private readonly Mock<ILogger<LeastLoadedScheduler>> _mockLogger;
    private readonly LeastLoadedScheduler _scheduler;

    public LeastLoadedSchedulerTests()
    {
        _mockNodeStore = new Mock<INodeStore>();
        _mockRunStore = new Mock<IRunStore>();
        _mockLogger = new Mock<ILogger<LeastLoadedScheduler>>();

        _scheduler = new LeastLoadedScheduler(
            _mockNodeStore.Object,
            _mockRunStore.Object,
            _mockLogger.Object
        );
    }

    [Fact]
    public async Task ScheduleRunAsync_ReturnsNull_WhenNoActiveNodes()
    {
        // Arrange
        _mockNodeStore.Setup(s => s.GetAllNodesAsync())
            .ReturnsAsync(new List<Node>());

        var run = new Run { RunId = "run-1", AgentId = "agent-1" };

        // Act
        var result = await _scheduler.ScheduleRunAsync(run);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task ScheduleRunAsync_ReturnsNull_WhenNoNodesHaveCapacity()
    {
        // Arrange
        var nodes = new List<Node>
        {
            new Node
            {
                NodeId = "node-1",
                Capacity = new Dictionary<string, object> { ["slots"] = 2 },
                Status = new NodeStatus
                {
                    State = "active",
                    ActiveRuns = 2,
                    AvailableSlots = 0
                }
            }
        };

        var runs = new List<Run>
        {
            new Run { RunId = "run-1", NodeId = "node-1", Status = "running" },
            new Run { RunId = "run-2", NodeId = "node-1", Status = "running" }
        };

        _mockNodeStore.Setup(s => s.GetAllNodesAsync()).ReturnsAsync(nodes);
        _mockRunStore.Setup(s => s.GetAllRunsAsync()).ReturnsAsync(runs);

        var run = new Run { RunId = "run-3", AgentId = "agent-1" };

        // Act
        var result = await _scheduler.ScheduleRunAsync(run);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task ScheduleRunAsync_SelectsLeastLoadedNode()
    {
        // Arrange
        var nodes = new List<Node>
        {
            new Node
            {
                NodeId = "node-1",
                Capacity = new Dictionary<string, object> { ["slots"] = 4 },
                Status = new NodeStatus
                {
                    State = "active",
                    ActiveRuns = 3,
                    AvailableSlots = 1
                }
            },
            new Node
            {
                NodeId = "node-2",
                Capacity = new Dictionary<string, object> { ["slots"] = 4 },
                Status = new NodeStatus
                {
                    State = "active",
                    ActiveRuns = 1,
                    AvailableSlots = 3
                }
            }
        };

        var runs = new List<Run>
        {
            new Run { RunId = "run-1", NodeId = "node-1", Status = "running" },
            new Run { RunId = "run-2", NodeId = "node-1", Status = "running" },
            new Run { RunId = "run-3", NodeId = "node-1", Status = "assigned" },
            new Run { RunId = "run-4", NodeId = "node-2", Status = "running" }
        };

        _mockNodeStore.Setup(s => s.GetAllNodesAsync()).ReturnsAsync(nodes);
        _mockRunStore.Setup(s => s.GetAllRunsAsync()).ReturnsAsync(runs);

        var run = new Run { RunId = "run-5", AgentId = "agent-1" };

        // Act
        var result = await _scheduler.ScheduleRunAsync(run);

        // Assert
        Assert.Equal("node-2", result); // node-2 has lower load (25% vs 75%)
    }

    [Fact]
    public async Task ScheduleRunAsync_RespectsRegionConstraint()
    {
        // Arrange
        var nodes = new List<Node>
        {
            new Node
            {
                NodeId = "node-1",
                Metadata = new Dictionary<string, object> { ["region"] = "us-east-1" },
                Capacity = new Dictionary<string, object> { ["slots"] = 4 },
                Status = new NodeStatus
                {
                    State = "active",
                    ActiveRuns = 1,
                    AvailableSlots = 3
                }
            },
            new Node
            {
                NodeId = "node-2",
                Metadata = new Dictionary<string, object> { ["region"] = "us-west-1" },
                Capacity = new Dictionary<string, object> { ["slots"] = 4 },
                Status = new NodeStatus
                {
                    State = "active",
                    ActiveRuns = 0,
                    AvailableSlots = 4
                }
            }
        };

        var runs = new List<Run>
        {
            new Run { RunId = "run-1", NodeId = "node-1", Status = "running" }
        };

        _mockNodeStore.Setup(s => s.GetAllNodesAsync()).ReturnsAsync(nodes);
        _mockRunStore.Setup(s => s.GetAllRunsAsync()).ReturnsAsync(runs);

        var run = new Run { RunId = "run-2", AgentId = "agent-1" };
        var constraints = new Dictionary<string, object>
        {
            ["region"] = "us-east-1"
        };

        // Act
        var result = await _scheduler.ScheduleRunAsync(run, constraints);

        // Assert
        Assert.Equal("node-1", result); // Only node-1 matches the region constraint
    }

    [Fact]
    public async Task ScheduleRunAsync_RespectsMultipleRegionConstraints()
    {
        // Arrange
        var nodes = new List<Node>
        {
            new Node
            {
                NodeId = "node-1",
                Metadata = new Dictionary<string, object> { ["region"] = "us-east-1" },
                Capacity = new Dictionary<string, object> { ["slots"] = 4 },
                Status = new NodeStatus
                {
                    State = "active",
                    ActiveRuns = 2,
                    AvailableSlots = 2
                }
            },
            new Node
            {
                NodeId = "node-2",
                Metadata = new Dictionary<string, object> { ["region"] = "eu-west-1" },
                Capacity = new Dictionary<string, object> { ["slots"] = 4 },
                Status = new NodeStatus
                {
                    State = "active",
                    ActiveRuns = 1,
                    AvailableSlots = 3
                }
            },
            new Node
            {
                NodeId = "node-3",
                Metadata = new Dictionary<string, object> { ["region"] = "ap-south-1" },
                Capacity = new Dictionary<string, object> { ["slots"] = 4 },
                Status = new NodeStatus
                {
                    State = "active",
                    ActiveRuns = 0,
                    AvailableSlots = 4
                }
            }
        };

        var runs = new List<Run>
        {
            new Run { RunId = "run-1", NodeId = "node-1", Status = "running" },
            new Run { RunId = "run-2", NodeId = "node-1", Status = "running" },
            new Run { RunId = "run-3", NodeId = "node-2", Status = "running" }
        };

        _mockNodeStore.Setup(s => s.GetAllNodesAsync()).ReturnsAsync(nodes);
        _mockRunStore.Setup(s => s.GetAllRunsAsync()).ReturnsAsync(runs);

        var run = new Run { RunId = "run-4", AgentId = "agent-1" };
        var constraints = new Dictionary<string, object>
        {
            ["region"] = new List<object> { "us-east-1", "eu-west-1" }
        };

        // Act
        var result = await _scheduler.ScheduleRunAsync(run, constraints);

        // Assert
        // Should select node-2 (eu-west-1) as it has lower load (25% vs 50%) among eligible nodes
        Assert.Equal("node-2", result);
    }

    [Fact]
    public async Task ScheduleRunAsync_ReturnsNull_WhenNoNodesMatchRegionConstraint()
    {
        // Arrange
        var nodes = new List<Node>
        {
            new Node
            {
                NodeId = "node-1",
                Metadata = new Dictionary<string, object> { ["region"] = "us-east-1" },
                Capacity = new Dictionary<string, object> { ["slots"] = 4 },
                Status = new NodeStatus
                {
                    State = "active",
                    ActiveRuns = 1,
                    AvailableSlots = 3
                }
            }
        };

        var runs = new List<Run>
        {
            new Run { RunId = "run-1", NodeId = "node-1", Status = "running" }
        };

        _mockNodeStore.Setup(s => s.GetAllNodesAsync()).ReturnsAsync(nodes);
        _mockRunStore.Setup(s => s.GetAllRunsAsync()).ReturnsAsync(runs);

        var run = new Run { RunId = "run-2", AgentId = "agent-1" };
        var constraints = new Dictionary<string, object>
        {
            ["region"] = "ap-south-1"
        };

        // Act
        var result = await _scheduler.ScheduleRunAsync(run, constraints);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task ScheduleRunAsync_RespectsEnvironmentConstraint()
    {
        // Arrange
        var nodes = new List<Node>
        {
            new Node
            {
                NodeId = "node-1",
                Metadata = new Dictionary<string, object> { ["environment"] = "production" },
                Capacity = new Dictionary<string, object> { ["slots"] = 4 },
                Status = new NodeStatus
                {
                    State = "active",
                    ActiveRuns = 0,
                    AvailableSlots = 4
                }
            },
            new Node
            {
                NodeId = "node-2",
                Metadata = new Dictionary<string, object> { ["environment"] = "staging" },
                Capacity = new Dictionary<string, object> { ["slots"] = 4 },
                Status = new NodeStatus
                {
                    State = "active",
                    ActiveRuns = 0,
                    AvailableSlots = 4
                }
            }
        };

        _mockNodeStore.Setup(s => s.GetAllNodesAsync()).ReturnsAsync(nodes);
        _mockRunStore.Setup(s => s.GetAllRunsAsync()).ReturnsAsync(new List<Run>());

        var run = new Run { RunId = "run-1", AgentId = "agent-1" };
        var constraints = new Dictionary<string, object>
        {
            ["environment"] = "production"
        };

        // Act
        var result = await _scheduler.ScheduleRunAsync(run, constraints);

        // Assert
        Assert.Equal("node-1", result);
    }

    [Fact]
    public async Task ScheduleRunAsync_CombinesRegionAndEnvironmentConstraints()
    {
        // Arrange
        var nodes = new List<Node>
        {
            new Node
            {
                NodeId = "node-1",
                Metadata = new Dictionary<string, object>
                {
                    ["region"] = "us-east-1",
                    ["environment"] = "production"
                },
                Capacity = new Dictionary<string, object> { ["slots"] = 4 },
                Status = new NodeStatus
                {
                    State = "active",
                    ActiveRuns = 0,
                    AvailableSlots = 4
                }
            },
            new Node
            {
                NodeId = "node-2",
                Metadata = new Dictionary<string, object>
                {
                    ["region"] = "us-east-1",
                    ["environment"] = "staging"
                },
                Capacity = new Dictionary<string, object> { ["slots"] = 4 },
                Status = new NodeStatus
                {
                    State = "active",
                    ActiveRuns = 0,
                    AvailableSlots = 4
                }
            },
            new Node
            {
                NodeId = "node-3",
                Metadata = new Dictionary<string, object>
                {
                    ["region"] = "us-west-1",
                    ["environment"] = "production"
                },
                Capacity = new Dictionary<string, object> { ["slots"] = 4 },
                Status = new NodeStatus
                {
                    State = "active",
                    ActiveRuns = 0,
                    AvailableSlots = 4
                }
            }
        };

        _mockNodeStore.Setup(s => s.GetAllNodesAsync()).ReturnsAsync(nodes);
        _mockRunStore.Setup(s => s.GetAllRunsAsync()).ReturnsAsync(new List<Run>());

        var run = new Run { RunId = "run-1", AgentId = "agent-1" };
        var constraints = new Dictionary<string, object>
        {
            ["region"] = "us-east-1",
            ["environment"] = "production"
        };

        // Act
        var result = await _scheduler.ScheduleRunAsync(run, constraints);

        // Assert
        Assert.Equal("node-1", result); // Only node-1 matches both constraints
    }

    [Fact]
    public async Task ScheduleRunAsync_SkipsInactiveNodes()
    {
        // Arrange
        var nodes = new List<Node>
        {
            new Node
            {
                NodeId = "node-1",
                Capacity = new Dictionary<string, object> { ["slots"] = 4 },
                Status = new NodeStatus
                {
                    State = "inactive",
                    ActiveRuns = 0,
                    AvailableSlots = 4
                }
            },
            new Node
            {
                NodeId = "node-2",
                Capacity = new Dictionary<string, object> { ["slots"] = 4 },
                Status = new NodeStatus
                {
                    State = "active",
                    ActiveRuns = 0,
                    AvailableSlots = 4
                }
            }
        };

        _mockNodeStore.Setup(s => s.GetAllNodesAsync()).ReturnsAsync(nodes);
        _mockRunStore.Setup(s => s.GetAllRunsAsync()).ReturnsAsync(new List<Run>());

        var run = new Run { RunId = "run-1", AgentId = "agent-1" };

        // Act
        var result = await _scheduler.ScheduleRunAsync(run);

        // Assert
        Assert.Equal("node-2", result); // node-1 is inactive
    }

    [Fact]
    public async Task GetNodeLoadAsync_ReturnsCorrectLoadInformation()
    {
        // Arrange
        var nodes = new List<Node>
        {
            new Node
            {
                NodeId = "node-1",
                Capacity = new Dictionary<string, object> { ["slots"] = 4 },
                Metadata = new Dictionary<string, object> { ["region"] = "us-east-1" },
                Status = new NodeStatus
                {
                    State = "active",
                    ActiveRuns = 2,
                    AvailableSlots = 2
                }
            },
            new Node
            {
                NodeId = "node-2",
                Capacity = new Dictionary<string, object> { ["slots"] = 8 },
                Metadata = new Dictionary<string, object> { ["region"] = "us-west-1" },
                Status = new NodeStatus
                {
                    State = "active",
                    ActiveRuns = 3,
                    AvailableSlots = 5
                }
            }
        };

        var runs = new List<Run>
        {
            new Run { RunId = "run-1", NodeId = "node-1", Status = "running" },
            new Run { RunId = "run-2", NodeId = "node-1", Status = "assigned" },
            new Run { RunId = "run-3", NodeId = "node-2", Status = "running" },
            new Run { RunId = "run-4", NodeId = "node-2", Status = "running" },
            new Run { RunId = "run-5", NodeId = "node-2", Status = "assigned" }
        };

        _mockNodeStore.Setup(s => s.GetAllNodesAsync()).ReturnsAsync(nodes);
        _mockRunStore.Setup(s => s.GetAllRunsAsync()).ReturnsAsync(runs);

        // Act
        var result = await _scheduler.GetNodeLoadAsync();

        // Assert
        Assert.Equal(2, result.Count);

        Assert.Equal("node-1", result["node-1"].NodeId);
        Assert.Equal(4, result["node-1"].TotalSlots);
        Assert.Equal(2, result["node-1"].ActiveRuns);
        Assert.Equal(2, result["node-1"].AvailableSlots);
        Assert.Equal(50.0, result["node-1"].LoadPercentage);
        Assert.True(result["node-1"].HasCapacity);

        Assert.Equal("node-2", result["node-2"].NodeId);
        Assert.Equal(8, result["node-2"].TotalSlots);
        Assert.Equal(3, result["node-2"].ActiveRuns);
        Assert.Equal(5, result["node-2"].AvailableSlots);
        Assert.Equal(37.5, result["node-2"].LoadPercentage);
        Assert.True(result["node-2"].HasCapacity);
    }

    [Fact]
    public async Task ScheduleRunAsync_PrefersNodeWithMoreAvailableSlots_WhenLoadIsEqual()
    {
        // Arrange
        var nodes = new List<Node>
        {
            new Node
            {
                NodeId = "node-1",
                Capacity = new Dictionary<string, object> { ["slots"] = 4 },
                Status = new NodeStatus
                {
                    State = "active",
                    ActiveRuns = 1,
                    AvailableSlots = 3
                }
            },
            new Node
            {
                NodeId = "node-2",
                Capacity = new Dictionary<string, object> { ["slots"] = 8 },
                Status = new NodeStatus
                {
                    State = "active",
                    ActiveRuns = 2,
                    AvailableSlots = 6
                }
            }
        };

        var runs = new List<Run>
        {
            new Run { RunId = "run-1", NodeId = "node-1", Status = "running" },
            new Run { RunId = "run-2", NodeId = "node-2", Status = "running" },
            new Run { RunId = "run-3", NodeId = "node-2", Status = "running" }
        };

        _mockNodeStore.Setup(s => s.GetAllNodesAsync()).ReturnsAsync(nodes);
        _mockRunStore.Setup(s => s.GetAllRunsAsync()).ReturnsAsync(runs);

        var run = new Run { RunId = "run-4", AgentId = "agent-1" };

        // Act
        var result = await _scheduler.ScheduleRunAsync(run);

        // Assert
        // Both nodes have 25% load, but node-2 has more available slots (6 vs 3)
        Assert.Equal("node-2", result);
    }
}
