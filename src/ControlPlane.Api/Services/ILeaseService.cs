using ControlPlane.Api.Grpc;

namespace ControlPlane.Api.Services;

/// <summary>
/// Interface for lease management business logic
/// </summary>
public interface ILeaseService
{
    /// <summary>
    /// Gets available leases for a node
    /// </summary>
    /// <param name="nodeId">The node requesting leases</param>
    /// <param name="maxLeases">Maximum number of concurrent leases</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Async enumerable of leases</returns>
    IAsyncEnumerable<Grpc.Lease> GetLeasesAsync(string nodeId, int maxLeases, CancellationToken cancellationToken);

    /// <summary>
    /// Acknowledges receipt of a lease
    /// </summary>
    /// <param name="leaseId">The lease ID</param>
    /// <param name="nodeId">The node acknowledging the lease</param>
    /// <param name="ackTimestamp">Acknowledgment timestamp in Unix milliseconds</param>
    /// <returns>True if acknowledgment was successful</returns>
    Task<bool> AcknowledgeLeaseAsync(string leaseId, string nodeId, long ackTimestamp);

    /// <summary>
    /// Completes a run associated with a lease
    /// </summary>
    /// <param name="leaseId">The lease ID</param>
    /// <param name="runId">The run ID</param>
    /// <param name="nodeId">The node completing the run</param>
    /// <param name="result">Run result data</param>
    /// <param name="timings">Timing information</param>
    /// <param name="costs">Cost information</param>
    /// <returns>True if completion was successful</returns>
    Task<bool> CompleteRunAsync(
        string leaseId, 
        string runId, 
        string nodeId, 
        IDictionary<string, string>? result,
        TimingInfo? timings,
        CostInfo? costs);

    /// <summary>
    /// Marks a run as failed
    /// </summary>
    /// <param name="leaseId">The lease ID</param>
    /// <param name="runId">The run ID</param>
    /// <param name="nodeId">The node reporting the failure</param>
    /// <param name="errorMessage">Error message</param>
    /// <param name="errorDetails">Detailed error information</param>
    /// <param name="timings">Timing information</param>
    /// <param name="retryable">Whether the error is retryable</param>
    /// <returns>Tuple of (success, shouldRetry)</returns>
    Task<(bool success, bool shouldRetry)> FailRunAsync(
        string leaseId,
        string runId,
        string nodeId,
        string errorMessage,
        string? errorDetails,
        TimingInfo? timings,
        bool retryable);
}
