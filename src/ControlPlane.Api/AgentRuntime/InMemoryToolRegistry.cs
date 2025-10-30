using System.Collections.Concurrent;

namespace ControlPlane.Api.AgentRuntime;

/// <summary>
/// In-memory implementation of tool registry for managing agent tools
/// </summary>
public class InMemoryToolRegistry : IToolRegistry
{
    private readonly ConcurrentDictionary<string, ToolDefinition> _tools = new();
    private readonly ConcurrentDictionary<string, HashSet<string>> _agentTools = new();
    private readonly ILogger<InMemoryToolRegistry> _logger;

    public InMemoryToolRegistry(ILogger<InMemoryToolRegistry> logger)
    {
        _logger = logger;
    }

    public Task RegisterToolAsync(ToolDefinition tool, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tool.Name))
        {
            throw new ArgumentException("Tool name is required", nameof(tool));
        }

        _tools[tool.Name] = tool;
        _logger.LogInformation("Registered tool: {ToolName}", tool.Name);
        return Task.CompletedTask;
    }

    public Task<ToolDefinition?> GetToolAsync(string toolName, CancellationToken cancellationToken = default)
    {
        _tools.TryGetValue(toolName, out var tool);
        return Task.FromResult(tool);
    }

    public Task<IEnumerable<ToolDefinition>> GetToolsForAgentAsync(string agentId, CancellationToken cancellationToken = default)
    {
        if (!_agentTools.TryGetValue(agentId, out var toolNames))
        {
            return Task.FromResult(Enumerable.Empty<ToolDefinition>());
        }

        var tools = toolNames
            .Select(name => _tools.TryGetValue(name, out var tool) ? tool : null)
            .Where(tool => tool != null)
            .Cast<ToolDefinition>()
            .ToList();

        return Task.FromResult<IEnumerable<ToolDefinition>>(tools);
    }

    public Task<IEnumerable<ToolDefinition>> GetAllToolsAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IEnumerable<ToolDefinition>>(_tools.Values.ToList());
    }

    public Task<bool> UnregisterToolAsync(string toolName, CancellationToken cancellationToken = default)
    {
        var removed = _tools.TryRemove(toolName, out _);
        if (removed)
        {
            _logger.LogInformation("Unregistered tool: {ToolName}", toolName);

            // Remove from all agent associations
            foreach (var agentTools in _agentTools.Values)
            {
                agentTools.Remove(toolName);
            }
        }
        return Task.FromResult(removed);
    }

    public Task AssociateToolWithAgentAsync(string agentId, string toolName, CancellationToken cancellationToken = default)
    {
        if (!_tools.ContainsKey(toolName))
        {
            throw new InvalidOperationException($"Tool '{toolName}' is not registered");
        }

        var toolSet = _agentTools.GetOrAdd(agentId, _ => new HashSet<string>());
        toolSet.Add(toolName);

        _logger.LogInformation("Associated tool {ToolName} with agent {AgentId}", toolName, agentId);
        return Task.CompletedTask;
    }

    public Task DisassociateToolFromAgentAsync(string agentId, string toolName, CancellationToken cancellationToken = default)
    {
        if (_agentTools.TryGetValue(agentId, out var toolSet))
        {
            toolSet.Remove(toolName);
            _logger.LogInformation("Disassociated tool {ToolName} from agent {AgentId}", toolName, agentId);
        }
        return Task.CompletedTask;
    }
}
