using ControlPlane.Api.Data;
using ControlPlane.Api.Models;
using ControlPlane.Api.Observability;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Text.Json;

namespace ControlPlane.Api.Services;

/// <summary>
/// PostgreSQL implementation of deployment store.
/// </summary>
public class PostgresDeploymentStore : IDeploymentStore
{
    private readonly ApplicationDbContext _context;

    public PostgresDeploymentStore(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Deployment?> GetDeploymentAsync(string depId)
    {
        using var activity = TelemetryConfig.ActivitySource.StartActivity("DeploymentStore.GetDeployment");
        activity?.SetTag("dep.id", depId);

        var entity = await _context.Deployments
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.DepId == depId);

        return entity != null ? MapToModel(entity) : null;
    }

    public async Task<IEnumerable<Deployment>> GetAllDeploymentsAsync()
    {
        using var activity = TelemetryConfig.ActivitySource.StartActivity("DeploymentStore.GetAllDeployments");

        var entities = await _context.Deployments
            .AsNoTracking()
            .ToListAsync();

        return entities.Select(MapToModel).ToList();
    }

    public async Task<IEnumerable<Deployment>> GetDeploymentsByAgentAsync(string agentId)
    {
        using var activity = TelemetryConfig.ActivitySource.StartActivity("DeploymentStore.GetDeploymentsByAgent");
        activity?.SetTag("agent.id", agentId);

        var entities = await _context.Deployments
            .AsNoTracking()
            .Where(d => d.AgentId == agentId)
            .ToListAsync();

        return entities.Select(MapToModel).ToList();
    }

    public async Task<Deployment> CreateDeploymentAsync(CreateDeploymentRequest request)
    {
        using var activity = TelemetryConfig.ActivitySource.StartActivity("DeploymentStore.CreateDeployment");
        activity?.SetTag("agent.id", request.AgentId);
        activity?.SetTag("version", request.Version);
        activity?.SetTag("env", request.Env);

        var entity = new DeploymentEntity
        {
            DepId = Guid.NewGuid().ToString(),
            AgentId = request.AgentId,
            Version = request.Version,
            Env = request.Env,
            Target = JsonSerializer.Serialize(request.Target ?? new DeploymentTarget { Replicas = 1 }),
            Status = JsonSerializer.Serialize(new DeploymentStatus
            {
                State = "pending",
                ReadyReplicas = 0,
                LastUpdated = DateTime.UtcNow
            })
        };

        _context.Deployments.Add(entity);
        await _context.SaveChangesAsync();

        return MapToModel(entity);
    }

    public async Task<Deployment?> UpdateDeploymentStatusAsync(string depId, UpdateDeploymentStatusRequest request)
    {
        using var activity = TelemetryConfig.ActivitySource.StartActivity("DeploymentStore.UpdateDeploymentStatus");
        activity?.SetTag("dep.id", depId);

        var entity = await _context.Deployments.FirstOrDefaultAsync(d => d.DepId == depId);
        if (entity == null)
        {
            return null;
        }

        if (request.Status != null)
        {
            request.Status.LastUpdated = DateTime.UtcNow;
            entity.Status = JsonSerializer.Serialize(request.Status);
        }

        await _context.SaveChangesAsync();

        return MapToModel(entity);
    }

    public async Task<bool> DeleteDeploymentAsync(string depId)
    {
        using var activity = TelemetryConfig.ActivitySource.StartActivity("DeploymentStore.DeleteDeployment");
        activity?.SetTag("dep.id", depId);

        var entity = await _context.Deployments.FirstOrDefaultAsync(d => d.DepId == depId);
        if (entity == null)
        {
            return false;
        }

        _context.Deployments.Remove(entity);
        await _context.SaveChangesAsync();

        return true;
    }

    private static Deployment MapToModel(DeploymentEntity entity)
    {
        return new Deployment
        {
            DepId = entity.DepId,
            AgentId = entity.AgentId,
            Version = entity.Version,
            Env = entity.Env,
            Target = string.IsNullOrEmpty(entity.Target)
                ? null
                : JsonSerializer.Deserialize<DeploymentTarget>(entity.Target),
            Status = string.IsNullOrEmpty(entity.Status)
                ? null
                : JsonSerializer.Deserialize<DeploymentStatus>(entity.Status)
        };
    }
}
