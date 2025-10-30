using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ControlPlane.Api.Models;
using ControlPlane.Api.Services;

namespace ControlPlane.Api.Tests;

public class AgentEndpointsTests : IAsyncLifetime
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
                    // Remove any existing IAgentStore, INodeStore, IRunStore registrations
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
    public async Task GetAgents_ReturnsEmptyList_Initially()
    {
        // Act
        var response = await _client.GetAsync("/v1/agents");

        // Assert
        response.EnsureSuccessStatusCode();
        var agents = await response.Content.ReadFromJsonAsync<List<Agent>>();
        Assert.NotNull(agents);
        Assert.Empty(agents);
    }

    [Fact]
    public async Task CreateAgent_ReturnsCreatedAgent()
    {
        // Arrange
        var request = new CreateAgentRequest
        {
            Name = "Test Agent",
            Instructions = "Test instructions",
            ModelProfile = new Dictionary<string, object>
            {
                { "model", "gpt-4" },
                { "temperature", 0.7 }
            }
        };

        // Act
        var response = await _client.PostAsJsonAsync("/v1/agents", request);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var agent = await response.Content.ReadFromJsonAsync<Agent>();
        Assert.NotNull(agent);
        Assert.NotEmpty(agent.AgentId);
        Assert.Equal("Test Agent", agent.Name);
        Assert.Equal("Test instructions", agent.Instructions);
        Assert.NotNull(agent.ModelProfile);
    }

    [Fact]
    public async Task CreateAgent_WithoutName_ReturnsBadRequest()
    {
        // Arrange
        var request = new CreateAgentRequest
        {
            Name = "",
            Instructions = "Test instructions"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/v1/agents", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetAgent_ReturnsAgent_WhenExists()
    {
        // Arrange
        var createRequest = new CreateAgentRequest
        {
            Name = "Test Agent",
            Instructions = "Test instructions"
        };
        var createResponse = await _client.PostAsJsonAsync("/v1/agents", createRequest);
        var createdAgent = await createResponse.Content.ReadFromJsonAsync<Agent>();

        // Act
        var response = await _client.GetAsync($"/v1/agents/{createdAgent!.AgentId}");

        // Assert
        response.EnsureSuccessStatusCode();
        var agent = await response.Content.ReadFromJsonAsync<Agent>();
        Assert.NotNull(agent);
        Assert.Equal(createdAgent.AgentId, agent.AgentId);
    }

    [Fact]
    public async Task GetAgent_ReturnsNotFound_WhenDoesNotExist()
    {
        // Act
        var response = await _client.GetAsync("/v1/agents/nonexistent-id");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdateAgent_ReturnsUpdatedAgent_WhenExists()
    {
        // Arrange
        var createRequest = new CreateAgentRequest
        {
            Name = "Original Name",
            Instructions = "Original instructions"
        };
        var createResponse = await _client.PostAsJsonAsync("/v1/agents", createRequest);
        var createdAgent = await createResponse.Content.ReadFromJsonAsync<Agent>();

        var updateRequest = new UpdateAgentRequest
        {
            Name = "Updated Name",
            Instructions = "Updated instructions"
        };

        // Act
        var response = await _client.PutAsJsonAsync($"/v1/agents/{createdAgent!.AgentId}", updateRequest);

        // Assert
        response.EnsureSuccessStatusCode();
        var agent = await response.Content.ReadFromJsonAsync<Agent>();
        Assert.NotNull(agent);
        Assert.Equal("Updated Name", agent.Name);
        Assert.Equal("Updated instructions", agent.Instructions);
    }

    [Fact]
    public async Task DeleteAgent_ReturnsNoContent_WhenExists()
    {
        // Arrange
        var createRequest = new CreateAgentRequest
        {
            Name = "Test Agent",
            Instructions = "Test instructions"
        };
        var createResponse = await _client.PostAsJsonAsync("/v1/agents", createRequest);
        var createdAgent = await createResponse.Content.ReadFromJsonAsync<Agent>();

        // Act
        var response = await _client.DeleteAsync($"/v1/agents/{createdAgent!.AgentId}");

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task DeleteAgent_ReturnsNotFound_WhenDoesNotExist()
    {
        // Act
        var response = await _client.DeleteAsync("/v1/agents/nonexistent-id");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreateAgent_WithFullDefinition_ReturnsCompleteAgent()
    {
        // Arrange
        var request = new CreateAgentRequest
        {
            Name = "Invoice Classifier",
            Description = "Classifies invoices by vendor and routes appropriately",
            Instructions = "Classify the invoice based on vendor information",
            ModelProfile = new Dictionary<string, object>
            {
                { "model", "gpt-4" },
                { "temperature", 0.7 }
            },
            Budget = new AgentBudget
            {
                MaxTokens = 4000,
                MaxDurationSeconds = 60
            },
            Tools = new List<string> { "http-post", "json-parser" },
            Input = new ConnectorConfiguration
            {
                Type = "service-bus",
                Config = new Dictionary<string, object>
                {
                    { "queue", "invoices" },
                    { "prefetch", 16 }
                }
            },
            Output = new ConnectorConfiguration
            {
                Type = "http",
                Config = new Dictionary<string, object>
                {
                    { "url", "https://api.example.com/invoices" },
                    { "method", "POST" }
                }
            },
            Metadata = new Dictionary<string, string>
            {
                { "team", "finance" },
                { "environment", "production" }
            }
        };

        // Act
        var response = await _client.PostAsJsonAsync("/v1/agents", request);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var agent = await response.Content.ReadFromJsonAsync<Agent>();
        Assert.NotNull(agent);
        Assert.Equal("Invoice Classifier", agent.Name);
        Assert.Equal("Classifies invoices by vendor and routes appropriately", agent.Description);
        Assert.NotNull(agent.Budget);
        Assert.Equal(4000, agent.Budget.MaxTokens);
        Assert.Equal(60, agent.Budget.MaxDurationSeconds);
        Assert.NotNull(agent.Tools);
        Assert.Equal(2, agent.Tools.Count);
        Assert.Contains("http-post", agent.Tools);
        Assert.NotNull(agent.Input);
        Assert.Equal("service-bus", agent.Input.Type);
        Assert.NotNull(agent.Output);
        Assert.Equal("http", agent.Output.Type);
        Assert.NotNull(agent.Metadata);
        Assert.Equal("finance", agent.Metadata["team"]);
    }

    [Fact]
    public async Task UpdateAgent_UpdatesBudgetAndTools()
    {
        // Arrange
        var createRequest = new CreateAgentRequest
        {
            Name = "Test Agent",
            Instructions = "Test instructions",
            Budget = new AgentBudget { MaxTokens = 1000 }
        };
        var createResponse = await _client.PostAsJsonAsync("/v1/agents", createRequest);
        var createdAgent = await createResponse.Content.ReadFromJsonAsync<Agent>();

        var updateRequest = new UpdateAgentRequest
        {
            Budget = new AgentBudget 
            { 
                MaxTokens = 5000,
                MaxDurationSeconds = 120
            },
            Tools = new List<string> { "calculator", "web-search" }
        };

        // Act
        var response = await _client.PutAsJsonAsync($"/v1/agents/{createdAgent!.AgentId}", updateRequest);

        // Assert
        response.EnsureSuccessStatusCode();
        var agent = await response.Content.ReadFromJsonAsync<Agent>();
        Assert.NotNull(agent);
        Assert.NotNull(agent.Budget);
        Assert.Equal(5000, agent.Budget.MaxTokens);
        Assert.Equal(120, agent.Budget.MaxDurationSeconds);
        Assert.NotNull(agent.Tools);
        Assert.Equal(2, agent.Tools.Count);
    }

    [Fact]
    public async Task CreateAgent_WithConnectors_PreservesConnectorConfiguration()
    {
        // Arrange
        var request = new CreateAgentRequest
        {
            Name = "Data Processor",
            Instructions = "Process data from queue",
            Input = new ConnectorConfiguration
            {
                Type = "azure-service-bus",
                Config = new Dictionary<string, object>
                {
                    { "connectionString", "Endpoint=sb://test.servicebus.windows.net/" },
                    { "queueName", "data-queue" }
                }
            },
            Output = new ConnectorConfiguration
            {
                Type = "blob-storage",
                Config = new Dictionary<string, object>
                {
                    { "container", "processed-data" },
                    { "format", "json" }
                }
            }
        };

        // Act
        var response = await _client.PostAsJsonAsync("/v1/agents", request);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var agent = await response.Content.ReadFromJsonAsync<Agent>();
        Assert.NotNull(agent);
        Assert.NotNull(agent.Input);
        Assert.Equal("azure-service-bus", agent.Input.Type);
        Assert.NotNull(agent.Input.Config);
        Assert.True(agent.Input.Config.ContainsKey("queueName"));
        Assert.NotNull(agent.Output);
        Assert.Equal("blob-storage", agent.Output.Type);
    }
}
