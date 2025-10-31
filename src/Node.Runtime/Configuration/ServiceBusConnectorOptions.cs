namespace Node.Runtime.Configuration;

/// <summary>
/// Configuration options for Azure Service Bus connector.
/// </summary>
public sealed class ServiceBusConnectorOptions
{
    /// <summary>
    /// Service Bus connection string or fully qualified namespace (e.g., "myservicebus.servicebus.windows.net").
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Queue name to receive messages from.
    /// </summary>
    public string QueueName { get; set; } = string.Empty;

    /// <summary>
    /// Number of messages to prefetch for better throughput.
    /// Default is 16 as specified in SAD.
    /// </summary>
    public int PrefetchCount { get; set; } = 16;

    /// <summary>
    /// Maximum number of concurrent message processing operations.
    /// </summary>
    public int MaxConcurrentCalls { get; set; } = 5;

    /// <summary>
    /// Maximum wait time for receiving messages.
    /// </summary>
    public TimeSpan MaxWaitTime { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Maximum number of delivery attempts before moving to DLQ.
    /// Default is 3 as specified in SAD.
    /// </summary>
    public int MaxDeliveryCount { get; set; } = 3;

    /// <summary>
    /// Whether to automatically complete messages after successful processing.
    /// </summary>
    public bool AutoComplete { get; set; } = false;

    /// <summary>
    /// Receive mode: PeekLock or ReceiveAndDelete.
    /// Default is PeekLock for reliability.
    /// </summary>
    public string ReceiveMode { get; set; } = "PeekLock";
}
