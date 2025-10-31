using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ControlPlane.Api.Models;
using ControlPlane.Api.Services;

namespace ControlPlane.Api.Tests;

public class AgentVersioningEndpointsTests : IAsyncLifetime
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
                
                builder.ConfigureServices(services =>
                {
                    var descriptorsToRemove = services
                        .Where(d => d.ServiceType == typeof(IAgentStore) || 
                                    d.ServiceType == typeof(INodeStore) || 
                                    d.ServiceType == typeof(IRunStore))
                        .ToList();
                    
                    foreach (var descriptor in descriptorsToRemove)
                    {
                        services.Remove(descriptor);
                    }
                    
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
    public async Task CreateAgentVersion_WithValidVersion_ReturnsCreated()
    {
        // Arrange
        var agent = await CreateTestAgent("Test Agent");
        var versionRequest = new CreateAgentVersionRequest
        {
            Version = "1.0.0",
            Spec = new Agent
            {
                AgentId = agent.AgentId,
                Name = agent.Name,
                Instructions = agent.Instructions
            }
        };

        // Act
        var response = await _client.PostAsJsonAsync($"/v1/agents/{agent.AgentId}:version", versionRequest);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var version = await response.Content.ReadFromJsonAsync<AgentVersionResponse>();
        Assert.NotNull(version);
        Assert.Equal(agent.AgentId, version.AgentId);
        Assert.Equal("1.0.0", version.Version);
        Assert.NotNull(version.Spec);
        Assert.True((DateTime.UtcNow - version.CreatedAt).TotalSeconds < 5);
    }

    [Fact]
    public async Task CreateAgentVersion_WithInvalidSemVer_ReturnsBadRequest()
    {
        // Arrange
        var agent = await CreateTestAgent("Test Agent");
        var versionRequest = new CreateAgentVersionRequest
        {
            Version = "invalid-version",
            Spec = new Agent { AgentId = agent.AgentId, Name = agent.Name, Instructions = agent.Instructions }
        };

        // Act
        var response = await _client.PostAsJsonAsync($"/v1/agents/{agent.AgentId}:version", versionRequest);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateAgentVersion_WithEmptyVersion_ReturnsBadRequest()
    {
        // Arrange
        var agent = await CreateTestAgent("Test Agent");
        var versionRequest = new CreateAgentVersionRequest
        {
            Version = "",
            Spec = new Agent { AgentId = agent.AgentId, Name = agent.Name, Instructions = agent.Instructions }
        };

        // Act
        var response = await _client.PostAsJsonAsync($"/v1/agents/{agent.AgentId}:version", versionRequest);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateAgentVersion_ForNonExistentAgent_ReturnsConflict()
    {
        // Arrange
        var versionRequest = new CreateAgentVersionRequest
        {
            Version = "1.0.0",
            Spec = new Agent { AgentId = "nonexistent", Name = "Test", Instructions = "Test" }
        };

        // Act
        var response = await _client.PostAsJsonAsync("/v1/agents/nonexistent:version", versionRequest);

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task CreateAgentVersion_WithDuplicateVersion_ReturnsConflict()
    {
        // Arrange
        var agent = await CreateTestAgent("Test Agent");
        var versionRequest = new CreateAgentVersionRequest
        {
            Version = "1.0.0",
            Spec = new Agent { AgentId = agent.AgentId, Name = agent.Name, Instructions = agent.Instructions }
        };
        await _client.PostAsJsonAsync($"/v1/agents/{agent.AgentId}:version", versionRequest);

        // Act
        var response = await _client.PostAsJsonAsync($"/v1/agents/{agent.AgentId}:version", versionRequest);

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Theory]
    [InlineData("1.0.0")]
    [InlineData("0.1.0")]
    [InlineData("1.2.3")]
    [InlineData("1.0.0-alpha")]
    [InlineData("1.0.0-beta.1")]
    [InlineData("1.0.0-rc.1+build.123")]
    [InlineData("2.0.0+20231030")]
    public async Task CreateAgentVersion_WithValidSemVerFormats_ReturnsCreated(string version)
    {
        // Arrange
        var agent = await CreateTestAgent($"Agent-{version}");
        var versionRequest = new CreateAgentVersionRequest
        {
            Version = version,
            Spec = new Agent { AgentId = agent.AgentId, Name = agent.Name, Instructions = agent.Instructions }
        };

        // Act
        var response = await _client.PostAsJsonAsync($"/v1/agents/{agent.AgentId}:version", versionRequest);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Theory]
    [InlineData("1.0")]
    [InlineData("v1.0.0")]
    [InlineData("1.0.0.0")]
    [InlineData("1.0.0-")]
    [InlineData("1.0.0+")]
    [InlineData("abc")]
    public async Task CreateAgentVersion_WithInvalidSemVerFormats_ReturnsBadRequest(string version)
    {
        // Arrange
        var agent = await CreateTestAgent($"Agent-{Guid.NewGuid()}");
        var versionRequest = new CreateAgentVersionRequest
        {
            Version = version,
            Spec = new Agent { AgentId = agent.AgentId, Name = agent.Name, Instructions = agent.Instructions }
        };

        // Act
        var response = await _client.PostAsJsonAsync($"/v1/agents/{agent.AgentId}:version", versionRequest);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetAgentVersions_ReturnsEmptyList_Initially()
    {
        // Arrange
        var agent = await CreateTestAgent("Test Agent");

        // Act
        var response = await _client.GetAsync($"/v1/agents/{agent.AgentId}/versions");

        // Assert
        response.EnsureSuccessStatusCode();
        var versions = await response.Content.ReadFromJsonAsync<List<AgentVersionResponse>>();
        Assert.NotNull(versions);
        Assert.Empty(versions);
    }

    [Fact]
    public async Task GetAgentVersions_ReturnsAllVersions_OrderedByCreatedAtDescending()
    {
        // Arrange
        var agent = await CreateTestAgent("Test Agent");
        await CreateTestVersion(agent.AgentId, "1.0.0");
        await Task.Delay(100); // Ensure different timestamps
        await CreateTestVersion(agent.AgentId, "1.1.0");
        await Task.Delay(100);
        await CreateTestVersion(agent.AgentId, "2.0.0");

        // Act
        var response = await _client.GetAsync($"/v1/agents/{agent.AgentId}/versions");

        // Assert
        response.EnsureSuccessStatusCode();
        var versions = await response.Content.ReadFromJsonAsync<List<AgentVersionResponse>>();
        Assert.NotNull(versions);
        Assert.Equal(3, versions.Count);
        Assert.Equal("2.0.0", versions[0].Version);
        Assert.Equal("1.1.0", versions[1].Version);
        Assert.Equal("1.0.0", versions[2].Version);
    }

    [Fact]
    public async Task GetAgentVersion_ReturnsVersion_WhenExists()
    {
        // Arrange
        var agent = await CreateTestAgent("Test Agent");
        await CreateTestVersion(agent.AgentId, "1.0.0");

        // Act
        var response = await _client.GetAsync($"/v1/agents/{agent.AgentId}/versions/1.0.0");

        // Assert
        response.EnsureSuccessStatusCode();
        var version = await response.Content.ReadFromJsonAsync<AgentVersionResponse>();
        Assert.NotNull(version);
        Assert.Equal("1.0.0", version.Version);
        Assert.Equal(agent.AgentId, version.AgentId);
    }

    [Fact]
    public async Task GetAgentVersion_ReturnsNotFound_WhenDoesNotExist()
    {
        // Arrange
        var agent = await CreateTestAgent("Test Agent");

        // Act
        var response = await _client.GetAsync($"/v1/agents/{agent.AgentId}/versions/99.99.99");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeleteAgentVersion_ReturnsNoContent_WhenExists()
    {
        // Arrange
        var agent = await CreateTestAgent("Test Agent");
        await CreateTestVersion(agent.AgentId, "1.0.0");

        // Act
        var response = await _client.DeleteAsync($"/v1/agents/{agent.AgentId}/versions/1.0.0");

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        // Verify deletion
        var getResponse = await _client.GetAsync($"/v1/agents/{agent.AgentId}/versions/1.0.0");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task DeleteAgentVersion_ReturnsNotFound_WhenDoesNotExist()
    {
        // Arrange
        var agent = await CreateTestAgent("Test Agent");

        // Act
        var response = await _client.DeleteAsync($"/v1/agents/{agent.AgentId}/versions/99.99.99");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreateAgentVersion_WithNullSpec_ReturnsCreated()
    {
        // Arrange
        var agent = await CreateTestAgent("Test Agent");
        var versionRequest = new CreateAgentVersionRequest
        {
            Version = "1.0.0",
            Spec = null
        };

        // Act
        var response = await _client.PostAsJsonAsync($"/v1/agents/{agent.AgentId}:version", versionRequest);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var version = await response.Content.ReadFromJsonAsync<AgentVersionResponse>();
        Assert.NotNull(version);
        Assert.Null(version.Spec);
    }

    [Fact]
    public async Task CreateAgentVersion_WithCompleteSpec_PreservesAllFields()
    {
        // Arrange
        var agent = await CreateTestAgent("Invoice Classifier");
        var spec = new Agent
        {
            AgentId = agent.AgentId,
            Name = "Invoice Classifier",
            Description = "Classifies invoices",
            Instructions = "Classify based on vendor",
            ModelProfile = new Dictionary<string, object> { { "model", "gpt-4" } },
            Budget = new AgentBudget { MaxTokens = 4000, MaxDurationSeconds = 60 },
            Tools = new List<string> { "http-post" },
            Input = new ConnectorConfiguration { Type = "service-bus" },
            Output = new ConnectorConfiguration { Type = "http" },
            Metadata = new Dictionary<string, string> { { "team", "finance" } }
        };
        var versionRequest = new CreateAgentVersionRequest
        {
            Version = "1.0.0",
            Spec = spec
        };

        // Act
        var response = await _client.PostAsJsonAsync($"/v1/agents/{agent.AgentId}:version", versionRequest);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var version = await response.Content.ReadFromJsonAsync<AgentVersionResponse>();
        Assert.NotNull(version);
        Assert.NotNull(version.Spec);
        Assert.Equal("Invoice Classifier", version.Spec.Name);
        Assert.Equal("Classifies invoices", version.Spec.Description);
        Assert.NotNull(version.Spec.Budget);
        Assert.Equal(4000, version.Spec.Budget.MaxTokens);
        Assert.Contains("http-post", version.Spec.Tools!);
    }

    [Fact]
    public async Task DeleteAgent_RemovesAllVersions()
    {
        // Arrange
        var agent = await CreateTestAgent("Test Agent");
        await CreateTestVersion(agent.AgentId, "1.0.0");
        await CreateTestVersion(agent.AgentId, "2.0.0");

        // Act
        await _client.DeleteAsync($"/v1/agents/{agent.AgentId}");

        // Assert
        var versionsResponse = await _client.GetAsync($"/v1/agents/{agent.AgentId}/versions");
        var versions = await versionsResponse.Content.ReadFromJsonAsync<List<AgentVersionResponse>>();
        Assert.Empty(versions!);
    }

    [Fact]
    public async Task CreateAgentVersion_WithInvalidSpec_MissingName_ReturnsBadRequest()
    {
        // Arrange
        var agent = await CreateTestAgent("Test Agent");
        var versionRequest = new CreateAgentVersionRequest
        {
            Version = "1.0.0",
            Spec = new Agent
            {
                AgentId = agent.AgentId,
                Name = "", // Invalid: empty name
                Instructions = "Test instructions"
            }
        };

        // Act
        var response = await _client.PostAsJsonAsync($"/v1/agents/{agent.AgentId}:version", versionRequest);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errorResponse = await response.Content.ReadAsStringAsync();
        Assert.Contains("name is required", errorResponse);
    }

    [Fact]
    public async Task CreateAgentVersion_WithInvalidSpec_MissingInstructions_ReturnsBadRequest()
    {
        // Arrange
        var agent = await CreateTestAgent("Test Agent");
        var versionRequest = new CreateAgentVersionRequest
        {
            Version = "1.0.0",
            Spec = new Agent
            {
                AgentId = agent.AgentId,
                Name = "Test Agent",
                Instructions = "" // Invalid: empty instructions
            }
        };

        // Act
        var response = await _client.PostAsJsonAsync($"/v1/agents/{agent.AgentId}:version", versionRequest);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errorResponse = await response.Content.ReadAsStringAsync();
        Assert.Contains("instructions are required", errorResponse);
    }

    [Fact]
    public async Task CreateAgentVersion_WithInvalidSpec_InvalidBudget_ReturnsBadRequest()
    {
        // Arrange
        var agent = await CreateTestAgent("Test Agent");
        var versionRequest = new CreateAgentVersionRequest
        {
            Version = "1.0.0",
            Spec = new Agent
            {
                AgentId = agent.AgentId,
                Name = "Test Agent",
                Instructions = "Test instructions",
                Budget = new AgentBudget
                {
                    MaxTokens = -1 // Invalid: negative tokens
                }
            }
        };

        // Act
        var response = await _client.PostAsJsonAsync($"/v1/agents/{agent.AgentId}:version", versionRequest);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errorResponse = await response.Content.ReadAsStringAsync();
        Assert.Contains("MaxTokens must be greater than 0", errorResponse);
    }

    [Fact]
    public async Task CreateAgentVersion_WithInvalidSpec_InvalidConnector_ReturnsBadRequest()
    {
        // Arrange
        var agent = await CreateTestAgent("Test Agent");
        var versionRequest = new CreateAgentVersionRequest
        {
            Version = "1.0.0",
            Spec = new Agent
            {
                AgentId = agent.AgentId,
                Name = "Test Agent",
                Instructions = "Test instructions",
                Input = new ConnectorConfiguration
                {
                    Type = "invalid-connector" // Invalid connector type
                }
            }
        };

        // Act
        var response = await _client.PostAsJsonAsync($"/v1/agents/{agent.AgentId}:version", versionRequest);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errorResponse = await response.Content.ReadAsStringAsync();
        Assert.Contains("is not recognized", errorResponse);
    }

    [Fact]
    public async Task CreateAgentVersion_WithValidSpec_AllFields_ReturnsCreated()
    {
        // Arrange
        var agent = await CreateTestAgent("Invoice Classifier");
        var versionRequest = new CreateAgentVersionRequest
        {
            Version = "1.0.0",
            Spec = new Agent
            {
                AgentId = agent.AgentId,
                Name = "Invoice Classifier",
                Instructions = "Classify invoices",
                Budget = new AgentBudget
                {
                    MaxTokens = 4000,
                    MaxDurationSeconds = 60
                },
                Input = new ConnectorConfiguration { Type = "service-bus" },
                Output = new ConnectorConfiguration { Type = "http" },
                Tools = new List<string> { "http-post" }
            }
        };

        // Act
        var response = await _client.PostAsJsonAsync($"/v1/agents/{agent.AgentId}:version", versionRequest);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var version = await response.Content.ReadFromJsonAsync<AgentVersionResponse>();
        Assert.NotNull(version);
        Assert.Equal("1.0.0", version.Version);
    }

    private async Task<Agent> CreateTestAgent(string name)
    {
        var request = new CreateAgentRequest
        {
            Name = name,
            Instructions = "Test instructions"
        };
        var response = await _client.PostAsJsonAsync("/v1/agents", request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<Agent>())!;
    }

    private async Task<AgentVersionResponse> CreateTestVersion(string agentId, string version)
    {
        var request = new CreateAgentVersionRequest
        {
            Version = version,
            Spec = new Agent { AgentId = agentId, Name = "Test", Instructions = "Test" }
        };
        var response = await _client.PostAsJsonAsync($"/v1/agents/{agentId}:version", request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AgentVersionResponse>())!;
    }
}
