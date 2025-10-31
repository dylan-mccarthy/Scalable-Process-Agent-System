using ControlPlane.Api.Models;
using ControlPlane.Api.Data;
using ControlPlane.Api.Observability;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Diagnostics;

namespace ControlPlane.Api.Services;

public class PostgresNodeStore : INodeStore
{
    private readonly ApplicationDbContext _context;

    public PostgresNodeStore(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<Node>> GetAllNodesAsync()
    {
        var entities = await _context.Nodes.ToListAsync();
        return entities.Select(MapToModel);
    }

    public async Task<Node?> GetNodeAsync(string nodeId)
    {
        var entity = await _context.Nodes.FindAsync(nodeId);
        return entity != null ? MapToModel(entity) : null;
    }

    public async Task<Node> RegisterNodeAsync(RegisterNodeRequest request)
    {
        using var activity = TelemetryConfig.ActivitySource.StartActivity("NodeStore.RegisterNode");
        activity?.SetTag("node.id", request.NodeId);

        var entity = new NodeEntity
        {
            NodeId = request.NodeId,
            Metadata = request.Metadata != null
                ? JsonSerializer.Serialize(request.Metadata)
                : null,
            Capacity = request.Capacity != null
                ? JsonSerializer.Serialize(request.Capacity)
                : null,
            Status = JsonSerializer.Serialize(new NodeStatus()),
            HeartbeatAt = DateTime.UtcNow
        };

        _context.Nodes.Add(entity);
        await _context.SaveChangesAsync();

        TelemetryConfig.NodesRegisteredCounter.Add(1,
            new KeyValuePair<string, object?>("node.id", request.NodeId));

        return MapToModel(entity);
    }

    public async Task<Node?> UpdateHeartbeatAsync(string nodeId, HeartbeatRequest request)
    {
        var entity = await _context.Nodes.FindAsync(nodeId);
        if (entity == null)
        {
            return null;
        }

        entity.Status = JsonSerializer.Serialize(request.Status);
        entity.HeartbeatAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return MapToModel(entity);
    }

    public async Task<bool> DeleteNodeAsync(string nodeId)
    {
        using var activity = TelemetryConfig.ActivitySource.StartActivity("NodeStore.DeleteNode");
        activity?.SetTag("node.id", nodeId);

        var entity = await _context.Nodes.FindAsync(nodeId);
        if (entity == null)
        {
            return false;
        }

        _context.Nodes.Remove(entity);
        await _context.SaveChangesAsync();

        TelemetryConfig.NodesDisconnectedCounter.Add(1,
            new KeyValuePair<string, object?>("node.id", nodeId));

        return true;
    }

    private static Node MapToModel(NodeEntity entity)
    {
        return new Node
        {
            NodeId = entity.NodeId,
            Metadata = entity.Metadata != null
                ? JsonSerializer.Deserialize<Dictionary<string, object>>(entity.Metadata)
                : null,
            Capacity = entity.Capacity != null
                ? JsonSerializer.Deserialize<Dictionary<string, object>>(entity.Capacity)
                : null,
            Status = entity.Status != null
                ? JsonSerializer.Deserialize<NodeStatus>(entity.Status) ?? new NodeStatus()
                : new NodeStatus(),
            HeartbeatAt = entity.HeartbeatAt
        };
    }
}
