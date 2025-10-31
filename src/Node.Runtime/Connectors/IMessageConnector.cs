namespace Node.Runtime.Connectors;

/// <summary>
/// Base interface for all message connectors (input and output).
/// </summary>
public interface IMessageConnector
{
    /// <summary>
    /// Gets the connector type (e.g., "ServiceBus", "Http").
    /// </summary>
    string ConnectorType { get; }

    /// <summary>
    /// Initializes the connector with the given configuration.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task InitializeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Closes the connector and releases resources.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task CloseAsync(CancellationToken cancellationToken = default);
}
