using ControlPlane.Api.Models;
using ControlPlane.Api.Data;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace ControlPlane.Api.Services;

public class PostgresAgentStore : IAgentStore
{
    private readonly ApplicationDbContext _context;

    public PostgresAgentStore(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<Agent>> GetAllAgentsAsync()
    {
        var entities = await _context.Agents.ToListAsync();
        return entities.Select(MapToModel);
    }

    public async Task<Agent?> GetAgentAsync(string agentId)
    {
        var entity = await _context.Agents.FindAsync(agentId);
        return entity != null ? MapToModel(entity) : null;
    }

    public async Task<Agent> CreateAgentAsync(CreateAgentRequest request)
    {
        var entity = new AgentEntity
        {
            AgentId = Guid.NewGuid().ToString(),
            Name = request.Name,
            Description = request.Description,
            Instructions = request.Instructions,
            ModelProfile = request.ModelProfile != null 
                ? JsonSerializer.Serialize(request.ModelProfile) 
                : null,
            Budget = request.Budget != null
                ? JsonSerializer.Serialize(request.Budget)
                : null,
            Tools = request.Tools != null
                ? JsonSerializer.Serialize(request.Tools)
                : null,
            InputConnector = request.Input != null
                ? JsonSerializer.Serialize(request.Input)
                : null,
            OutputConnector = request.Output != null
                ? JsonSerializer.Serialize(request.Output)
                : null,
            Metadata = request.Metadata != null
                ? JsonSerializer.Serialize(request.Metadata)
                : null
        };

        _context.Agents.Add(entity);
        await _context.SaveChangesAsync();

        return MapToModel(entity);
    }

    public async Task<Agent?> UpdateAgentAsync(string agentId, UpdateAgentRequest request)
    {
        var entity = await _context.Agents.FindAsync(agentId);
        if (entity == null)
        {
            return null;
        }

        if (request.Name != null)
        {
            entity.Name = request.Name;
        }

        if (request.Description != null)
        {
            entity.Description = request.Description;
        }

        if (request.Instructions != null)
        {
            entity.Instructions = request.Instructions;
        }

        if (request.ModelProfile != null)
        {
            entity.ModelProfile = JsonSerializer.Serialize(request.ModelProfile);
        }

        if (request.Budget != null)
        {
            entity.Budget = JsonSerializer.Serialize(request.Budget);
        }

        if (request.Tools != null)
        {
            entity.Tools = JsonSerializer.Serialize(request.Tools);
        }

        if (request.Input != null)
        {
            entity.InputConnector = JsonSerializer.Serialize(request.Input);
        }

        if (request.Output != null)
        {
            entity.OutputConnector = JsonSerializer.Serialize(request.Output);
        }

        if (request.Metadata != null)
        {
            entity.Metadata = JsonSerializer.Serialize(request.Metadata);
        }

        await _context.SaveChangesAsync();

        return MapToModel(entity);
    }

    public async Task<bool> DeleteAgentAsync(string agentId)
    {
        var entity = await _context.Agents.FindAsync(agentId);
        if (entity == null)
        {
            return false;
        }

        _context.Agents.Remove(entity);
        await _context.SaveChangesAsync();

        return true;
    }

    private static Agent MapToModel(AgentEntity entity)
    {
        return new Agent
        {
            AgentId = entity.AgentId,
            Name = entity.Name,
            Description = entity.Description,
            Instructions = entity.Instructions,
            ModelProfile = entity.ModelProfile != null 
                ? JsonSerializer.Deserialize<Dictionary<string, object>>(entity.ModelProfile) 
                : null,
            Budget = entity.Budget != null
                ? JsonSerializer.Deserialize<AgentBudget>(entity.Budget)
                : null,
            Tools = entity.Tools != null
                ? JsonSerializer.Deserialize<List<string>>(entity.Tools)
                : null,
            Input = entity.InputConnector != null
                ? JsonSerializer.Deserialize<ConnectorConfiguration>(entity.InputConnector)
                : null,
            Output = entity.OutputConnector != null
                ? JsonSerializer.Deserialize<ConnectorConfiguration>(entity.OutputConnector)
                : null,
            Metadata = entity.Metadata != null
                ? JsonSerializer.Deserialize<Dictionary<string, string>>(entity.Metadata)
                : null
        };
    }
}
