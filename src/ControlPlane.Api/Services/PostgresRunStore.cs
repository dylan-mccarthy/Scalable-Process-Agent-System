using ControlPlane.Api.Models;
using ControlPlane.Api.Data;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

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

        return MapToModel(entity);
    }

    public async Task<Run?> CompleteRunAsync(string runId, CompleteRunRequest request)
    {
        var entity = await _context.Runs.FindAsync(runId);
        if (entity == null)
        {
            return null;
        }

        entity.Status = "completed";
        
        if (request.Timings != null)
        {
            entity.Timings = JsonSerializer.Serialize(request.Timings);
        }

        if (request.Costs != null)
        {
            entity.Costs = JsonSerializer.Serialize(request.Costs);
        }

        await _context.SaveChangesAsync();

        return MapToModel(entity);
    }

    public async Task<Run?> FailRunAsync(string runId, FailRunRequest request)
    {
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

        if (request.Timings != null)
        {
            entity.Timings = JsonSerializer.Serialize(request.Timings);
        }

        await _context.SaveChangesAsync();

        return MapToModel(entity);
    }

    public async Task<Run?> CancelRunAsync(string runId, CancelRunRequest request)
    {
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
