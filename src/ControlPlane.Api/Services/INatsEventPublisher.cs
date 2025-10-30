using ControlPlane.Api.Events;

namespace ControlPlane.Api.Services;

/// <summary>
/// Service for publishing events to NATS JetStream
/// </summary>
public interface INatsEventPublisher
{
    /// <summary>
    /// Publishes a system event to the appropriate JetStream subject
    /// </summary>
    /// <param name="event">The event to publish</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Acknowledgement information from JetStream</returns>
    Task PublishAsync(SystemEvent @event, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ensures that all required JetStream streams are created
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    Task InitializeStreamsAsync(CancellationToken cancellationToken = default);
}
