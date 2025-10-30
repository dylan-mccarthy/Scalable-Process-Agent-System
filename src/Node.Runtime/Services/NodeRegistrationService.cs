using System.Net.Http.Json;
using System.Text.Json;
using Node.Runtime.Configuration;
using Microsoft.Extensions.Options;

namespace Node.Runtime.Services;

/// <summary>
/// Service for managing node registration with the Control Plane.
/// </summary>
public interface INodeRegistrationService
{
    /// <summary>
    /// Registers the node with the Control Plane.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if registration was successful, false otherwise.</returns>
    Task<bool> RegisterNodeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a heartbeat to the Control Plane.
    /// </summary>
    /// <param name="activeRuns">Number of currently active runs.</param>
    /// <param name="availableSlots">Number of available execution slots.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if heartbeat was successful, false otherwise.</returns>
    Task<bool> SendHeartbeatAsync(int activeRuns, int availableSlots, CancellationToken cancellationToken = default);
}

/// <summary>
/// Implementation of the node registration service.
/// </summary>
public sealed class NodeRegistrationService : INodeRegistrationService
{
    private readonly HttpClient _httpClient;
    private readonly NodeRuntimeOptions _options;
    private readonly ILogger<NodeRegistrationService> _logger;

    public NodeRegistrationService(
        HttpClient httpClient,
        IOptions<NodeRuntimeOptions> options,
        ILogger<NodeRegistrationService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<bool> RegisterNodeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new
            {
                nodeId = _options.NodeId,
                metadata = _options.Metadata,
                capacity = new
                {
                    slots = _options.Capacity.Slots,
                    cpu = _options.Capacity.Cpu,
                    memory = _options.Capacity.Memory
                }
            };

            var response = await _httpClient.PostAsJsonAsync(
                "/v1/nodes:register",
                request,
                cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Node {NodeId} registered successfully", _options.NodeId);
                return true;
            }

            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Failed to register node {NodeId}. Status: {StatusCode}, Error: {Error}",
                _options.NodeId, response.StatusCode, errorContent);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception occurred while registering node {NodeId}", _options.NodeId);
            return false;
        }
    }

    public async Task<bool> SendHeartbeatAsync(int activeRuns, int availableSlots, CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new
            {
                status = new
                {
                    state = "active",
                    activeRuns,
                    availableSlots
                }
            };

            var response = await _httpClient.PostAsJsonAsync(
                $"/v1/nodes/{_options.NodeId}:heartbeat",
                request,
                cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogDebug("Heartbeat sent successfully for node {NodeId}", _options.NodeId);
                return true;
            }

            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("Failed to send heartbeat for node {NodeId}. Status: {StatusCode}, Error: {Error}",
                _options.NodeId, response.StatusCode, errorContent);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Exception occurred while sending heartbeat for node {NodeId}", _options.NodeId);
            return false;
        }
    }
}
