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
    
    // Version management
    Task<AgentVersionResponse> CreateVersionAsync(string agentId, CreateAgentVersionRequest request);
    Task<AgentVersionResponse?> GetVersionAsync(string agentId, string version);
    Task<IEnumerable<AgentVersionResponse>> GetVersionsAsync(string agentId);
    Task<bool> DeleteVersionAsync(string agentId, string version);
}

public class InMemoryAgentStore : IAgentStore
{
    private readonly ConcurrentDictionary<string, Agent> _agents = new();
    private readonly ConcurrentDictionary<string, List<AgentVersionResponse>> _versions = new();

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
        var removed = _agents.TryRemove(agentId, out _);
        if (removed)
        {
            _versions.TryRemove(agentId, out _);
        }
        return Task.FromResult(removed);
    }

    public Task<AgentVersionResponse> CreateVersionAsync(string agentId, CreateAgentVersionRequest request)
    {
        if (!_agents.ContainsKey(agentId))
        {
            throw new InvalidOperationException($"Agent with ID {agentId} does not exist");
        }

        var versions = _versions.GetOrAdd(agentId, _ => new List<AgentVersionResponse>());
        
        if (versions.Any(v => v.Version == request.Version))
        {
            throw new InvalidOperationException($"Version {request.Version} already exists for agent {agentId}");
        }

        var versionResponse = new AgentVersionResponse
        {
            AgentId = agentId,
            Version = request.Version,
            Spec = request.Spec,
            CreatedAt = DateTime.UtcNow
        };

        versions.Add(versionResponse);
        return Task.FromResult(versionResponse);
    }

    public Task<AgentVersionResponse?> GetVersionAsync(string agentId, string version)
    {
        if (_versions.TryGetValue(agentId, out var versions))
        {
            var versionResponse = versions.FirstOrDefault(v => v.Version == version);
            return Task.FromResult(versionResponse);
        }
        return Task.FromResult<AgentVersionResponse?>(null);
    }

    public Task<IEnumerable<AgentVersionResponse>> GetVersionsAsync(string agentId)
    {
        if (_versions.TryGetValue(agentId, out var versions))
        {
            return Task.FromResult<IEnumerable<AgentVersionResponse>>(versions.OrderByDescending(v => v.CreatedAt).ToList());
        }
        return Task.FromResult<IEnumerable<AgentVersionResponse>>(new List<AgentVersionResponse>());
    }

    public Task<bool> DeleteVersionAsync(string agentId, string version)
    {
        if (_versions.TryGetValue(agentId, out var versions))
        {
            var versionToRemove = versions.FirstOrDefault(v => v.Version == version);
            if (versionToRemove != null)
            {
                versions.Remove(versionToRemove);
                return Task.FromResult(true);
            }
        }
        return Task.FromResult(false);
    }
}
