using ControlPlane.Api.Models;
using ControlPlane.Api.Observability;
using System.Diagnostics;

namespace ControlPlane.Api.Services;

/// <summary>
/// Scheduler that assigns runs to the least-loaded node with region constraint support
/// </summary>
public class LeastLoadedScheduler : IScheduler
{
    private readonly INodeStore _nodeStore;
    private readonly IRunStore _runStore;
    private readonly ILogger<LeastLoadedScheduler> _logger;

    public LeastLoadedScheduler(
        INodeStore nodeStore,
        IRunStore runStore,
        ILogger<LeastLoadedScheduler> logger)
    {
        _nodeStore = nodeStore;
        _runStore = runStore;
        _logger = logger;
    }

    public async Task<string?> ScheduleRunAsync(
        Run run,
        Dictionary<string, object>? placementConstraints = null,
        CancellationToken cancellationToken = default)
    {
        using var activity = TelemetryConfig.ActivitySource.StartActivity("Scheduler.ScheduleRun");
        activity?.SetTag("run.id", run.RunId);
        activity?.SetTag("agent.id", run.AgentId);
        
        var startTime = Stopwatch.GetTimestamp();
        
        _logger.LogDebug("Scheduling run {RunId} with constraints: {Constraints}", 
            run.RunId, placementConstraints);

        TelemetryConfig.SchedulingAttemptsCounter.Add(1,
            new KeyValuePair<string, object?>("agent.id", run.AgentId));

        // Get all active nodes
        var nodes = await _nodeStore.GetAllNodesAsync();
        var activeNodes = nodes.Where(n => n.Status.State == "active").ToList();

        if (!activeNodes.Any())
        {
            _logger.LogWarning("No active nodes available for scheduling");
            activity?.SetTag("scheduling.result", "no_nodes");
            TelemetryConfig.SchedulingFailuresCounter.Add(1,
                new KeyValuePair<string, object?>("reason", "no_active_nodes"));
            return null;
        }

        // Get current load information for all nodes
        var nodeLoads = await GetNodeLoadAsync(cancellationToken);

        // Filter nodes by placement constraints (e.g., region affinity)
        var eligibleNodes = FilterNodesByConstraints(activeNodes, nodeLoads, placementConstraints);

        if (!eligibleNodes.Any())
        {
            _logger.LogWarning("No eligible nodes found matching placement constraints for run {RunId}", run.RunId);
            activity?.SetTag("scheduling.result", "no_eligible_nodes");
            TelemetryConfig.SchedulingFailuresCounter.Add(1,
                new KeyValuePair<string, object?>("reason", "no_eligible_nodes"));
            return null;
        }

        // Select the least-loaded node with available capacity
        var selectedNode = SelectLeastLoadedNode(eligibleNodes, nodeLoads);

        if (selectedNode == null)
        {
            _logger.LogWarning("No node with available capacity found for run {RunId}", run.RunId);
            activity?.SetTag("scheduling.result", "no_capacity");
            TelemetryConfig.SchedulingFailuresCounter.Add(1,
                new KeyValuePair<string, object?>("reason", "no_capacity"));
            return null;
        }

        var loadPercentage = nodeLoads[selectedNode].LoadPercentage;
        _logger.LogInformation("Scheduled run {RunId} to node {NodeId} (load: {LoadPercentage:F1}%)", 
            run.RunId, selectedNode, loadPercentage);

        activity?.SetTag("scheduling.result", "success");
        activity?.SetTag("node.id", selectedNode);
        activity?.SetTag("node.load_percentage", loadPercentage);

        var elapsedMs = Stopwatch.GetElapsedTime(startTime).TotalMilliseconds;
        TelemetryConfig.SchedulingDurationHistogram.Record(elapsedMs,
            new KeyValuePair<string, object?>("result", "success"));

        return selectedNode;
    }

    public async Task<Dictionary<string, NodeLoadInfo>> GetNodeLoadAsync(CancellationToken cancellationToken = default)
    {
        var nodes = await _nodeStore.GetAllNodesAsync();
        var runs = await _runStore.GetAllRunsAsync();

        var nodeLoads = new Dictionary<string, NodeLoadInfo>();

        foreach (var node in nodes)
        {
            // Get total slots from capacity
            var totalSlots = GetSlotsFromCapacity(node.Capacity);

            // Count active runs on this node
            var activeRuns = runs.Count(r => 
                r.NodeId == node.NodeId && 
                (r.Status == "assigned" || r.Status == "running"));

            var availableSlots = node.Status.AvailableSlots;

            nodeLoads[node.NodeId] = new NodeLoadInfo
            {
                NodeId = node.NodeId,
                TotalSlots = totalSlots,
                ActiveRuns = activeRuns,
                AvailableSlots = availableSlots,
                Metadata = node.Metadata
            };
        }

        return nodeLoads;
    }

    private List<Node> FilterNodesByConstraints(
        List<Node> nodes,
        Dictionary<string, NodeLoadInfo> nodeLoads,
        Dictionary<string, object>? placementConstraints)
    {
        if (placementConstraints == null || !placementConstraints.Any())
        {
            // No constraints - return all nodes with capacity
            return nodes.Where(n => nodeLoads[n.NodeId].HasCapacity).ToList();
        }

        var eligibleNodes = new List<Node>();

        foreach (var node in nodes)
        {
            // Skip nodes without capacity
            if (!nodeLoads[node.NodeId].HasCapacity)
            {
                continue;
            }

            // Check if node matches all placement constraints
            if (MatchesPlacementConstraints(node, placementConstraints))
            {
                eligibleNodes.Add(node);
            }
        }

        return eligibleNodes;
    }

    private bool MatchesPlacementConstraints(Node node, Dictionary<string, object> placementConstraints)
    {
        // Handle region constraint (most common)
        if (placementConstraints.ContainsKey("region"))
        {
            var requiredRegions = GetRegionConstraint(placementConstraints["region"]);
            var nodeRegion = GetNodeRegion(node);

            if (nodeRegion == null || !requiredRegions.Contains(nodeRegion))
            {
                _logger.LogDebug("Node {NodeId} excluded: region {NodeRegion} not in required regions {RequiredRegions}",
                    node.NodeId, nodeRegion, string.Join(", ", requiredRegions));
                return false;
            }
        }

        // Handle environment constraint
        if (placementConstraints.ContainsKey("environment"))
        {
            var requiredEnvironment = placementConstraints["environment"]?.ToString();
            var nodeEnvironment = GetNodeMetadataValue(node, "environment");

            if (requiredEnvironment != null && nodeEnvironment != requiredEnvironment)
            {
                _logger.LogDebug("Node {NodeId} excluded: environment {NodeEnv} != required {RequiredEnv}",
                    node.NodeId, nodeEnvironment, requiredEnvironment);
                return false;
            }
        }

        // Add more constraint types as needed

        return true;
    }

    private string? SelectLeastLoadedNode(List<Node> eligibleNodes, Dictionary<string, NodeLoadInfo> nodeLoads)
    {
        if (!eligibleNodes.Any())
        {
            return null;
        }

        // Sort by load percentage (ascending), then by available slots (descending)
        var sortedNodes = eligibleNodes
            .Select(n => new
            {
                Node = n,
                Load = nodeLoads[n.NodeId]
            })
            .Where(x => x.Load.HasCapacity)
            .OrderBy(x => x.Load.LoadPercentage)
            .ThenByDescending(x => x.Load.AvailableSlots)
            .ToList();

        return sortedNodes.FirstOrDefault()?.Node.NodeId;
    }

    private int GetSlotsFromCapacity(Dictionary<string, object>? capacity)
    {
        if (capacity == null || !capacity.ContainsKey("slots"))
        {
            return 0;
        }

        var slotsValue = capacity["slots"];
        if (slotsValue is int intValue)
        {
            return intValue;
        }

        if (int.TryParse(slotsValue?.ToString(), out var parsedValue))
        {
            return parsedValue;
        }

        return 0;
    }

    private List<string> GetRegionConstraint(object regionConstraint)
    {
        if (regionConstraint is string singleRegion)
        {
            return new List<string> { singleRegion };
        }

        if (regionConstraint is List<object> regionList)
        {
            return regionList.Select(r => r.ToString() ?? string.Empty).Where(r => !string.IsNullOrEmpty(r)).ToList();
        }

        if (regionConstraint is string[] regionArray)
        {
            return regionArray.ToList();
        }

        return new List<string>();
    }

    private string? GetNodeRegion(Node node)
    {
        return GetNodeMetadataValue(node, "region");
    }

    private string? GetNodeMetadataValue(Node node, string key)
    {
        if (node.Metadata == null || !node.Metadata.ContainsKey(key))
        {
            return null;
        }

        return node.Metadata[key]?.ToString();
    }
}
