namespace Node.Runtime.Connectors;

/// <summary>
/// Represents a message to be sent via an output connector.
/// </summary>
public sealed class OutgoingMessage
{
    /// <summary>
    /// Unique identifier for this message (used for idempotency).
    /// </summary>
    public required string MessageId { get; init; }

    /// <summary>
    /// The message body/content to send.
    /// </summary>
    public required string Body { get; init; }

    /// <summary>
    /// Content type of the message body (e.g., "application/json").
    /// </summary>
    public string ContentType { get; init; } = "application/json";

    /// <summary>
    /// Optional correlation ID for tracking.
    /// </summary>
    public string? CorrelationId { get; init; }

    /// <summary>
    /// Additional headers to include in the request.
    /// </summary>
    public IReadOnlyDictionary<string, string> Headers { get; init; } = new Dictionary<string, string>();
}

/// <summary>
/// Result of sending a message via an output connector.
/// </summary>
public sealed class SendMessageResult
{
    /// <summary>
    /// Indicates whether the message was sent successfully.
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// HTTP status code from the response (if applicable).
    /// </summary>
    public int? StatusCode { get; init; }

    /// <summary>
    /// Response body from the target system (if applicable).
    /// </summary>
    public string? ResponseBody { get; init; }

    /// <summary>
    /// Error message if the operation failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Indicates whether the error is retryable (e.g., 5xx errors).
    /// </summary>
    public bool IsRetryable { get; init; }
}

/// <summary>
/// Interface for output connectors that send messages to external systems.
/// </summary>
public interface IOutputConnector : IMessageConnector
{
    /// <summary>
    /// Sends a message to the output destination.
    /// </summary>
    /// <param name="message">The message to send.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result of the send operation.</returns>
    Task<SendMessageResult> SendMessageAsync(
        OutgoingMessage message,
        CancellationToken cancellationToken = default);
}
