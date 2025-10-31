using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ControlPlane.Api.Models;
using ControlPlane.Api.Services;

namespace ControlPlane.Api.Tests;

/// <summary>
/// Integration tests for node registration and heartbeat functionality (E2-T3).
/// Tests the complete flow from registration through heartbeat updates.
/// </summary>
public class NodeRegistrationIntegrationTests : IAsyncLifetime
{
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    public Task InitializeAsync()
    {
        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((context, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["UseInMemoryStores"] = "true"
                    });
                });
                
                // Override service registrations to use in-memory stores
                builder.ConfigureServices(services =>
                {
                    // Remove any existing store registrations
                    var descriptorsToRemove = services
                        .Where(d => d.ServiceType == typeof(IAgentStore) || 
                                    d.ServiceType == typeof(INodeStore) || 
                                    d.ServiceType == typeof(IRunStore))
                        .ToList();
                    
                    foreach (var descriptor in descriptorsToRemove)
                    {
                        services.Remove(descriptor);
                    }
                    
                    // Add in-memory stores
                    services.AddSingleton<IAgentStore, InMemoryAgentStore>();
                    services.AddSingleton<INodeStore, InMemoryNodeStore>();
                    services.AddSingleton<IRunStore, InMemoryRunStore>();
                });
            });
        _client = _factory.CreateClient();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task CompleteNodeLifecycle_RegisterHeartbeatAndRetrieve_Succeeds()
    {
        // Arrange
        var nodeId = "integration-test-node-1";
        var registerRequest = new RegisterNodeRequest
        {
            NodeId = nodeId,
            Metadata = new Dictionary<string, object>
            {
                { "Region", "us-east-1" },
                { "Environment", "development" },
                { "Version", "1.0.0" }
            },
            Capacity = new Dictionary<string, object>
            {
                { "slots", 8 },
                { "cpu", "4" },
                { "memory", "8Gi" }
            }
        };

        // Act 1: Register the node
        var registerResponse = await _client.PostAsJsonAsync("/v1/nodes:register", registerRequest);

        // Assert: Registration successful
        Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);
        var registeredNode = await registerResponse.Content.ReadFromJsonAsync<Node>();
        Assert.NotNull(registeredNode);
        Assert.Equal(nodeId, registeredNode.NodeId);
        Assert.Equal("active", registeredNode.Status.State);
        Assert.Equal(0, registeredNode.Status.ActiveRuns);
        Assert.Contains("Region", registeredNode.Metadata!.Keys);
        Assert.Contains("slots", registeredNode.Capacity!.Keys);

        // Act 2: Retrieve the registered node
        var getResponse = await _client.GetAsync($"/v1/nodes/{nodeId}");
        
        // Assert: Node can be retrieved
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var retrievedNode = await getResponse.Content.ReadFromJsonAsync<Node>();
        Assert.NotNull(retrievedNode);
        Assert.Equal(nodeId, retrievedNode.NodeId);

        // Act 3: Send heartbeat with updated status
        var heartbeatRequest = new HeartbeatRequest
        {
            Status = new NodeStatus
            {
                State = "active",
                ActiveRuns = 2,
                AvailableSlots = 6
            }
        };
        var heartbeatResponse = await _client.PostAsJsonAsync($"/v1/nodes/{nodeId}:heartbeat", heartbeatRequest);

        // Assert: Heartbeat accepted and status updated
        Assert.Equal(HttpStatusCode.OK, heartbeatResponse.StatusCode);
        var updatedNode = await heartbeatResponse.Content.ReadFromJsonAsync<Node>();
        Assert.NotNull(updatedNode);
        Assert.Equal(2, updatedNode.Status.ActiveRuns);
        Assert.Equal(6, updatedNode.Status.AvailableSlots);
        Assert.True(updatedNode.HeartbeatAt > registeredNode.HeartbeatAt);
    }

    [Fact]
    public async Task RegisterMultipleNodes_WithDifferentCapacities_AllSucceed()
    {
        // Arrange
        var nodes = new[]
        {
            new RegisterNodeRequest
            {
                NodeId = "node-small",
                Metadata = new Dictionary<string, object> { { "size", "small" } },
                Capacity = new Dictionary<string, object>
                {
                    { "slots", 4 },
                    { "cpu", "2" },
                    { "memory", "4Gi" }
                }
            },
            new RegisterNodeRequest
            {
                NodeId = "node-medium",
                Metadata = new Dictionary<string, object> { { "size", "medium" } },
                Capacity = new Dictionary<string, object>
                {
                    { "slots", 8 },
                    { "cpu", "4" },
                    { "memory", "8Gi" }
                }
            },
            new RegisterNodeRequest
            {
                NodeId = "node-large",
                Metadata = new Dictionary<string, object> { { "size", "large" } },
                Capacity = new Dictionary<string, object>
                {
                    { "slots", 16 },
                    { "cpu", "8" },
                    { "memory", "16Gi" }
                }
            }
        };

        // Act: Register all nodes
        foreach (var nodeRequest in nodes)
        {
            var response = await _client.PostAsJsonAsync("/v1/nodes:register", nodeRequest);
            
            // Assert: Each registration succeeds
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        }

        // Act: Retrieve all nodes
        var getAllResponse = await _client.GetAsync("/v1/nodes");
        
        // Assert: All nodes are registered
        Assert.Equal(HttpStatusCode.OK, getAllResponse.StatusCode);
        var allNodes = await getAllResponse.Content.ReadFromJsonAsync<List<Node>>();
        Assert.NotNull(allNodes);
        Assert.Equal(3, allNodes.Count);
        Assert.Contains(allNodes, n => n.NodeId == "node-small");
        Assert.Contains(allNodes, n => n.NodeId == "node-medium");
        Assert.Contains(allNodes, n => n.NodeId == "node-large");
    }

    [Fact]
    public async Task RegisterNode_WithMinimalData_Succeeds()
    {
        // Arrange: Node with minimal required data
        var registerRequest = new RegisterNodeRequest
        {
            NodeId = "minimal-node"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/v1/nodes:register", registerRequest);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var node = await response.Content.ReadFromJsonAsync<Node>();
        Assert.NotNull(node);
        Assert.Equal("minimal-node", node.NodeId);
        Assert.Equal("active", node.Status.State);
    }

    [Fact]
    public async Task RegisterNode_WithEmptyNodeId_ReturnsBadRequest()
    {
        // Arrange
        var registerRequest = new RegisterNodeRequest
        {
            NodeId = "",
            Capacity = new Dictionary<string, object> { { "slots", 8 } }
        };

        // Act
        var response = await _client.PostAsJsonAsync("/v1/nodes:register", registerRequest);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Heartbeat_ForNonExistentNode_ReturnsNotFound()
    {
        // Arrange
        var heartbeatRequest = new HeartbeatRequest
        {
            Status = new NodeStatus
            {
                State = "active",
                ActiveRuns = 0,
                AvailableSlots = 8
            }
        };

        // Act
        var response = await _client.PostAsJsonAsync("/v1/nodes/non-existent-node:heartbeat", heartbeatRequest);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task MultipleHeartbeats_UpdateNodeStatus_Successfully()
    {
        // Arrange: Register a node
        var nodeId = "heartbeat-test-node";
        var registerRequest = new RegisterNodeRequest
        {
            NodeId = nodeId,
            Capacity = new Dictionary<string, object> { { "slots", 10 } }
        };
        await _client.PostAsJsonAsync("/v1/nodes:register", registerRequest);

        // Act & Assert: Send multiple heartbeats with changing status
        var testScenarios = new[]
        {
            new { ActiveRuns = 0, AvailableSlots = 10 },
            new { ActiveRuns = 3, AvailableSlots = 7 },
            new { ActiveRuns = 7, AvailableSlots = 3 },
            new { ActiveRuns = 10, AvailableSlots = 0 },
            new { ActiveRuns = 5, AvailableSlots = 5 }
        };

        foreach (var scenario in testScenarios)
        {
            var heartbeatRequest = new HeartbeatRequest
            {
                Status = new NodeStatus
                {
                    State = "active",
                    ActiveRuns = scenario.ActiveRuns,
                    AvailableSlots = scenario.AvailableSlots
                }
            };

            var response = await _client.PostAsJsonAsync($"/v1/nodes/{nodeId}:heartbeat", heartbeatRequest);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var node = await response.Content.ReadFromJsonAsync<Node>();
            Assert.NotNull(node);
            Assert.Equal(scenario.ActiveRuns, node.Status.ActiveRuns);
            Assert.Equal(scenario.AvailableSlots, node.Status.AvailableSlots);
        }
    }

    [Fact]
    public async Task RegisterNode_WithRegionMetadata_CanBeUsedForPlacement()
    {
        // Arrange: Register nodes in different regions
        var regions = new[] { "us-east-1", "us-west-2", "eu-west-1", "ap-southeast-1" };
        
        foreach (var region in regions)
        {
            var registerRequest = new RegisterNodeRequest
            {
                NodeId = $"node-{region}",
                Metadata = new Dictionary<string, object>
                {
                    { "Region", region },
                    { "Environment", "production" }
                },
                Capacity = new Dictionary<string, object> { { "slots", 8 } }
            };

            var response = await _client.PostAsJsonAsync("/v1/nodes:register", registerRequest);
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        }

        // Act: Retrieve all nodes
        var getAllResponse = await _client.GetAsync("/v1/nodes");
        var allNodes = await getAllResponse.Content.ReadFromJsonAsync<List<Node>>();

        // Assert: All regional nodes are registered with correct metadata
        Assert.NotNull(allNodes);
        Assert.Equal(4, allNodes.Count);
        
        foreach (var region in regions)
        {
            var node = allNodes.FirstOrDefault(n => n.NodeId == $"node-{region}");
            Assert.NotNull(node);
            Assert.Contains("Region", node.Metadata!.Keys);
            Assert.Equal(region, node.Metadata["Region"].ToString());
        }
    }

    [Fact]
    public async Task DeleteNode_AfterRegistration_Succeeds()
    {
        // Arrange: Register a node
        var nodeId = "node-to-delete";
        var registerRequest = new RegisterNodeRequest
        {
            NodeId = nodeId,
            Capacity = new Dictionary<string, object> { { "slots", 8 } }
        };
        await _client.PostAsJsonAsync("/v1/nodes:register", registerRequest);

        // Act: Delete the node
        var deleteResponse = await _client.DeleteAsync($"/v1/nodes/{nodeId}");

        // Assert: Deletion succeeds
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        // Verify node is gone
        var getResponse = await _client.GetAsync($"/v1/nodes/{nodeId}");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task HeartbeatAfterDeletion_ReturnsNotFound()
    {
        // Arrange: Register and then delete a node
        var nodeId = "node-deleted";
        var registerRequest = new RegisterNodeRequest
        {
            NodeId = nodeId,
            Capacity = new Dictionary<string, object> { { "slots", 8 } }
        };
        await _client.PostAsJsonAsync("/v1/nodes:register", registerRequest);
        await _client.DeleteAsync($"/v1/nodes/{nodeId}");

        // Act: Try to send heartbeat
        var heartbeatRequest = new HeartbeatRequest
        {
            Status = new NodeStatus { State = "active", ActiveRuns = 0, AvailableSlots = 8 }
        };
        var response = await _client.PostAsJsonAsync($"/v1/nodes/{nodeId}:heartbeat", heartbeatRequest);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
