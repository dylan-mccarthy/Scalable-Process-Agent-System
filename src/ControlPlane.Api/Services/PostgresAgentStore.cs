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
            Instructions = request.Instructions,
            ModelProfile = request.ModelProfile != null 
                ? JsonSerializer.Serialize(request.ModelProfile) 
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

        if (request.Instructions != null)
        {
            entity.Instructions = request.Instructions;
        }

        if (request.ModelProfile != null)
        {
            entity.ModelProfile = JsonSerializer.Serialize(request.ModelProfile);
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
            Instructions = entity.Instructions,
            ModelProfile = entity.ModelProfile != null 
                ? JsonSerializer.Deserialize<Dictionary<string, object>>(entity.ModelProfile) 
                : null
        };
    }
}
