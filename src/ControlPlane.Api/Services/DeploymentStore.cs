using ControlPlane.Api.Models;
using System.Collections.Concurrent;

namespace ControlPlane.Api.Services;

/// <summary>
/// Interface for managing agent deployments.
/// </summary>
public interface IDeploymentStore
{
    Task<Deployment?> GetDeploymentAsync(string depId);
    Task<IEnumerable<Deployment>> GetAllDeploymentsAsync();
    Task<IEnumerable<Deployment>> GetDeploymentsByAgentAsync(string agentId);
    Task<Deployment> CreateDeploymentAsync(CreateDeploymentRequest request);
    Task<Deployment?> UpdateDeploymentStatusAsync(string depId, UpdateDeploymentStatusRequest request);
    Task<bool> DeleteDeploymentAsync(string depId);
}

/// <summary>
/// In-memory implementation of deployment store for testing.
/// </summary>
public class InMemoryDeploymentStore : IDeploymentStore
{
    private readonly ConcurrentDictionary<string, Deployment> _deployments = new();

    public Task<Deployment?> GetDeploymentAsync(string depId)
    {
        _deployments.TryGetValue(depId, out var deployment);
        return Task.FromResult(deployment);
    }

    public Task<IEnumerable<Deployment>> GetAllDeploymentsAsync()
    {
        return Task.FromResult<IEnumerable<Deployment>>(_deployments.Values.ToList());
    }

    public Task<IEnumerable<Deployment>> GetDeploymentsByAgentAsync(string agentId)
    {
        var deployments = _deployments.Values.Where(d => d.AgentId == agentId).ToList();
        return Task.FromResult<IEnumerable<Deployment>>(deployments);
    }

    public Task<Deployment> CreateDeploymentAsync(CreateDeploymentRequest request)
    {
        var deployment = new Deployment
        {
            DepId = Guid.NewGuid().ToString(),
            AgentId = request.AgentId,
            Version = request.Version,
            Env = request.Env,
            Target = request.Target ?? new DeploymentTarget { Replicas = 1 },
            Status = new DeploymentStatus
            {
                State = "pending",
                ReadyReplicas = 0,
                LastUpdated = DateTime.UtcNow
            }
        };

        _deployments[deployment.DepId] = deployment;
        return Task.FromResult(deployment);
    }

    public Task<Deployment?> UpdateDeploymentStatusAsync(string depId, UpdateDeploymentStatusRequest request)
    {
        if (!_deployments.TryGetValue(depId, out var deployment))
        {
            return Task.FromResult<Deployment?>(null);
        }

        if (request.Status != null)
        {
            deployment.Status = request.Status;
            deployment.Status.LastUpdated = DateTime.UtcNow;
        }

        return Task.FromResult<Deployment?>(deployment);
    }

    public Task<bool> DeleteDeploymentAsync(string depId)
    {
        return Task.FromResult(_deployments.TryRemove(depId, out _));
    }
}
