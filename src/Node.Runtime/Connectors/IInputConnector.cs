namespace Node.Runtime.Connectors;

/// <summary>
/// Represents a received message from an input connector.
/// </summary>
public sealed class ReceivedMessage
{
    /// <summary>
    /// Unique identifier for this message.
    /// </summary>
    public required string MessageId { get; init; }

    /// <summary>
    /// The message body/content.
    /// </summary>
    public required string Body { get; init; }

    /// <summary>
    /// Optional correlation ID for tracking.
    /// </summary>
    public string? CorrelationId { get; init; }

    /// <summary>
    /// Message metadata/properties.
    /// </summary>
    public IReadOnlyDictionary<string, object> Properties { get; init; } = new Dictionary<string, object>();

    /// <summary>
    /// Delivery count (number of times the message has been delivered).
    /// </summary>
    public int DeliveryCount { get; init; }

    /// <summary>
    /// Time when the message was enqueued.
    /// </summary>
    public DateTimeOffset EnqueuedTime { get; init; }

    /// <summary>
    /// Context object for connector-specific acknowledgment (e.g., Service Bus receiver).
    /// </summary>
    public object? AckContext { get; init; }
}

/// <summary>
/// Result of completing or abandoning a message.
/// </summary>
public sealed class MessageCompletionResult
{
    /// <summary>
    /// Indicates whether the operation was successful.
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// Error message if the operation failed.
    /// </summary>
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Interface for input connectors that receive messages from external sources.
/// </summary>
public interface IInputConnector : IMessageConnector
{
    /// <summary>
    /// Receives messages from the input source.
    /// </summary>
    /// <param name="maxMessages">Maximum number of messages to receive.</param>
    /// <param name="maxWaitTime">Maximum time to wait for messages.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Collection of received messages.</returns>
    Task<IReadOnlyList<ReceivedMessage>> ReceiveMessagesAsync(
        int maxMessages = 1,
        TimeSpan? maxWaitTime = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Completes (acknowledges) a message, removing it from the queue.
    /// </summary>
    /// <param name="message">The message to complete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<MessageCompletionResult> CompleteMessageAsync(
        ReceivedMessage message,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Abandons a message, making it available for redelivery.
    /// </summary>
    /// <param name="message">The message to abandon.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<MessageCompletionResult> AbandonMessageAsync(
        ReceivedMessage message,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Dead-letters a message, moving it to the dead-letter queue.
    /// </summary>
    /// <param name="message">The message to dead-letter.</param>
    /// <param name="reason">Reason for dead-lettering.</param>
    /// <param name="errorDescription">Optional error description.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<MessageCompletionResult> DeadLetterMessageAsync(
        ReceivedMessage message,
        string reason,
        string? errorDescription = null,
        CancellationToken cancellationToken = default);
}
