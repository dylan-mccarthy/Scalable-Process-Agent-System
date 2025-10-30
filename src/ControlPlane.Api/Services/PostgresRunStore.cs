using ControlPlane.Api.Models;
using ControlPlane.Api.Data;
using ControlPlane.Api.Observability;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Diagnostics;

namespace ControlPlane.Api.Services;

public class PostgresRunStore : IRunStore
{
    private readonly ApplicationDbContext _context;

    public PostgresRunStore(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<Run>> GetAllRunsAsync()
    {
        var entities = await _context.Runs.ToListAsync();
        return entities.Select(MapToModel);
    }

    public async Task<Run?> GetRunAsync(string runId)
    {
        var entity = await _context.Runs.FindAsync(runId);
        return entity != null ? MapToModel(entity) : null;
    }

    public async Task<Run> CreateRunAsync(string agentId, string version)
    {
        using var activity = TelemetryConfig.ActivitySource.StartActivity("RunStore.CreateRun");
        activity?.SetTag("agent.id", agentId);
        activity?.SetTag("agent.version", version);

        var entity = new RunEntity
        {
            RunId = Guid.NewGuid().ToString(),
            AgentId = agentId,
            Version = version,
            Status = "pending",
            CreatedAt = DateTime.UtcNow
        };

        _context.Runs.Add(entity);
        await _context.SaveChangesAsync();

        var run = MapToModel(entity);
        activity?.SetTag("run.id", run.RunId);
        
        TelemetryConfig.RunsStartedCounter.Add(1, 
            new KeyValuePair<string, object?>("agent.id", agentId),
            new KeyValuePair<string, object?>("agent.version", version));

        return run;
    }

    public async Task<Run?> CompleteRunAsync(string runId, CompleteRunRequest request)
    {
        using var activity = TelemetryConfig.ActivitySource.StartActivity("RunStore.CompleteRun");
        activity?.SetTag("run.id", runId);

        var entity = await _context.Runs.FindAsync(runId);
        if (entity == null)
        {
            return null;
        }

        entity.Status = "completed";
        
        if (request.Timings != null)
        {
            entity.Timings = JsonSerializer.Serialize(request.Timings);
            
            // Record run duration metric
            if (request.Timings.TryGetValue("duration", out var duration))
            {
                var durationMs = Convert.ToDouble(duration);
                TelemetryConfig.RunDurationHistogram.Record(durationMs,
                    new KeyValuePair<string, object?>("agent.id", entity.AgentId),
                    new KeyValuePair<string, object?>("status", "completed"));
                activity?.SetTag("run.duration_ms", durationMs);
            }
        }

        if (request.Costs != null)
        {
            entity.Costs = JsonSerializer.Serialize(request.Costs);
            
            // Record token and cost metrics
            if (request.Costs.TryGetValue("tokens", out var tokens))
            {
                var tokenCount = Convert.ToInt64(tokens);
                TelemetryConfig.RunTokensHistogram.Record(tokenCount,
                    new KeyValuePair<string, object?>("agent.id", entity.AgentId));
                activity?.SetTag("run.tokens", tokenCount);
            }
            
            if (request.Costs.TryGetValue("usd", out var usd))
            {
                var costUsd = Convert.ToDouble(usd);
                TelemetryConfig.RunCostHistogram.Record(costUsd,
                    new KeyValuePair<string, object?>("agent.id", entity.AgentId));
                activity?.SetTag("run.cost_usd", costUsd);
            }
        }

        await _context.SaveChangesAsync();

        TelemetryConfig.RunsCompletedCounter.Add(1,
            new KeyValuePair<string, object?>("agent.id", entity.AgentId));

        return MapToModel(entity);
    }

    public async Task<Run?> FailRunAsync(string runId, FailRunRequest request)
    {
        using var activity = TelemetryConfig.ActivitySource.StartActivity("RunStore.FailRun");
        activity?.SetTag("run.id", runId);

        var entity = await _context.Runs.FindAsync(runId);
        if (entity == null)
        {
            return null;
        }

        entity.Status = "failed";
        
        var errorInfo = new Dictionary<string, object>
        {
            ["errorMessage"] = request.ErrorMessage
        };
        
        if (request.ErrorDetails != null)
        {
            errorInfo["errorDetails"] = request.ErrorDetails;
        }
        
        entity.ErrorInfo = JsonSerializer.Serialize(errorInfo);
        activity?.SetTag("run.error", request.ErrorMessage);

        if (request.Timings != null)
        {
            entity.Timings = JsonSerializer.Serialize(request.Timings);
            
            // Record run duration metric even for failed runs
            if (request.Timings.TryGetValue("duration", out var duration))
            {
                var durationMs = Convert.ToDouble(duration);
                TelemetryConfig.RunDurationHistogram.Record(durationMs,
                    new KeyValuePair<string, object?>("agent.id", entity.AgentId),
                    new KeyValuePair<string, object?>("status", "failed"));
                activity?.SetTag("run.duration_ms", durationMs);
            }
        }

        await _context.SaveChangesAsync();

        TelemetryConfig.RunsFailedCounter.Add(1,
            new KeyValuePair<string, object?>("agent.id", entity.AgentId),
            new KeyValuePair<string, object?>("error.type", request.ErrorMessage));

        return MapToModel(entity);
    }

    public async Task<Run?> CancelRunAsync(string runId, CancelRunRequest request)
    {
        using var activity = TelemetryConfig.ActivitySource.StartActivity("RunStore.CancelRun");
        activity?.SetTag("run.id", runId);
        activity?.SetTag("cancel.reason", request.Reason);
        var entity = await _context.Runs.FindAsync(runId);
        if (entity == null)
        {
            return null;
        }

        entity.Status = "cancelled";
        
        var errorInfo = new Dictionary<string, object>
        {
            ["reason"] = request.Reason
        };
        
        entity.ErrorInfo = JsonSerializer.Serialize(errorInfo);

        await _context.SaveChangesAsync();

        TelemetryConfig.RunsCancelledCounter.Add(1,
            new KeyValuePair<string, object?>("agent.id", entity.AgentId),
            new KeyValuePair<string, object?>("cancel.reason", request.Reason));

        return MapToModel(entity);
    }

    private static Run MapToModel(RunEntity entity)
    {
        return new Run
        {
            RunId = entity.RunId,
            AgentId = entity.AgentId,
            Version = entity.Version,
            DeploymentId = entity.DepId,
            NodeId = entity.NodeId,
            InputRef = entity.InputRef != null 
                ? JsonSerializer.Deserialize<Dictionary<string, object>>(entity.InputRef) 
                : null,
            Status = entity.Status,
            Timings = entity.Timings != null 
                ? JsonSerializer.Deserialize<Dictionary<string, object>>(entity.Timings) 
                : null,
            Costs = entity.Costs != null 
                ? JsonSerializer.Deserialize<Dictionary<string, object>>(entity.Costs) 
                : null,
            ErrorInfo = entity.ErrorInfo != null 
                ? JsonSerializer.Deserialize<Dictionary<string, object>>(entity.ErrorInfo) 
                : null,
            TraceId = entity.TraceId,
            CreatedAt = entity.CreatedAt
        };
    }
}
