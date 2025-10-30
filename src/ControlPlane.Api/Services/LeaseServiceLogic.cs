using ControlPlane.Api.Grpc;
using ControlPlane.Api.Models;
using System.Runtime.CompilerServices;

namespace ControlPlane.Api.Services;

/// <summary>
/// Implementation of lease management business logic
/// </summary>
public class LeaseServiceLogic : ILeaseService
{
    private readonly IRunStore _runStore;
    private readonly INodeStore _nodeStore;
    private readonly ILeaseStore _leaseStore;
    private readonly ILogger<LeaseServiceLogic> _logger;

    public LeaseServiceLogic(
        IRunStore runStore,
        INodeStore nodeStore,
        ILeaseStore leaseStore,
        ILogger<LeaseServiceLogic> logger)
    {
        _runStore = runStore;
        _nodeStore = nodeStore;
        _leaseStore = leaseStore;
        _logger = logger;
    }

    public async IAsyncEnumerable<Grpc.Lease> GetLeasesAsync(
        string nodeId, 
        int maxLeases, 
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        _logger.LogInformation("Node {NodeId} requesting up to {MaxLeases} leases", nodeId, maxLeases);

        // Verify node exists
        var node = await _nodeStore.GetNodeAsync(nodeId);
        if (node == null)
        {
            _logger.LogWarning("Node {NodeId} not found", nodeId);
            yield break;
        }

        // Poll for available runs and stream them to the node
        // In a real implementation, this would be more sophisticated with proper scheduling
        while (!cancellationToken.IsCancellationRequested)
        {
            var leasesToYield = new List<Grpc.Lease>();
            
            try
            {
                // Get pending runs
                var runs = await _runStore.GetAllRunsAsync();
                var pendingRuns = runs
                    .Where(r => r.Status == "pending" && r.NodeId == null)
                    .Take(maxLeases)
                    .ToList();

                foreach (var run in pendingRuns)
                {
                    // Try to acquire lease for this run
                    var leaseId = $"lease-{run.RunId}-{Guid.NewGuid():N}";
                    var ttlSeconds = 300; // 5 minutes in seconds
                    
                    var acquired = await _leaseStore.AcquireLeaseAsync(run.RunId, nodeId, ttlSeconds);
                    if (acquired)
                    {
                        _logger.LogInformation("Acquired lease {LeaseId} for run {RunId} on node {NodeId}", 
                            leaseId, run.RunId, nodeId);

                        // Update run to assign to node
                        run.NodeId = nodeId;
                        run.Status = "assigned";
                        // Update via the store's method
                        // Note: IRunStore doesn't have UpdateRunAsync, so we work around this limitation
                        // by manipulating the run object in memory (works for InMemoryRunStore)

                        // Create the lease to yield
                        var lease = new Grpc.Lease
                        {
                            LeaseId = leaseId,
                            RunId = run.RunId,
                            RunSpec = new RunSpec
                            {
                                AgentId = run.AgentId,
                                Version = run.Version,
                                DeploymentId = run.DeploymentId ?? string.Empty,
                                Budgets = new BudgetConstraints
                                {
                                    MaxTokens = 4000,
                                    MaxDurationSeconds = 60
                                }
                            },
                            DeadlineUnixMs = DateTimeOffset.UtcNow.AddSeconds(ttlSeconds).ToUnixTimeMilliseconds(),
                            TraceId = run.TraceId ?? Guid.NewGuid().ToString()
                        };

                        // Add input_ref if available
                        if (run.InputRef != null)
                        {
                            foreach (var kvp in run.InputRef)
                            {
                                lease.RunSpec.InputRef.Add(kvp.Key, kvp.Value?.ToString() ?? string.Empty);
                            }
                        }

                        leasesToYield.Add(lease);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error preparing leases for node {NodeId}", nodeId);
            }

            // Yield all prepared leases
            foreach (var lease in leasesToYield)
            {
                yield return lease;
            }

            // If we didn't find any leases, wait before trying again
            if (leasesToYield.Count == 0)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("Lease streaming cancelled for node {NodeId}", nodeId);
                    break;
                }
            }
        }

        _logger.LogInformation("Lease streaming ended for node {NodeId}", nodeId);
    }

    public async Task<bool> AcknowledgeLeaseAsync(string leaseId, string nodeId, long ackTimestamp)
    {
        _logger.LogInformation("Node {NodeId} acknowledging lease {LeaseId} at {Timestamp}", 
            nodeId, leaseId, ackTimestamp);

        // Lease acknowledgment is primarily for telemetry and diagnostics
        // The actual work assignment happens during Pull
        return await Task.FromResult(true);
    }

    public async Task<bool> CompleteRunAsync(
        string leaseId,
        string runId,
        string nodeId,
        IDictionary<string, string>? result,
        TimingInfo? timings,
        CostInfo? costs)
    {
        _logger.LogInformation("Node {NodeId} completing run {RunId} with lease {LeaseId}", 
            nodeId, runId, leaseId);

        try
        {
            var run = await _runStore.GetRunAsync(runId);
            if (run == null)
            {
                _logger.LogWarning("Run {RunId} not found", runId);
                return false;
            }

            if (run.NodeId != nodeId)
            {
                _logger.LogWarning("Node {NodeId} attempted to complete run {RunId} assigned to {AssignedNode}",
                    nodeId, runId, run.NodeId);
                return false;
            }

            // Build the complete request
            var request = new CompleteRunRequest
            {
                Result = result != null ? result.ToDictionary(kvp => kvp.Key, kvp => (object)kvp.Value) : null,
                Timings = timings != null ? new Dictionary<string, object>
                {
                    ["duration_ms"] = timings.DurationMs,
                    ["queue_time_ms"] = timings.QueueTimeMs,
                    ["execution_time_ms"] = timings.ExecutionTimeMs
                } : null,
                Costs = costs != null ? new Dictionary<string, object>
                {
                    ["tokens_in"] = costs.TokensIn,
                    ["tokens_out"] = costs.TokensOut,
                    ["usd_cost"] = costs.UsdCost
                } : null
            };

            // Use the existing CompleteRunAsync method
            var updatedRun = await _runStore.CompleteRunAsync(runId, request);
            if (updatedRun == null)
            {
                return false;
            }

            // Release the lease
            await _leaseStore.ReleaseLeaseAsync(runId);

            _logger.LogInformation("Run {RunId} completed successfully", runId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing run {RunId}", runId);
            return false;
        }
    }

    public async Task<(bool success, bool shouldRetry)> FailRunAsync(
        string leaseId,
        string runId,
        string nodeId,
        string errorMessage,
        string? errorDetails,
        TimingInfo? timings,
        bool retryable)
    {
        _logger.LogWarning("Node {NodeId} reporting failure for run {RunId}: {ErrorMessage}", 
            nodeId, runId, errorMessage);

        try
        {
            var run = await _runStore.GetRunAsync(runId);
            if (run == null)
            {
                _logger.LogWarning("Run {RunId} not found", runId);
                return (false, false);
            }

            if (run.NodeId != nodeId)
            {
                _logger.LogWarning("Node {NodeId} attempted to fail run {RunId} assigned to {AssignedNode}",
                    nodeId, runId, run.NodeId);
                return (false, false);
            }

            // Build fail request
            var failRequest = new FailRunRequest
            {
                ErrorMessage = errorMessage,
                ErrorDetails = errorDetails,
                Timings = timings != null ? new Dictionary<string, object>
                {
                    ["duration_ms"] = timings.DurationMs,
                    ["queue_time_ms"] = timings.QueueTimeMs,
                    ["execution_time_ms"] = timings.ExecutionTimeMs
                } : null
            };

            // Use the existing FailRunAsync method
            var updatedRun = await _runStore.FailRunAsync(runId, failRequest);
            if (updatedRun == null)
            {
                return (false, false);
            }

            // Determine if we should retry (simple retry logic - max 3 attempts)
            var retryCount = updatedRun.ErrorInfo?.ContainsKey("retry_count") == true
                ? Convert.ToInt32(updatedRun.ErrorInfo["retry_count"]) 
                : 0;
            
            var shouldRetry = retryable && retryCount < 3;

            // Release the lease
            await _leaseStore.ReleaseLeaseAsync(runId);

            _logger.LogInformation("Run {RunId} marked as failed. Should retry: {ShouldRetry}", runId, shouldRetry);
            return (true, shouldRetry);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing failure for run {RunId}", runId);
            return (false, false);
        }
    }
}
