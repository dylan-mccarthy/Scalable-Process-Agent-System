using ControlPlane.Api.Models;
using System.Collections.Concurrent;

namespace ControlPlane.Api.Services;

public interface INodeStore
{
    Task<Node?> GetNodeAsync(string nodeId);
    Task<IEnumerable<Node>> GetAllNodesAsync();
    Task<Node> RegisterNodeAsync(RegisterNodeRequest request);
    Task<Node?> UpdateHeartbeatAsync(string nodeId, HeartbeatRequest request);
    Task<bool> DeleteNodeAsync(string nodeId);
}

public class InMemoryNodeStore : INodeStore
{
    private readonly ConcurrentDictionary<string, Node> _nodes = new();

    public Task<Node?> GetNodeAsync(string nodeId)
    {
        _nodes.TryGetValue(nodeId, out var node);
        return Task.FromResult(node);
    }

    public Task<IEnumerable<Node>> GetAllNodesAsync()
    {
        return Task.FromResult<IEnumerable<Node>>(_nodes.Values.ToList());
    }

    public Task<Node> RegisterNodeAsync(RegisterNodeRequest request)
    {
        // Extract slots from capacity
        int availableSlots = 8; // default
        if (request.Capacity?.TryGetValue("slots", out var slotsObj) == true && slotsObj != null)
        {
            if (slotsObj is System.Text.Json.JsonElement jsonElement)
            {
                if (jsonElement.ValueKind == System.Text.Json.JsonValueKind.Number)
                {
                    availableSlots = jsonElement.GetInt32();
                }
            }
            else if (slotsObj is int intSlots)
            {
                availableSlots = intSlots;
            }
            else if (slotsObj is long longSlots)
            {
                availableSlots = (int)longSlots;
            }
            else
            {
                // Try to parse as string or convert
                availableSlots = Convert.ToInt32(slotsObj);
            }
        }

        var node = new Node
        {
            NodeId = request.NodeId,
            Metadata = request.Metadata,
            Capacity = request.Capacity,
            Status = new NodeStatus
            {
                State = "active",
                ActiveRuns = 0,
                AvailableSlots = availableSlots
            },
            HeartbeatAt = DateTime.UtcNow
        };

        _nodes[node.NodeId] = node;
        return Task.FromResult(node);
    }

    public Task<Node?> UpdateHeartbeatAsync(string nodeId, HeartbeatRequest request)
    {
        if (!_nodes.TryGetValue(nodeId, out var node))
        {
            return Task.FromResult<Node?>(null);
        }

        node.Status = request.Status;
        node.HeartbeatAt = DateTime.UtcNow;
        _nodes[nodeId] = node;
        return Task.FromResult<Node?>(node);
    }

    public Task<bool> DeleteNodeAsync(string nodeId)
    {
        return Task.FromResult(_nodes.TryRemove(nodeId, out _));
    }
}
