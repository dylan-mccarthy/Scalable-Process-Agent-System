namespace Node.Runtime.Configuration;

/// <summary>
/// Configuration options for HTTP output connector.
/// </summary>
public sealed class HttpOutputConnectorOptions
{
    /// <summary>
    /// Base URL for the HTTP endpoint (e.g., "https://api.example.com").
    /// </summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// HTTP method to use for sending messages (default: POST).
    /// </summary>
    public string HttpMethod { get; set; } = "POST";

    /// <summary>
    /// Maximum number of retry attempts for retryable errors (5xx status codes).
    /// Default is 3 as specified in SAD.
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>
    /// Base delay for exponential backoff in milliseconds.
    /// Default is 100ms.
    /// </summary>
    public int BaseBackoffMs { get; set; } = 100;

    /// <summary>
    /// Maximum delay for exponential backoff in milliseconds.
    /// Default is 5000ms (5 seconds).
    /// </summary>
    public int MaxBackoffMs { get; set; } = 5000;

    /// <summary>
    /// Whether to add jitter to retry delays to avoid thundering herd.
    /// Default is true as specified in SAD.
    /// </summary>
    public bool UseJitter { get; set; } = true;

    /// <summary>
    /// Timeout for HTTP requests in seconds.
    /// Default is 30 seconds.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Name of the header to use for idempotency key.
    /// Default is "Idempotency-Key".
    /// </summary>
    public string IdempotencyKeyHeader { get; set; } = "Idempotency-Key";

    /// <summary>
    /// Additional headers to include in all requests.
    /// </summary>
    public Dictionary<string, string> DefaultHeaders { get; set; } = new();
}
