using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ControlPlane.Api.Models;
using ControlPlane.Api.Services;

namespace ControlPlane.Api.Tests;

public class DeploymentEndpointsTests : IAsyncLifetime
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
                                    d.ServiceType == typeof(IRunStore) ||
                                    d.ServiceType == typeof(IDeploymentStore))
                        .ToList();

                    foreach (var descriptor in descriptorsToRemove)
                    {
                        services.Remove(descriptor);
                    }

                    // Add in-memory stores
                    services.AddSingleton<IAgentStore, InMemoryAgentStore>();
                    services.AddSingleton<INodeStore, InMemoryNodeStore>();
                    services.AddSingleton<IRunStore, InMemoryRunStore>();
                    services.AddSingleton<IDeploymentStore, InMemoryDeploymentStore>();
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
    public async Task GetDeployments_ReturnsEmptyList_Initially()
    {
        // Act
        var response = await _client.GetAsync("/v1/deployments");

        // Assert
        response.EnsureSuccessStatusCode();
        var deployments = await response.Content.ReadFromJsonAsync<List<Deployment>>();
        Assert.NotNull(deployments);
        Assert.Empty(deployments);
    }

    [Fact]
    public async Task CreateDeployment_ReturnsCreatedDeployment()
    {
        // Arrange - Create agent and version first
        var agentRequest = new CreateAgentRequest
        {
            Name = "Invoice Classifier",
            Instructions = "Classify invoices by vendor"
        };
        var agentResponse = await _client.PostAsJsonAsync("/v1/agents", agentRequest);
        agentResponse.EnsureSuccessStatusCode();
        var agent = await agentResponse.Content.ReadFromJsonAsync<Agent>();

        var versionRequest = new CreateAgentVersionRequest
        {
            Version = "1.0.0",
            Spec = agent
        };
        var versionResponse = await _client.PostAsJsonAsync($"/v1/agents/{agent!.AgentId}:version", versionRequest);
        versionResponse.EnsureSuccessStatusCode();

        var deploymentRequest = new CreateDeploymentRequest
        {
            AgentId = agent.AgentId,
            Version = "1.0.0",
            Env = "production",
            Target = new DeploymentTarget
            {
                Replicas = 3,
                Placement = new Dictionary<string, object>
                {
                    ["region"] = "us-east-1",
                    ["environment"] = "production"
                }
            }
        };

        // Act
        var response = await _client.PostAsJsonAsync("/v1/deployments", deploymentRequest);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var deployment = await response.Content.ReadFromJsonAsync<Deployment>();
        Assert.NotNull(deployment);
        Assert.NotEmpty(deployment.DepId);
        Assert.Equal(agent.AgentId, deployment.AgentId);
        Assert.Equal("1.0.0", deployment.Version);
        Assert.Equal("production", deployment.Env);
        Assert.NotNull(deployment.Target);
        Assert.Equal(3, deployment.Target.Replicas);
        Assert.NotNull(deployment.Target.Placement);
        Assert.True(deployment.Target.Placement.ContainsKey("region"));
        Assert.Equal("us-east-1", deployment.Target.Placement["region"].ToString());
        Assert.NotNull(deployment.Status);
        Assert.Equal("pending", deployment.Status.State);
        Assert.Equal(0, deployment.Status.ReadyReplicas);
    }

    [Fact]
    public async Task CreateDeployment_WithoutAgentId_ReturnsBadRequest()
    {
        // Arrange
        var request = new CreateDeploymentRequest
        {
            AgentId = "",
            Version = "1.0.0",
            Env = "production"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/v1/deployments", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateDeployment_WithoutVersion_ReturnsBadRequest()
    {
        // Arrange
        var request = new CreateDeploymentRequest
        {
            AgentId = "test-agent",
            Version = "",
            Env = "production"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/v1/deployments", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateDeployment_WithoutEnv_ReturnsBadRequest()
    {
        // Arrange
        var request = new CreateDeploymentRequest
        {
            AgentId = "test-agent",
            Version = "1.0.0",
            Env = ""
        };

        // Act
        var response = await _client.PostAsJsonAsync("/v1/deployments", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateDeployment_WithNonexistentVersion_ReturnsNotFound()
    {
        // Arrange
        var request = new CreateDeploymentRequest
        {
            AgentId = "nonexistent-agent",
            Version = "1.0.0",
            Env = "production"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/v1/deployments", request);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetDeployment_ReturnsDeployment()
    {
        // Arrange - Create agent, version, and deployment
        var agentRequest = new CreateAgentRequest
        {
            Name = "Test Agent",
            Instructions = "Test instructions"
        };
        var agentResponse = await _client.PostAsJsonAsync("/v1/agents", agentRequest);
        var agent = await agentResponse.Content.ReadFromJsonAsync<Agent>();

        var versionRequest = new CreateAgentVersionRequest
        {
            Version = "1.0.0",
            Spec = agent
        };
        await _client.PostAsJsonAsync($"/v1/agents/{agent!.AgentId}:version", versionRequest);

        var deploymentRequest = new CreateDeploymentRequest
        {
            AgentId = agent.AgentId,
            Version = "1.0.0",
            Env = "staging"
        };
        var createResponse = await _client.PostAsJsonAsync("/v1/deployments", deploymentRequest);
        var createdDeployment = await createResponse.Content.ReadFromJsonAsync<Deployment>();

        // Act
        var response = await _client.GetAsync($"/v1/deployments/{createdDeployment!.DepId}");

        // Assert
        response.EnsureSuccessStatusCode();
        var deployment = await response.Content.ReadFromJsonAsync<Deployment>();
        Assert.NotNull(deployment);
        Assert.Equal(createdDeployment.DepId, deployment.DepId);
        Assert.Equal(agent.AgentId, deployment.AgentId);
        Assert.Equal("1.0.0", deployment.Version);
        Assert.Equal("staging", deployment.Env);
    }

    [Fact]
    public async Task GetDeployment_WithNonexistentId_ReturnsNotFound()
    {
        // Act
        var response = await _client.GetAsync("/v1/deployments/nonexistent-id");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetAgentDeployments_ReturnsDeploymentsForAgent()
    {
        // Arrange - Create agent, version, and multiple deployments
        var agentRequest = new CreateAgentRequest
        {
            Name = "Multi-Deploy Agent",
            Instructions = "Test instructions"
        };
        var agentResponse = await _client.PostAsJsonAsync("/v1/agents", agentRequest);
        var agent = await agentResponse.Content.ReadFromJsonAsync<Agent>();

        var versionRequest = new CreateAgentVersionRequest
        {
            Version = "1.0.0",
            Spec = agent
        };
        await _client.PostAsJsonAsync($"/v1/agents/{agent!.AgentId}:version", versionRequest);

        // Create deployments for different environments
        var prodDeployment = new CreateDeploymentRequest
        {
            AgentId = agent.AgentId,
            Version = "1.0.0",
            Env = "production"
        };
        await _client.PostAsJsonAsync("/v1/deployments", prodDeployment);

        var stagingDeployment = new CreateDeploymentRequest
        {
            AgentId = agent.AgentId,
            Version = "1.0.0",
            Env = "staging"
        };
        await _client.PostAsJsonAsync("/v1/deployments", stagingDeployment);

        // Act
        var response = await _client.GetAsync($"/v1/agents/{agent.AgentId}/deployments");

        // Assert
        response.EnsureSuccessStatusCode();
        var deployments = await response.Content.ReadFromJsonAsync<List<Deployment>>();
        Assert.NotNull(deployments);
        Assert.Equal(2, deployments.Count);
        Assert.All(deployments, d => Assert.Equal(agent.AgentId, d.AgentId));
    }

    [Fact]
    public async Task UpdateDeploymentStatus_UpdatesStatus()
    {
        // Arrange - Create agent, version, and deployment
        var agentRequest = new CreateAgentRequest
        {
            Name = "Status Update Agent",
            Instructions = "Test instructions"
        };
        var agentResponse = await _client.PostAsJsonAsync("/v1/agents", agentRequest);
        var agent = await agentResponse.Content.ReadFromJsonAsync<Agent>();

        var versionRequest = new CreateAgentVersionRequest
        {
            Version = "1.0.0",
            Spec = agent
        };
        await _client.PostAsJsonAsync($"/v1/agents/{agent!.AgentId}:version", versionRequest);

        var deploymentRequest = new CreateDeploymentRequest
        {
            AgentId = agent.AgentId,
            Version = "1.0.0",
            Env = "production",
            Target = new DeploymentTarget { Replicas = 3 }
        };
        var createResponse = await _client.PostAsJsonAsync("/v1/deployments", deploymentRequest);
        var createdDeployment = await createResponse.Content.ReadFromJsonAsync<Deployment>();

        var statusUpdate = new UpdateDeploymentStatusRequest
        {
            Status = new DeploymentStatus
            {
                State = "active",
                ReadyReplicas = 3,
                Message = "All replicas ready"
            }
        };

        // Act
        var response = await _client.PutAsJsonAsync($"/v1/deployments/{createdDeployment!.DepId}", statusUpdate);

        // Assert
        response.EnsureSuccessStatusCode();
        var deployment = await response.Content.ReadFromJsonAsync<Deployment>();
        Assert.NotNull(deployment);
        Assert.NotNull(deployment.Status);
        Assert.Equal("active", deployment.Status.State);
        Assert.Equal(3, deployment.Status.ReadyReplicas);
        Assert.Equal("All replicas ready", deployment.Status.Message);
        Assert.NotNull(deployment.Status.LastUpdated);
    }

    [Fact]
    public async Task UpdateDeploymentStatus_WithNonexistentId_ReturnsNotFound()
    {
        // Arrange
        var statusUpdate = new UpdateDeploymentStatusRequest
        {
            Status = new DeploymentStatus { State = "active" }
        };

        // Act
        var response = await _client.PutAsJsonAsync("/v1/deployments/nonexistent-id", statusUpdate);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeleteDeployment_DeletesDeployment()
    {
        // Arrange - Create agent, version, and deployment
        var agentRequest = new CreateAgentRequest
        {
            Name = "Delete Agent",
            Instructions = "Test instructions"
        };
        var agentResponse = await _client.PostAsJsonAsync("/v1/agents", agentRequest);
        var agent = await agentResponse.Content.ReadFromJsonAsync<Agent>();

        var versionRequest = new CreateAgentVersionRequest
        {
            Version = "1.0.0",
            Spec = agent
        };
        await _client.PostAsJsonAsync($"/v1/agents/{agent!.AgentId}:version", versionRequest);

        var deploymentRequest = new CreateDeploymentRequest
        {
            AgentId = agent.AgentId,
            Version = "1.0.0",
            Env = "development"
        };
        var createResponse = await _client.PostAsJsonAsync("/v1/deployments", deploymentRequest);
        var createdDeployment = await createResponse.Content.ReadFromJsonAsync<Deployment>();

        // Act
        var deleteResponse = await _client.DeleteAsync($"/v1/deployments/{createdDeployment!.DepId}");

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        // Verify deployment is deleted
        var getResponse = await _client.GetAsync($"/v1/deployments/{createdDeployment.DepId}");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task DeleteDeployment_WithNonexistentId_ReturnsNotFound()
    {
        // Act
        var response = await _client.DeleteAsync("/v1/deployments/nonexistent-id");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreateDeployment_WithDefaultReplicas_SetsReplicasToOne()
    {
        // Arrange - Create agent and version
        var agentRequest = new CreateAgentRequest
        {
            Name = "Default Replicas Agent",
            Instructions = "Test instructions"
        };
        var agentResponse = await _client.PostAsJsonAsync("/v1/agents", agentRequest);
        var agent = await agentResponse.Content.ReadFromJsonAsync<Agent>();

        var versionRequest = new CreateAgentVersionRequest
        {
            Version = "1.0.0",
            Spec = agent
        };
        await _client.PostAsJsonAsync($"/v1/agents/{agent!.AgentId}:version", versionRequest);

        var deploymentRequest = new CreateDeploymentRequest
        {
            AgentId = agent.AgentId,
            Version = "1.0.0",
            Env = "production"
            // No Target specified
        };

        // Act
        var response = await _client.PostAsJsonAsync("/v1/deployments", deploymentRequest);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var deployment = await response.Content.ReadFromJsonAsync<Deployment>();
        Assert.NotNull(deployment);
        Assert.NotNull(deployment.Target);
        Assert.Equal(1, deployment.Target.Replicas);
    }

    [Fact]
    public async Task CreateDeployment_WithPlacementConstraints_StoresConstraints()
    {
        // Arrange - Create agent and version
        var agentRequest = new CreateAgentRequest
        {
            Name = "Placement Agent",
            Instructions = "Test instructions"
        };
        var agentResponse = await _client.PostAsJsonAsync("/v1/agents", agentRequest);
        var agent = await agentResponse.Content.ReadFromJsonAsync<Agent>();

        var versionRequest = new CreateAgentVersionRequest
        {
            Version = "1.0.0",
            Spec = agent
        };
        await _client.PostAsJsonAsync($"/v1/agents/{agent!.AgentId}:version", versionRequest);

        var deploymentRequest = new CreateDeploymentRequest
        {
            AgentId = agent.AgentId,
            Version = "1.0.0",
            Env = "production",
            Target = new DeploymentTarget
            {
                Replicas = 5,
                Placement = new Dictionary<string, object>
                {
                    ["region"] = "eu-west-1",
                    ["environment"] = "production",
                    ["zone"] = "a"
                }
            }
        };

        // Act
        var response = await _client.PostAsJsonAsync("/v1/deployments", deploymentRequest);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var deployment = await response.Content.ReadFromJsonAsync<Deployment>();
        Assert.NotNull(deployment);
        Assert.NotNull(deployment.Target);
        Assert.NotNull(deployment.Target.Placement);
        Assert.Equal(3, deployment.Target.Placement.Count);
        Assert.Equal("eu-west-1", deployment.Target.Placement["region"].ToString());
        Assert.Equal("production", deployment.Target.Placement["environment"].ToString());
        Assert.Equal("a", deployment.Target.Placement["zone"].ToString());
    }

    [Fact]
    public async Task GetAllDeployments_ReturnsAllDeployments()
    {
        // Arrange - Create multiple agents and deployments
        var agent1Request = new CreateAgentRequest
        {
            Name = "Agent 1",
            Instructions = "Test instructions"
        };
        var agent1Response = await _client.PostAsJsonAsync("/v1/agents", agent1Request);
        var agent1 = await agent1Response.Content.ReadFromJsonAsync<Agent>();

        var version1Request = new CreateAgentVersionRequest
        {
            Version = "1.0.0",
            Spec = agent1
        };
        await _client.PostAsJsonAsync($"/v1/agents/{agent1!.AgentId}:version", version1Request);

        var agent2Request = new CreateAgentRequest
        {
            Name = "Agent 2",
            Instructions = "Test instructions"
        };
        var agent2Response = await _client.PostAsJsonAsync("/v1/agents", agent2Request);
        var agent2 = await agent2Response.Content.ReadFromJsonAsync<Agent>();

        var version2Request = new CreateAgentVersionRequest
        {
            Version = "1.0.0",
            Spec = agent2
        };
        await _client.PostAsJsonAsync($"/v1/agents/{agent2!.AgentId}:version", version2Request);

        // Create deployments
        await _client.PostAsJsonAsync("/v1/deployments", new CreateDeploymentRequest
        {
            AgentId = agent1.AgentId,
            Version = "1.0.0",
            Env = "production"
        });

        await _client.PostAsJsonAsync("/v1/deployments", new CreateDeploymentRequest
        {
            AgentId = agent2.AgentId,
            Version = "1.0.0",
            Env = "staging"
        });

        // Act
        var response = await _client.GetAsync("/v1/deployments");

        // Assert
        response.EnsureSuccessStatusCode();
        var deployments = await response.Content.ReadFromJsonAsync<List<Deployment>>();
        Assert.NotNull(deployments);
        Assert.Equal(2, deployments.Count);
    }
}
