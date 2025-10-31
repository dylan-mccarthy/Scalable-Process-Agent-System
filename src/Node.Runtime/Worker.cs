using Node.Runtime.Services;
using Node.Runtime.Configuration;
using Microsoft.Extensions.Options;

namespace Node.Runtime;

/// <summary>
/// Background service that manages the node runtime lifecycle.
/// </summary>
public sealed class Worker : BackgroundService
{
    private readonly INodeRegistrationService _registrationService;
    private readonly ILeasePullService _leasePullService;
    private readonly NodeRuntimeOptions _options;
    private readonly ILogger<Worker> _logger;
    private Timer? _heartbeatTimer;

    public Worker(
        INodeRegistrationService registrationService,
        ILeasePullService leasePullService,
        IOptions<NodeRuntimeOptions> options,
        ILogger<Worker> logger)
    {
        _registrationService = registrationService;
        _leasePullService = leasePullService;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Node Runtime starting for node {NodeId}", _options.NodeId);

        // Register the node with the Control Plane
        var registered = await _registrationService.RegisterNodeAsync(stoppingToken);
        if (!registered)
        {
            _logger.LogError("Failed to register node {NodeId}. Shutting down.", _options.NodeId);
            return;
        }

        // Start the heartbeat timer
        StartHeartbeatTimer();

        // Start the lease pull service
        await _leasePullService.StartAsync(stoppingToken);

        _logger.LogInformation("Node Runtime started successfully for node {NodeId}", _options.NodeId);

        // Keep the worker running until cancellation
        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Node Runtime stopping for node {NodeId}", _options.NodeId);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping Node Runtime for node {NodeId}", _options.NodeId);

        // Stop heartbeat timer
        _heartbeatTimer?.Dispose();

        // Stop lease pull service
        await _leasePullService.StopAsync(cancellationToken);

        await base.StopAsync(cancellationToken);

        _logger.LogInformation("Node Runtime stopped for node {NodeId}", _options.NodeId);
    }

    private void StartHeartbeatTimer()
    {
        var interval = TimeSpan.FromSeconds(_options.HeartbeatIntervalSeconds);
        _heartbeatTimer = new Timer(
            async _ => await SendHeartbeatAsync(),
            null,
            interval,
            interval);

        _logger.LogInformation("Heartbeat timer started with interval {IntervalSeconds}s", _options.HeartbeatIntervalSeconds);
    }

    private async Task SendHeartbeatAsync()
    {
        try
        {
            // Get current active runs and available slots from the lease pull service
            var leasePullService = _leasePullService as LeasePullService;
            var activeRuns = leasePullService?.GetActiveLeaseCount() ?? 0;
            var availableSlots = leasePullService?.GetAvailableSlots() ?? _options.MaxConcurrentLeases;

            await _registrationService.SendHeartbeatAsync(activeRuns, availableSlots);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "HTTP error sending heartbeat for node {NodeId}", _options.NodeId);
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogWarning(ex, "Timeout sending heartbeat for node {NodeId}", _options.NodeId);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Invalid operation sending heartbeat for node {NodeId}", _options.NodeId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error sending heartbeat for node {NodeId}", _options.NodeId);
        }
    }
}

