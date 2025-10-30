using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using ControlPlane.Api.Models;

namespace ControlPlane.Api.Tests;

public class NodeEndpointsTests : IAsyncLifetime
{
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    public Task InitializeAsync()
    {
        _factory = new WebApplicationFactory<Program>();
        _client = _factory.CreateClient();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task GetNodes_ReturnsEmptyList_Initially()
    {
        // Act
        var response = await _client.GetAsync("/v1/nodes");

        // Assert
        response.EnsureSuccessStatusCode();
        var nodes = await response.Content.ReadFromJsonAsync<List<Node>>();
        Assert.NotNull(nodes);
        Assert.Empty(nodes);
    }

    [Fact]
    public async Task RegisterNode_ReturnsCreatedNode()
    {
        // Arrange
        var request = new RegisterNodeRequest
        {
            NodeId = "node-1",
            Metadata = new Dictionary<string, object>
            {
                { "region", "us-east-1" },
                { "environment", "dev" }
            },
            Capacity = new Dictionary<string, object>
            {
                { "slots", 8 },
                { "cpu", "4" },
                { "memory", "8Gi" }
            }
        };

        // Act
        var response = await _client.PostAsJsonAsync("/v1/nodes:register", request);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var node = await response.Content.ReadFromJsonAsync<Node>();
        Assert.NotNull(node);
        Assert.Equal("node-1", node.NodeId);
        Assert.NotNull(node.Capacity);
        Assert.Equal("active", node.Status.State);
    }

    [Fact]
    public async Task RegisterNode_WithoutNodeId_ReturnsBadRequest()
    {
        // Arrange
        var request = new RegisterNodeRequest
        {
            NodeId = "",
            Capacity = new Dictionary<string, object> { { "slots", 8 } }
        };

        // Act
        var response = await _client.PostAsJsonAsync("/v1/nodes:register", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetNode_ReturnsNode_WhenExists()
    {
        // Arrange
        var registerRequest = new RegisterNodeRequest
        {
            NodeId = "node-2",
            Capacity = new Dictionary<string, object> { { "slots", 8 } }
        };
        var registerResponse = await _client.PostAsJsonAsync("/v1/nodes:register", registerRequest);
        var registeredNode = await registerResponse.Content.ReadFromJsonAsync<Node>();

        // Act
        var response = await _client.GetAsync($"/v1/nodes/{registeredNode!.NodeId}");

        // Assert
        response.EnsureSuccessStatusCode();
        var node = await response.Content.ReadFromJsonAsync<Node>();
        Assert.NotNull(node);
        Assert.Equal(registeredNode.NodeId, node.NodeId);
    }

    [Fact]
    public async Task Heartbeat_UpdatesNode_WhenExists()
    {
        // Arrange
        var registerRequest = new RegisterNodeRequest
        {
            NodeId = "node-3",
            Capacity = new Dictionary<string, object> { { "slots", 8 } }
        };
        var registerResponse = await _client.PostAsJsonAsync("/v1/nodes:register", registerRequest);
        var registeredNode = await registerResponse.Content.ReadFromJsonAsync<Node>();

        var heartbeatRequest = new HeartbeatRequest
        {
            Status = new NodeStatus
            {
                State = "active",
                ActiveRuns = 2,
                AvailableSlots = 6
            }
        };

        // Act
        var response = await _client.PostAsJsonAsync($"/v1/nodes/{registeredNode!.NodeId}:heartbeat", heartbeatRequest);

        // Assert
        response.EnsureSuccessStatusCode();
        var node = await response.Content.ReadFromJsonAsync<Node>();
        Assert.NotNull(node);
        Assert.Equal(2, node.Status.ActiveRuns);
        Assert.Equal(6, node.Status.AvailableSlots);
    }

    [Fact]
    public async Task DeleteNode_ReturnsNoContent_WhenExists()
    {
        // Arrange
        var registerRequest = new RegisterNodeRequest
        {
            NodeId = "node-4",
            Capacity = new Dictionary<string, object> { { "slots", 8 } }
        };
        var registerResponse = await _client.PostAsJsonAsync("/v1/nodes:register", registerRequest);
        var registeredNode = await registerResponse.Content.ReadFromJsonAsync<Node>();

        // Act
        var response = await _client.DeleteAsync($"/v1/nodes/{registeredNode!.NodeId}");

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task DeleteNode_ReturnsNotFound_WhenDoesNotExist()
    {
        // Act
        var response = await _client.DeleteAsync("/v1/nodes/nonexistent-id");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
