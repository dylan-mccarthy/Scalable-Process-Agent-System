using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Node.Runtime.Configuration;
using Polly;
using Polly.Retry;
using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;

namespace Node.Runtime.Connectors;

/// <summary>
/// HTTP output connector implementation with retry/backoff and idempotency support.
/// Provides reliable HTTP POST operations with exponential backoff for transient failures.
/// </summary>
public sealed class HttpOutputConnector : IOutputConnector, IAsyncDisposable
{
    private readonly ILogger<HttpOutputConnector> _logger;
    private readonly HttpOutputConnectorOptions _options;
    private readonly IHttpClientFactory _httpClientFactory;
    private HttpClient? _httpClient;
    private AsyncRetryPolicy<HttpResponseMessage>? _retryPolicy;
    private bool _isInitialized;

    private static readonly ActivitySource ActivitySource = new("Node.Runtime.Connectors.Http", "1.0.0");

    // Jitter factor (20% of delay) to avoid thundering herd
    private const double JitterFactor = 0.2;

    /// <inheritdoc/>
    public string ConnectorType => "Http";

    /// <summary>
    /// Initializes a new instance of the <see cref="HttpOutputConnector"/> class.
    /// </summary>
    /// <param name="options">HTTP output connector configuration options.</param>
    /// <param name="httpClientFactory">HTTP client factory for creating HTTP clients.</param>
    /// <param name="logger">Logger instance.</param>
    public HttpOutputConnector(
        IOptions<HttpOutputConnectorOptions> options,
        IHttpClientFactory httpClientFactory,
        ILogger<HttpOutputConnector> logger)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
#pragma warning disable CS1998 // Async method lacks 'await' operators
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
#pragma warning restore CS1998
    {
        using var activity = ActivitySource.StartActivity("HttpOutputConnector.Initialize");

        if (_isInitialized)
        {
            _logger.LogWarning("HTTP output connector is already initialized");
            return;
        }

        if (string.IsNullOrWhiteSpace(_options.BaseUrl))
        {
            throw new InvalidOperationException("HTTP output connector base URL is not configured");
        }

        if (!Uri.TryCreate(_options.BaseUrl, UriKind.Absolute, out var baseUri))
        {
            throw new InvalidOperationException($"Invalid base URL: {_options.BaseUrl}");
        }

        _logger.LogInformation(
            "Initializing HTTP output connector for base URL: {BaseUrl}, MaxRetries: {MaxRetries}",
            _options.BaseUrl,
            _options.MaxRetryAttempts);

        try
        {
            _httpClient = _httpClientFactory.CreateClient();
            _httpClient.BaseAddress = baseUri;
            _httpClient.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);

            // Add default headers
            foreach (var header in _options.DefaultHeaders)
            {
                _httpClient.DefaultRequestHeaders.TryAddWithoutValidation(header.Key, header.Value);
            }

            // Configure Polly retry policy
            _retryPolicy = BuildRetryPolicy();

            _isInitialized = true;

            _logger.LogInformation(
                "HTTP output connector initialized successfully for base URL: {BaseUrl}",
                _options.BaseUrl);

            activity?.SetTag("base.url", _options.BaseUrl);
            activity?.SetTag("max.retries", _options.MaxRetryAttempts);
        }
        catch (UriFormatException ex)
        {
            _logger.LogError(ex, "Invalid base URL format: {BaseUrl}", _options.BaseUrl);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
        catch (ArgumentException ex)
        {
            _logger.LogError(ex, "Invalid HTTP configuration: {Message}", ex.Message);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Invalid operation during HTTP connector initialization: {Message}", ex.Message);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error initializing HTTP output connector");
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<SendMessageResult> SendMessageAsync(
        OutgoingMessage message,
        CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("HttpOutputConnector.SendMessage");
        activity?.SetTag("message.id", message.MessageId);

        EnsureInitialized();

        try
        {
            _logger.LogDebug(
                "Sending message: MessageId={MessageId}, CorrelationId={CorrelationId}",
                message.MessageId,
                message.CorrelationId);

            // Execute with retry policy - create new request for each attempt
            var response = await _retryPolicy!.ExecuteAsync(async () =>
            {
                var request = BuildHttpRequest(message);
                return await _httpClient!.SendAsync(request, cancellationToken);
            });

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            var success = response.IsSuccessStatusCode;

            if (success)
            {
                _logger.LogInformation(
                    "Successfully sent message: MessageId={MessageId}, StatusCode={StatusCode}",
                    message.MessageId,
                    (int)response.StatusCode);

                activity?.SetTag("http.status_code", (int)response.StatusCode);
            }
            else
            {
                _logger.LogWarning(
                    "Failed to send message: MessageId={MessageId}, StatusCode={StatusCode}, Response={Response}",
                    message.MessageId,
                    (int)response.StatusCode,
                    responseBody);

                activity?.SetStatus(ActivityStatusCode.Error, $"HTTP {(int)response.StatusCode}");
            }

            return new SendMessageResult
            {
                Success = success,
                StatusCode = (int)response.StatusCode,
                ResponseBody = responseBody,
                IsRetryable = IsRetryableStatusCode(response.StatusCode)
            };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(
                ex,
                "HTTP request exception while sending message: MessageId={MessageId}",
                message.MessageId);

            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);

            return new SendMessageResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                IsRetryable = true // Network errors are generally retryable
            };
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(
                ex,
                "Timeout while sending message: MessageId={MessageId}",
                message.MessageId);

            activity?.SetStatus(ActivityStatusCode.Error, "Timeout");

            return new SendMessageResult
            {
                Success = false,
                ErrorMessage = "Request timeout",
                IsRetryable = true
            };
        }
        catch (JsonException ex)
        {
            _logger.LogError(
                ex,
                "JSON serialization error while sending message: MessageId={MessageId}",
                message.MessageId);

            activity?.SetStatus(ActivityStatusCode.Error, "Serialization error");

            return new SendMessageResult
            {
                Success = false,
                ErrorMessage = $"JSON error: {ex.Message}",
                IsRetryable = false
            };
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(
                ex,
                "Invalid operation while sending message: MessageId={MessageId}",
                message.MessageId);

            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);

            return new SendMessageResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                IsRetryable = false
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Unexpected error while sending message: MessageId={MessageId}",
                message.MessageId);

            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);

            return new SendMessageResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                IsRetryable = false
            };
        }
    }

    /// <inheritdoc/>
#pragma warning disable CS1998 // Async method lacks 'await' operators
    public async Task CloseAsync(CancellationToken cancellationToken = default)
#pragma warning restore CS1998
    {
        using var activity = ActivitySource.StartActivity("HttpOutputConnector.Close");

        if (!_isInitialized)
        {
            return;
        }

        _logger.LogInformation("Closing HTTP output connector");

        try
        {
            _httpClient?.Dispose();
            _httpClient = null;
            _retryPolicy = null;
            _isInitialized = false;

            _logger.LogInformation("HTTP output connector closed successfully");
        }
        catch (ObjectDisposedException ex)
        {
            _logger.LogWarning(ex, "HTTP client already disposed during close");
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Invalid operation while closing HTTP output connector");
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error closing HTTP output connector");
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await CloseAsync();
    }

    private void EnsureInitialized()
    {
        if (!_isInitialized || _httpClient == null || _retryPolicy == null)
        {
            throw new InvalidOperationException(
                "HTTP output connector is not initialized. Call InitializeAsync first.");
        }
    }

    private HttpRequestMessage BuildHttpRequest(OutgoingMessage message)
    {
        var request = new HttpRequestMessage(
            new HttpMethod(_options.HttpMethod),
            _options.BaseUrl);

        // Add idempotency key header
        request.Headers.TryAddWithoutValidation(
            _options.IdempotencyKeyHeader,
            message.MessageId);

        // Add correlation ID if present
        if (!string.IsNullOrWhiteSpace(message.CorrelationId))
        {
            request.Headers.TryAddWithoutValidation("X-Correlation-ID", message.CorrelationId);
        }

        // Add custom headers
        foreach (var header in message.Headers)
        {
            request.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        // Set content
        request.Content = new StringContent(
            message.Body,
            Encoding.UTF8,
            message.ContentType);

        return request;
    }

    private AsyncRetryPolicy<HttpResponseMessage> BuildRetryPolicy()
    {
        return Policy<HttpResponseMessage>
            .Handle<HttpRequestException>()
            .Or<TaskCanceledException>()
            .OrResult(response => IsRetryableStatusCode(response.StatusCode))
            .WaitAndRetryAsync(
                _options.MaxRetryAttempts,
                retryAttempt =>
                {
                    var baseDelay = TimeSpan.FromMilliseconds(_options.BaseBackoffMs);
                    var exponentialDelay = TimeSpan.FromMilliseconds(
                        _options.BaseBackoffMs * Math.Pow(2, retryAttempt - 1));

                    // Cap at max delay
                    var delay = exponentialDelay > TimeSpan.FromMilliseconds(_options.MaxBackoffMs)
                        ? TimeSpan.FromMilliseconds(_options.MaxBackoffMs)
                        : exponentialDelay;

                    // Add jitter if enabled
                    if (_options.UseJitter)
                    {
                        var jitter = Random.Shared.Next(0, (int)(delay.TotalMilliseconds * JitterFactor));
                        delay = delay.Add(TimeSpan.FromMilliseconds(jitter));
                    }

                    return delay;
                },
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    var statusCode = outcome.Result?.StatusCode;
                    _logger.LogWarning(
                        "Retrying HTTP request (attempt {Attempt}/{MaxAttempts}), StatusCode={StatusCode}, Delay={Delay}ms",
                        retryCount,
                        _options.MaxRetryAttempts,
                        statusCode.HasValue ? (int)statusCode : null,
                        timespan.TotalMilliseconds);
                });
    }

    private static bool IsRetryableStatusCode(HttpStatusCode statusCode)
    {
        // Retry on 5xx server errors and specific 4xx errors
        return statusCode >= HttpStatusCode.InternalServerError || // 5xx
               statusCode == HttpStatusCode.RequestTimeout || // 408
               statusCode == (HttpStatusCode)429; // Too Many Requests
    }
}
