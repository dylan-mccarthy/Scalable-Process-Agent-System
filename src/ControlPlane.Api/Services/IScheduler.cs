using ControlPlane.Api.Models;

namespace ControlPlane.Api.Services;

/// <summary>
/// Interface for scheduling runs to nodes
/// </summary>
public interface IScheduler
{
    /// <summary>
    /// Schedules a run to the most appropriate node based on the scheduling strategy
    /// </summary>
    /// <param name="run">The run to schedule</param>
    /// <param name="placementConstraints">Optional placement constraints (e.g., region affinity)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The selected node ID, or null if no suitable node is available</returns>
    Task<string?> ScheduleRunAsync(
        Run run,
        Dictionary<string, object>? placementConstraints = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current load information for all nodes
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Dictionary mapping node IDs to their current load metrics</returns>
    Task<Dictionary<string, NodeLoadInfo>> GetNodeLoadAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents the load information for a node
/// </summary>
public class NodeLoadInfo
{
    /// <summary>
    /// Node identifier
    /// </summary>
    public string NodeId { get; set; } = string.Empty;

    /// <summary>
    /// Total capacity (number of slots)
    /// </summary>
    public int TotalSlots { get; set; }

    /// <summary>
    /// Number of active runs on this node
    /// </summary>
    public int ActiveRuns { get; set; }

    /// <summary>
    /// Number of available slots
    /// </summary>
    public int AvailableSlots { get; set; }

    /// <summary>
    /// Node metadata (includes region, environment, etc.)
    /// </summary>
    public Dictionary<string, object>? Metadata { get; set; }

    /// <summary>
    /// Load percentage (0-100)
    /// </summary>
    public double LoadPercentage => TotalSlots > 0 ? (ActiveRuns * 100.0 / TotalSlots) : 100.0;

    /// <summary>
    /// Whether the node can accept more work
    /// </summary>
    public bool HasCapacity => AvailableSlots > 0;
}
