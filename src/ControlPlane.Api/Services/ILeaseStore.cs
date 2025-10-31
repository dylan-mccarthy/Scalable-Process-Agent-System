namespace ControlPlane.Api.Services;

/// <summary>
/// Represents a lease assignment with TTL expiry.
/// Used by the scheduler to prevent double-assignment of runs to nodes.
/// </summary>
public class Lease
{
    /// <summary>
    /// Unique identifier for the run being leased
    /// </summary>
    public required string RunId { get; set; }

    /// <summary>
    /// Node ID that holds the lease
    /// </summary>
    public required string NodeId { get; set; }

    /// <summary>
    /// When the lease was acquired
    /// </summary>
    public DateTime AcquiredAt { get; set; }

    /// <summary>
    /// When the lease expires (UTC)
    /// </summary>
    public DateTime ExpiresAt { get; set; }
}

/// <summary>
/// Store for managing run leases with TTL expiry.
/// Prevents double-assignment of runs to nodes.
/// </summary>
public interface ILeaseStore
{
    /// <summary>
    /// Attempt to acquire a lease for a run on a specific node.
    /// Returns true if the lease was acquired, false if it already exists.
    /// </summary>
    /// <param name="runId">The run ID to lease</param>
    /// <param name="nodeId">The node requesting the lease</param>
    /// <param name="ttlSeconds">Time-to-live in seconds for the lease</param>
    /// <returns>True if lease was acquired, false if already exists</returns>
    Task<bool> AcquireLeaseAsync(string runId, string nodeId, int ttlSeconds);

    /// <summary>
    /// Release a lease, allowing it to be acquired by another node.
    /// </summary>
    /// <param name="runId">The run ID to release</param>
    /// <returns>True if lease was released, false if it didn't exist</returns>
    Task<bool> ReleaseLeaseAsync(string runId);

    /// <summary>
    /// Get the current lease for a run, if it exists and hasn't expired.
    /// </summary>
    /// <param name="runId">The run ID to check</param>
    /// <returns>The lease if it exists and is valid, null otherwise</returns>
    Task<Lease?> GetLeaseAsync(string runId);

    /// <summary>
    /// Extend an existing lease by adding more time to its TTL.
    /// Used for heartbeat/keepalive scenarios.
    /// </summary>
    /// <param name="runId">The run ID to extend</param>
    /// <param name="additionalSeconds">Additional seconds to add to the TTL</param>
    /// <returns>True if the lease was extended, false if it doesn't exist</returns>
    Task<bool> ExtendLeaseAsync(string runId, int additionalSeconds);
}
