using ControlPlane.Api.Observability;

namespace ControlPlane.Api.Services;

/// <summary>
/// Service for providing real-time metrics data to OpenTelemetry observable gauges
/// </summary>
public interface IMetricsService
{
    /// <summary>
    /// Get the current number of active runs (synchronous for observable gauge callbacks)
    /// </summary>
    int GetActiveRunsCount();

    /// <summary>
    /// Get the current number of active nodes (synchronous for observable gauge callbacks)
    /// </summary>
    int GetActiveNodesCount();

    /// <summary>
    /// Get the total number of slots across all nodes (synchronous for observable gauge callbacks)
    /// </summary>
    int GetTotalSlots();

    /// <summary>
    /// Get the number of slots currently in use (synchronous for observable gauge callbacks)
    /// </summary>
    int GetUsedSlots();

    /// <summary>
    /// Get the number of slots currently available (synchronous for observable gauge callbacks)
    /// </summary>
    int GetAvailableSlots();
}

/// <summary>
/// Implementation of metrics service that queries stores for real-time data
/// </summary>
public class MetricsService : IMetricsService
{
    private const int NodeHeartbeatTimeoutSeconds = 60;

    private readonly IRunStore _runStore;
    private readonly INodeStore _nodeStore;
    private readonly ILogger<MetricsService> _logger;

    public MetricsService(
        IRunStore runStore,
        INodeStore nodeStore,
        ILogger<MetricsService> logger)
    {
        _runStore = runStore;
        _nodeStore = nodeStore;
        _logger = logger;
    }

    public int GetActiveRunsCount()
    {
        try
        {
            // Use synchronous method to avoid deadlocks in observable gauge callbacks
            var runs = _runStore.GetAllRunsAsync().ConfigureAwait(false).GetAwaiter().GetResult();
            return runs.Count(r => r.Status == "running" || r.Status == "pending");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving active runs count");
            return 0;
        }
    }

    public int GetActiveNodesCount()
    {
        try
        {
            var nodes = _nodeStore.GetAllNodesAsync().ConfigureAwait(false).GetAwaiter().GetResult();
            var now = DateTime.UtcNow;
            // Consider a node active if it has sent a heartbeat within the timeout period
            return nodes.Count(n => n.Status?.State == "active" &&
                                   (now - n.HeartbeatAt).TotalSeconds < NodeHeartbeatTimeoutSeconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving active nodes count");
            return 0;
        }
    }

    public int GetTotalSlots()
    {
        try
        {
            var nodes = _nodeStore.GetAllNodesAsync().ConfigureAwait(false).GetAwaiter().GetResult();
            var now = DateTime.UtcNow;
            // Only count slots from active nodes
            return nodes
                .Where(n => n.Status?.State == "active" && (now - n.HeartbeatAt).TotalSeconds < NodeHeartbeatTimeoutSeconds)
                .Sum(n => GetNodeTotalSlots(n));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving total slots");
            return 0;
        }
    }

    public int GetUsedSlots()
    {
        try
        {
            var nodes = _nodeStore.GetAllNodesAsync().ConfigureAwait(false).GetAwaiter().GetResult();
            var now = DateTime.UtcNow;
            return nodes
                .Where(n => n.Status?.State == "active" && (now - n.HeartbeatAt).TotalSeconds < NodeHeartbeatTimeoutSeconds)
                .Sum(n => n.Status?.ActiveRuns ?? 0);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving used slots");
            return 0;
        }
    }

    public int GetAvailableSlots()
    {
        try
        {
            var nodes = _nodeStore.GetAllNodesAsync().ConfigureAwait(false).GetAwaiter().GetResult();
            var now = DateTime.UtcNow;
            return nodes
                .Where(n => n.Status?.State == "active" && (now - n.HeartbeatAt).TotalSeconds < NodeHeartbeatTimeoutSeconds)
                .Sum(n => n.Status?.AvailableSlots ?? 0);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving available slots");
            return 0;
        }
    }

    /// <summary>
    /// Extract total slots from node capacity
    /// </summary>
    private int GetNodeTotalSlots(Models.Node node)
    {
        if (node.Capacity?.TryGetValue("slots", out var slotsObj) == true && slotsObj != null)
        {
            if (slotsObj is System.Text.Json.JsonElement jsonElement)
            {
                if (jsonElement.ValueKind == System.Text.Json.JsonValueKind.Number)
                {
                    return jsonElement.GetInt32();
                }
            }
            else if (slotsObj is int intSlots)
            {
                return intSlots;
            }
            else if (slotsObj is long longSlots)
            {
                // Safe conversion with bounds checking
                if (longSlots > int.MaxValue)
                {
                    _logger.LogWarning("Slot count {LongSlots} exceeds int.MaxValue, capping at {MaxValue}",
                        longSlots, int.MaxValue);
                    return int.MaxValue;
                }
                if (longSlots < 0)
                {
                    _logger.LogWarning("Slot count {LongSlots} is negative, using 0", longSlots);
                    return 0;
                }
                return (int)longSlots;
            }
            else
            {
                try
                {
                    return Convert.ToInt32(slotsObj);
                }
                catch (OverflowException ex)
                {
                    _logger.LogWarning(ex, "Slot count conversion overflow, using 0");
                    return 0;
                }
                catch
                {
                    return 0;
                }
            }
        }

        return 0;
    }
}
