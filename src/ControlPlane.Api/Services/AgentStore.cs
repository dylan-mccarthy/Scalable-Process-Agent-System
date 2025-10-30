using ControlPlane.Api.Models;
using System.Collections.Concurrent;

namespace ControlPlane.Api.Services;

public interface IAgentStore
{
    Task<Agent?> GetAgentAsync(string agentId);
    Task<IEnumerable<Agent>> GetAllAgentsAsync();
    Task<Agent> CreateAgentAsync(CreateAgentRequest request);
    Task<Agent?> UpdateAgentAsync(string agentId, UpdateAgentRequest request);
    Task<bool> DeleteAgentAsync(string agentId);
}

public class InMemoryAgentStore : IAgentStore
{
    private readonly ConcurrentDictionary<string, Agent> _agents = new();

    public Task<Agent?> GetAgentAsync(string agentId)
    {
        _agents.TryGetValue(agentId, out var agent);
        return Task.FromResult(agent);
    }

    public Task<IEnumerable<Agent>> GetAllAgentsAsync()
    {
        return Task.FromResult<IEnumerable<Agent>>(_agents.Values.ToList());
    }

    public Task<Agent> CreateAgentAsync(CreateAgentRequest request)
    {
        var agent = new Agent
        {
            AgentId = Guid.NewGuid().ToString(),
            Name = request.Name,
            Description = request.Description,
            Instructions = request.Instructions,
            ModelProfile = request.ModelProfile,
            Budget = request.Budget,
            Tools = request.Tools,
            Input = request.Input,
            Output = request.Output,
            Metadata = request.Metadata
        };

        _agents[agent.AgentId] = agent;
        return Task.FromResult(agent);
    }

    public Task<Agent?> UpdateAgentAsync(string agentId, UpdateAgentRequest request)
    {
        if (!_agents.TryGetValue(agentId, out var agent))
        {
            return Task.FromResult<Agent?>(null);
        }

        if (request.Name != null)
            agent.Name = request.Name;
        if (request.Description != null)
            agent.Description = request.Description;
        if (request.Instructions != null)
            agent.Instructions = request.Instructions;
        if (request.ModelProfile != null)
            agent.ModelProfile = request.ModelProfile;
        if (request.Budget != null)
            agent.Budget = request.Budget;
        if (request.Tools != null)
            agent.Tools = request.Tools;
        if (request.Input != null)
            agent.Input = request.Input;
        if (request.Output != null)
            agent.Output = request.Output;
        if (request.Metadata != null)
            agent.Metadata = request.Metadata;

        _agents[agentId] = agent;
        return Task.FromResult<Agent?>(agent);
    }

    public Task<bool> DeleteAgentAsync(string agentId)
    {
        return Task.FromResult(_agents.TryRemove(agentId, out _));
    }
}
