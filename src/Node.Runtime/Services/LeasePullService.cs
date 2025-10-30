using ControlPlane.Api.Grpc;
using Grpc.Core;
using Node.Runtime.Configuration;
using Microsoft.Extensions.Options;
using System.Diagnostics;

namespace Node.Runtime.Services;

/// <summary>
/// Service for pulling leases from the Control Plane via gRPC.
/// </summary>
public interface ILeasePullService
{
    /// <summary>
    /// Starts pulling leases from the Control Plane.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops pulling leases.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task StopAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Implementation of the lease pull service.
/// </summary>
public sealed class LeasePullService : ILeasePullService
{
    private readonly LeaseService.LeaseServiceClient _leaseClient;
    private readonly IAgentExecutor _agentExecutor;
    private readonly NodeRuntimeOptions _options;
    private readonly ILogger<LeasePullService> _logger;
    private Task? _pullTask;
    private CancellationTokenSource? _pullCts;
    private readonly SemaphoreSlim _activeLeasesLock = new(1, 1);
    private int _activeLeases = 0;

    public LeasePullService(
        LeaseService.LeaseServiceClient leaseClient,
        IAgentExecutor agentExecutor,
        IOptions<NodeRuntimeOptions> options,
        ILogger<LeasePullService> logger)
    {
        _leaseClient = leaseClient;
        _agentExecutor = agentExecutor;
        _options = options.Value;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting lease pull service for node {NodeId}", _options.NodeId);

        _pullCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _pullTask = Task.Run(() => PullLeasesAsync(_pullCts.Token), _pullCts.Token);

        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Stopping lease pull service for node {NodeId}", _options.NodeId);

        if (_pullCts != null)
        {
            await _pullCts.CancelAsync();
        }

        if (_pullTask != null)
        {
            try
            {
                await _pullTask;
            }
            catch (OperationCanceledException)
            {
                // Expected when stopping
            }
        }

        _pullCts?.Dispose();
        _pullCts = null;
        _pullTask = null;
    }

    private async Task PullLeasesAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                _logger.LogInformation("Initiating lease pull for node {NodeId} with max leases {MaxLeases}",
                    _options.NodeId, _options.MaxConcurrentLeases);

                var request = new PullRequest
                {
                    NodeId = _options.NodeId,
                    MaxLeases = _options.MaxConcurrentLeases
                };

                using var call = _leaseClient.Pull(request, cancellationToken: cancellationToken);

                await foreach (var lease in call.ResponseStream.ReadAllAsync(cancellationToken))
                {
                    _logger.LogInformation(
                        "Received lease {LeaseId} for run {RunId} (Agent: {AgentId}, Version: {Version})",
                        lease.LeaseId, lease.RunId, lease.RunSpec?.AgentId, lease.RunSpec?.Version);

                    // Acknowledge the lease
                    _ = Task.Run(async () => await AcknowledgeLeaseAsync(lease, cancellationToken), cancellationToken);

                    // Process the lease (to be implemented in E2-T4)
                    _ = Task.Run(async () => await ProcessLeaseAsync(lease, cancellationToken), cancellationToken);
                }

                _logger.LogInformation("Lease pull stream completed for node {NodeId}", _options.NodeId);
            }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled)
            {
                _logger.LogInformation("Lease pull cancelled for node {NodeId}", _options.NodeId);
                break;
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Lease pull cancelled for node {NodeId}", _options.NodeId);
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error pulling leases for node {NodeId}", _options.NodeId);

                // Wait before retrying
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            }
        }
    }

    private async Task AcknowledgeLeaseAsync(Lease lease, CancellationToken cancellationToken)
    {
        try
        {
            var ackRequest = new AckRequest
            {
                LeaseId = lease.LeaseId,
                NodeId = _options.NodeId,
                AckTimestampUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };

            var response = await _leaseClient.AckAsync(ackRequest, cancellationToken: cancellationToken);

            if (response.Success)
            {
                _logger.LogDebug("Acknowledged lease {LeaseId}", lease.LeaseId);
            }
            else
            {
                _logger.LogWarning("Failed to acknowledge lease {LeaseId}: {Message}", lease.LeaseId, response.Message);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error acknowledging lease {LeaseId}", lease.LeaseId);
        }
    }

    private async Task ProcessLeaseAsync(Lease lease, CancellationToken cancellationToken)
    {
        await _activeLeasesLock.WaitAsync(cancellationToken);
        try
        {
            _activeLeases++;
        }
        finally
        {
            _activeLeasesLock.Release();
        }

        var stopwatch = Stopwatch.StartNew();

        try
        {
            _logger.LogInformation("Processing lease {LeaseId} for run {RunId}", lease.LeaseId, lease.RunId);

            if (lease.RunSpec == null)
            {
                throw new InvalidOperationException($"Lease {lease.LeaseId} has no run specification");
            }

            // For MVP, we use a simplified approach where agent details are embedded in metadata
            // In a full implementation, we would fetch the agent definition from the Control Plane API
            // For now, extract basic info from the run spec
            var agentId = lease.RunSpec.AgentId;
            var version = lease.RunSpec.Version;

            // Get input from metadata or use empty string
            var input = lease.RunSpec.InputRef.TryGetValue("message", out var msg) ? msg : string.Empty;

            // Create agent specification with defaults
            // Note: In production, these would be fetched from the Control Plane
            var agentSpec = new AgentSpec
            {
                AgentId = agentId,
                Version = version,
                Name = lease.RunSpec.Metadata.TryGetValue("agent_name", out var name) ? name : agentId,
                Instructions = lease.RunSpec.Metadata.TryGetValue("instructions", out var instr) 
                    ? instr 
                    : "Process the input message.",
                ModelProfile = null, // Will use defaults from AgentRuntimeOptions
                Budget = lease.RunSpec.Budgets != null
                    ? new BudgetConstraints
                    {
                        MaxTokens = lease.RunSpec.Budgets.MaxTokens > 0 ? lease.RunSpec.Budgets.MaxTokens : null,
                        MaxDurationSeconds = lease.RunSpec.Budgets.MaxDurationSeconds > 0 ? lease.RunSpec.Budgets.MaxDurationSeconds : null
                    }
                    : null
            };

            // Execute the agent using MAF SDK
            var result = await _agentExecutor.ExecuteAsync(agentSpec, input, cancellationToken);

            stopwatch.Stop();

            if (result.Success)
            {
                // Mark as completed
                var completeRequest = new CompleteRequest
                {
                    LeaseId = lease.LeaseId,
                    RunId = lease.RunId,
                    NodeId = _options.NodeId,
                    Timings = new TimingInfo
                    {
                        DurationMs = (long)stopwatch.ElapsedMilliseconds
                    },
                    Costs = new CostInfo
                    {
                        TokensIn = result.TokensIn,
                        TokensOut = result.TokensOut,
                        UsdCost = result.UsdCost
                    }
                };

                // Add result to response if available
                if (!string.IsNullOrEmpty(result.Output))
                {
                    completeRequest.Result.Add("output", result.Output);
                }

                var response = await _leaseClient.CompleteAsync(completeRequest, cancellationToken: cancellationToken);

                if (response.Success)
                {
                    _logger.LogInformation(
                        "Successfully completed run {RunId} in {DurationMs}ms (Tokens: {TokensIn}/{TokensOut}, Cost: ${Cost:F4})",
                        lease.RunId,
                        stopwatch.ElapsedMilliseconds,
                        result.TokensIn,
                        result.TokensOut,
                        result.UsdCost);
                }
                else
                {
                    _logger.LogWarning("Failed to complete run {RunId}: {Message}", lease.RunId, response.Message);
                }
            }
            else
            {
                // Agent execution failed - report failure
                _logger.LogWarning(
                    "Agent execution failed for run {RunId}: {Error}",
                    lease.RunId,
                    result.Error);

                var failRequest = new FailRequest
                {
                    LeaseId = lease.LeaseId,
                    RunId = lease.RunId,
                    NodeId = _options.NodeId,
                    ErrorMessage = result.Error ?? "Agent execution failed",
                    ErrorDetails = result.Error ?? string.Empty,
                    Retryable = !result.Error?.Contains("timeout", StringComparison.OrdinalIgnoreCase) ?? true,
                    Timings = new TimingInfo
                    {
                        DurationMs = (long)stopwatch.ElapsedMilliseconds
                    }
                };

                await _leaseClient.FailAsync(failRequest, cancellationToken: cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Processing cancelled for lease {LeaseId}", lease.LeaseId);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Error processing lease {LeaseId} for run {RunId}", lease.LeaseId, lease.RunId);

            // Report failure
            try
            {
                var failRequest = new FailRequest
                {
                    LeaseId = lease.LeaseId,
                    RunId = lease.RunId,
                    NodeId = _options.NodeId,
                    ErrorMessage = ex.Message,
                    ErrorDetails = ex.ToString(),
                    Retryable = true,
                    Timings = new TimingInfo
                    {
                        DurationMs = (long)stopwatch.ElapsedMilliseconds
                    }
                };

                await _leaseClient.FailAsync(failRequest, cancellationToken: cancellationToken);
            }
            catch (Exception failEx)
            {
                _logger.LogError(failEx, "Error reporting failure for lease {LeaseId}", lease.LeaseId);
            }
        }
        finally
        {
            await _activeLeasesLock.WaitAsync(cancellationToken);
            try
            {
                _activeLeases--;
            }
            finally
            {
                _activeLeasesLock.Release();
            }
        }
    }

    public int GetActiveLeaseCount()
    {
        _activeLeasesLock.Wait();
        try
        {
            return _activeLeases;
        }
        finally
        {
            _activeLeasesLock.Release();
        }
    }

    public int GetAvailableSlots()
    {
        return _options.MaxConcurrentLeases - GetActiveLeaseCount();
    }
}
