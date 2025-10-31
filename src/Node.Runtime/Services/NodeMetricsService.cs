namespace Node.Runtime.Services;

/// <summary>
/// Service for providing real-time metrics data to OpenTelemetry observable gauges
/// </summary>
public interface INodeMetricsService
{
    /// <summary>
    /// Get the current number of active leases being processed (synchronous for observable gauge callbacks)
    /// </summary>
    int GetActiveLeases();

    /// <summary>
    /// Get the current number of available slots for lease processing (synchronous for observable gauge callbacks)
    /// </summary>
    int GetAvailableSlots();

    /// <summary>
    /// Increment the active leases counter (called when a lease starts processing)
    /// </summary>
    void IncrementActiveLeases();

    /// <summary>
    /// Decrement the active leases counter (called when a lease completes)
    /// </summary>
    void DecrementActiveLeases();
}

/// <summary>
/// Implementation of node metrics service that tracks lease processing state
/// </summary>
public class NodeMetricsService : INodeMetricsService
{
    private int _activeLeases = 0;
    private readonly int _maxConcurrentLeases;
    private readonly ILogger<NodeMetricsService> _logger;

    public NodeMetricsService(
        Microsoft.Extensions.Options.IOptions<Configuration.NodeRuntimeOptions> options,
        ILogger<NodeMetricsService> logger)
    {
        _maxConcurrentLeases = options.Value.MaxConcurrentLeases;
        _logger = logger;
    }

    public int GetActiveLeases()
    {
        try
        {
            return Interlocked.CompareExchange(ref _activeLeases, 0, 0);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving active leases count");
            return 0;
        }
    }

    public int GetAvailableSlots()
    {
        try
        {
            var activeLeases = GetActiveLeases();
            var available = _maxConcurrentLeases - activeLeases;
            return available >= 0 ? available : 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating available slots");
            return 0;
        }
    }

    public void IncrementActiveLeases()
    {
        Interlocked.Increment(ref _activeLeases);
    }

    public void DecrementActiveLeases()
    {
        Interlocked.Decrement(ref _activeLeases);
    }
}
