using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Node.Runtime.Configuration;
using Node.Runtime.Observability;
using System.Diagnostics;

namespace Node.Runtime.Connectors;

/// <summary>
/// Azure Service Bus input connector implementation.
/// Provides reliable message reception with prefetch, DLQ support, and poison message detection.
/// </summary>
public sealed class ServiceBusInputConnector : IInputConnector, IAsyncDisposable
{
    private readonly ILogger<ServiceBusInputConnector> _logger;
    private readonly ServiceBusConnectorOptions _options;
    private ServiceBusClient? _client;
    private ServiceBusReceiver? _receiver;
    private bool _isInitialized;

    /// <inheritdoc/>
    public string ConnectorType => "ServiceBus";

    /// <summary>
    /// Initializes a new instance of the <see cref="ServiceBusInputConnector"/> class.
    /// </summary>
    /// <param name="options">Service Bus connector configuration options.</param>
    /// <param name="logger">Logger instance.</param>
    public ServiceBusInputConnector(
        IOptions<ServiceBusConnectorOptions> options,
        ILogger<ServiceBusInputConnector> logger)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        using var activity = TelemetryConfig.ActivitySource.StartActivity("ServiceBusConnector.Initialize");
        activity?.SetTag("connector.type", "ServiceBus");

        if (_isInitialized)
        {
            _logger.LogWarning("Service Bus connector is already initialized");
            return;
        }

        if (string.IsNullOrWhiteSpace(_options.ConnectionString))
        {
            throw new InvalidOperationException("Service Bus connection string is not configured");
        }

        if (string.IsNullOrWhiteSpace(_options.QueueName))
        {
            throw new InvalidOperationException("Service Bus queue name is not configured");
        }

        _logger.LogInformation(
            "Initializing Service Bus connector for queue: {QueueName}, PrefetchCount: {PrefetchCount}",
            _options.QueueName,
            _options.PrefetchCount);

        try
        {
            _client = new ServiceBusClient(_options.ConnectionString);

            var receiverOptions = new ServiceBusReceiverOptions
            {
                ReceiveMode = _options.ReceiveMode.Equals("ReceiveAndDelete", StringComparison.OrdinalIgnoreCase)
                    ? ServiceBusReceiveMode.ReceiveAndDelete
                    : ServiceBusReceiveMode.PeekLock,
                PrefetchCount = _options.PrefetchCount
            };

            _receiver = _client.CreateReceiver(_options.QueueName, receiverOptions);

            _isInitialized = true;

            _logger.LogInformation(
                "Service Bus connector initialized successfully for queue: {QueueName}",
                _options.QueueName);

            activity?.SetTag("queue.name", _options.QueueName);
            activity?.SetTag("prefetch.count", _options.PrefetchCount);
            activity?.SetTag("receive.mode", receiverOptions.ReceiveMode.ToString());
            activity?.SetStatus(ActivityStatusCode.Ok);
        }
        catch (Azure.Messaging.ServiceBus.ServiceBusException sbEx)
        {
            _logger.LogError(sbEx, "Service Bus error initializing connector: {Reason}", sbEx.Reason);
            activity?.SetStatus(ActivityStatusCode.Error, $"Service Bus error: {sbEx.Reason}");
            activity?.AddEvent(new ActivityEvent("exception", tags: new ActivityTagsCollection { { "exception.type", "ServiceBusException" }, { "exception.reason", sbEx.Reason.ToString() } }));
            throw;
        }
        catch (UnauthorizedAccessException uaEx)
        {
            _logger.LogError(uaEx, "Unauthorized access to Service Bus - check connection string and permissions");
            activity?.SetStatus(ActivityStatusCode.Error, "Unauthorized access");
            activity?.AddEvent(new ActivityEvent("exception", tags: new ActivityTagsCollection { { "exception.type", "UnauthorizedAccessException" }, { "exception.message", uaEx.Message } }));
            throw;
        }
        catch (ArgumentException argEx)
        {
            _logger.LogError(argEx, "Invalid Service Bus configuration");
            activity?.SetStatus(ActivityStatusCode.Error, "Invalid configuration");
            activity?.AddEvent(new ActivityEvent("exception", tags: new ActivityTagsCollection { { "exception.type", "ArgumentException" }, { "exception.message", argEx.Message } }));
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error initializing Service Bus connector");
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddEvent(new ActivityEvent("exception", tags: new ActivityTagsCollection { { "exception.type", ex.GetType().FullName }, { "exception.message", ex.Message } }));
            throw;
        }

        await Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ReceivedMessage>> ReceiveMessagesAsync(
        int maxMessages = 1,
        TimeSpan? maxWaitTime = null,
        CancellationToken cancellationToken = default)
    {
        using var activity = TelemetryConfig.ActivitySource.StartActivity("ServiceBusConnector.ReceiveMessages");
        activity?.SetTag("connector.type", "ServiceBus");
        activity?.SetTag("max.messages", maxMessages);

        EnsureInitialized();

        var waitTime = maxWaitTime ?? _options.MaxWaitTime;
        var messages = new List<ReceivedMessage>();

        try
        {
            _logger.LogDebug("Receiving up to {MaxMessages} messages with max wait time {WaitTime}",
                maxMessages, waitTime);

            var serviceBusMessages = await _receiver!.ReceiveMessagesAsync(
                maxMessages,
                waitTime,
                cancellationToken);

            foreach (var sbMessage in serviceBusMessages)
            {
                var receivedMessage = ConvertToReceivedMessage(sbMessage);
                messages.Add(receivedMessage);

                _logger.LogDebug(
                    "Received message: MessageId={MessageId}, DeliveryCount={DeliveryCount}",
                    receivedMessage.MessageId,
                    receivedMessage.DeliveryCount);

                // Check for poison messages (exceeded max delivery count)
                if (receivedMessage.DeliveryCount > _options.MaxDeliveryCount)
                {
                    _logger.LogWarning(
                        "Poison message detected: MessageId={MessageId}, DeliveryCount={DeliveryCount}, MaxDeliveryCount={MaxDeliveryCount}",
                        receivedMessage.MessageId,
                        receivedMessage.DeliveryCount,
                        _options.MaxDeliveryCount);

                    activity?.AddEvent(new ActivityEvent("poison_message_detected",
                        tags: new ActivityTagsCollection
                        {
                            { "message.id", receivedMessage.MessageId },
                            { "delivery.count", receivedMessage.DeliveryCount }
                        }));
                }
            }

            _logger.LogInformation("Received {MessageCount} messages from Service Bus", messages.Count);
            activity?.SetTag("messages.received", messages.Count);
            activity?.SetTag("queue.name", _options.QueueName);
            activity?.SetStatus(ActivityStatusCode.Ok);

            return messages;
        }
        catch (Azure.Messaging.ServiceBus.ServiceBusException sbEx) when (sbEx.Reason == Azure.Messaging.ServiceBus.ServiceBusFailureReason.ServiceTimeout)
        {
            _logger.LogWarning(sbEx, "Timeout receiving messages from Service Bus");
            activity?.SetStatus(ActivityStatusCode.Error, "Service timeout");
            activity?.AddEvent(new ActivityEvent("exception", tags: new ActivityTagsCollection { { "exception.type", "ServiceBusException" }, { "exception.reason", "ServiceTimeout" } }));
            throw;
        }
        catch (Azure.Messaging.ServiceBus.ServiceBusException sbEx)
        {
            _logger.LogError(sbEx, "Service Bus error receiving messages: {Reason}", sbEx.Reason);
            activity?.SetStatus(ActivityStatusCode.Error, $"Service Bus error: {sbEx.Reason}");
            activity?.AddEvent(new ActivityEvent("exception", tags: new ActivityTagsCollection { { "exception.type", "ServiceBusException" }, { "exception.reason", sbEx.Reason.ToString() } }));
            throw;
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Message receive operation cancelled");
            activity?.SetStatus(ActivityStatusCode.Error, "Cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error receiving messages from Service Bus");
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddEvent(new ActivityEvent("exception", tags: new ActivityTagsCollection { { "exception.type", ex.GetType().FullName }, { "exception.message", ex.Message } }));
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<MessageCompletionResult> CompleteMessageAsync(
        ReceivedMessage message,
        CancellationToken cancellationToken = default)
    {
        using var activity = TelemetryConfig.ActivitySource.StartActivity("ServiceBusConnector.CompleteMessage");
        activity?.SetTag("connector.type", "ServiceBus");
        activity?.SetTag("message.id", message.MessageId);

        EnsureInitialized();

        try
        {
            var sbMessage = GetServiceBusMessage(message);

            await _receiver!.CompleteMessageAsync(sbMessage, cancellationToken);

            _logger.LogDebug("Completed message: MessageId={MessageId}", message.MessageId);
            activity?.SetStatus(ActivityStatusCode.Ok);

            return new MessageCompletionResult { Success = true };
        }
        catch (Azure.Messaging.ServiceBus.ServiceBusException sbEx) when (sbEx.Reason == Azure.Messaging.ServiceBus.ServiceBusFailureReason.MessageLockLost)
        {
            _logger.LogWarning(sbEx, "Cannot complete message {MessageId} - lock lost", message.MessageId);
            activity?.SetStatus(ActivityStatusCode.Error, "Lock lost");
            activity?.AddEvent(new ActivityEvent("exception", tags: new ActivityTagsCollection { { "exception.type", "ServiceBusException" }, { "exception.reason", "MessageLockLost" } }));

            return new MessageCompletionResult
            {
                Success = false,
                ErrorMessage = "Message lock lost"
            };
        }
        catch (Azure.Messaging.ServiceBus.ServiceBusException sbEx)
        {
            _logger.LogError(sbEx, "Service Bus error completing message {MessageId}: {Reason}", message.MessageId, sbEx.Reason);
            activity?.SetStatus(ActivityStatusCode.Error, $"Service Bus error: {sbEx.Reason}");
            activity?.AddEvent(new ActivityEvent("exception", tags: new ActivityTagsCollection { { "exception.type", "ServiceBusException" }, { "exception.reason", sbEx.Reason.ToString() } }));

            return new MessageCompletionResult
            {
                Success = false,
                ErrorMessage = $"Service Bus error: {sbEx.Reason}"
            };
        }
        catch (InvalidOperationException invalidOpEx)
        {
            _logger.LogError(invalidOpEx, "Invalid operation completing message {MessageId} - message may already be settled", message.MessageId);
            activity?.SetStatus(ActivityStatusCode.Error, "Invalid operation");
            activity?.AddEvent(new ActivityEvent("exception", tags: new ActivityTagsCollection { { "exception.type", "InvalidOperationException" }, { "exception.message", invalidOpEx.Message } }));

            return new MessageCompletionResult
            {
                Success = false,
                ErrorMessage = "Message already settled"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error completing message: MessageId={MessageId}", message.MessageId);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddEvent(new ActivityEvent("exception", tags: new ActivityTagsCollection { { "exception.type", ex.GetType().FullName }, { "exception.message", ex.Message } }));

            return new MessageCompletionResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <inheritdoc/>
    public async Task<MessageCompletionResult> AbandonMessageAsync(
        ReceivedMessage message,
        CancellationToken cancellationToken = default)
    {
        using var activity = TelemetryConfig.ActivitySource.StartActivity("ServiceBusConnector.AbandonMessage");
        activity?.SetTag("connector.type", "ServiceBus");
        activity?.SetTag("message.id", message.MessageId);

        EnsureInitialized();

        try
        {
            var sbMessage = GetServiceBusMessage(message);

            await _receiver!.AbandonMessageAsync(sbMessage, cancellationToken: cancellationToken);

            _logger.LogDebug("Abandoned message: MessageId={MessageId}", message.MessageId);
            activity?.SetStatus(ActivityStatusCode.Ok);

            return new MessageCompletionResult { Success = true };
        }
        catch (Azure.Messaging.ServiceBus.ServiceBusException sbEx) when (sbEx.Reason == Azure.Messaging.ServiceBus.ServiceBusFailureReason.MessageLockLost)
        {
            _logger.LogWarning(sbEx, "Cannot abandon message {MessageId} - lock lost", message.MessageId);
            activity?.SetStatus(ActivityStatusCode.Error, "Lock lost");
            activity?.AddEvent(new ActivityEvent("exception", tags: new ActivityTagsCollection { { "exception.type", "ServiceBusException" }, { "exception.reason", "MessageLockLost" } }));

            return new MessageCompletionResult
            {
                Success = false,
                ErrorMessage = "Message lock lost"
            };
        }
        catch (Azure.Messaging.ServiceBus.ServiceBusException sbEx)
        {
            _logger.LogError(sbEx, "Service Bus error abandoning message {MessageId}: {Reason}", message.MessageId, sbEx.Reason);
            activity?.SetStatus(ActivityStatusCode.Error, $"Service Bus error: {sbEx.Reason}");
            activity?.AddEvent(new ActivityEvent("exception", tags: new ActivityTagsCollection { { "exception.type", "ServiceBusException" }, { "exception.reason", sbEx.Reason.ToString() } }));

            return new MessageCompletionResult
            {
                Success = false,
                ErrorMessage = $"Service Bus error: {sbEx.Reason}"
            };
        }
        catch (InvalidOperationException invalidOpEx)
        {
            _logger.LogError(invalidOpEx, "Invalid operation abandoning message {MessageId} - message may already be settled", message.MessageId);
            activity?.SetStatus(ActivityStatusCode.Error, "Invalid operation");
            activity?.AddEvent(new ActivityEvent("exception", tags: new ActivityTagsCollection { { "exception.type", "InvalidOperationException" }, { "exception.message", invalidOpEx.Message } }));

            return new MessageCompletionResult
            {
                Success = false,
                ErrorMessage = "Message already settled"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error abandoning message: MessageId={MessageId}", message.MessageId);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddEvent(new ActivityEvent("exception", tags: new ActivityTagsCollection { { "exception.type", ex.GetType().FullName }, { "exception.message", ex.Message } }));

            return new MessageCompletionResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <inheritdoc/>
    public async Task<MessageCompletionResult> DeadLetterMessageAsync(
        ReceivedMessage message,
        string reason,
        string? errorDescription = null,
        CancellationToken cancellationToken = default)
    {
        using var activity = TelemetryConfig.ActivitySource.StartActivity("ServiceBusConnector.DeadLetterMessage");
        activity?.SetTag("connector.type", "ServiceBus");
        activity?.SetTag("message.id", message.MessageId);
        activity?.SetTag("dlq.reason", reason);
        if (!string.IsNullOrEmpty(errorDescription))
        {
            activity?.SetTag("dlq.error_description", errorDescription);
        }

        EnsureInitialized();

        try
        {
            var sbMessage = GetServiceBusMessage(message);

            await _receiver!.DeadLetterMessageAsync(
                sbMessage,
                reason,
                errorDescription,
                cancellationToken);

            _logger.LogWarning(
                "Dead-lettered message: MessageId={MessageId}, Reason={Reason}, Description={Description}",
                message.MessageId,
                reason,
                errorDescription);

            activity?.SetStatus(ActivityStatusCode.Ok);

            return new MessageCompletionResult { Success = true };
        }
        catch (Azure.Messaging.ServiceBus.ServiceBusException sbEx) when (sbEx.Reason == Azure.Messaging.ServiceBus.ServiceBusFailureReason.MessageLockLost)
        {
            _logger.LogWarning(sbEx, "Cannot dead-letter message {MessageId} - lock lost", message.MessageId);
            activity?.SetStatus(ActivityStatusCode.Error, "Lock lost");
            activity?.AddEvent(new ActivityEvent("exception", tags: new ActivityTagsCollection { { "exception.type", "ServiceBusException" }, { "exception.reason", "MessageLockLost" } }));

            return new MessageCompletionResult
            {
                Success = false,
                ErrorMessage = "Message lock lost"
            };
        }
        catch (Azure.Messaging.ServiceBus.ServiceBusException sbEx)
        {
            _logger.LogError(
                sbEx,
                "Service Bus error dead-lettering message {MessageId}: {Reason}",
                message.MessageId,
                sbEx.Reason);
            activity?.SetStatus(ActivityStatusCode.Error, $"Service Bus error: {sbEx.Reason}");
            activity?.AddEvent(new ActivityEvent("exception", tags: new ActivityTagsCollection { { "exception.type", "ServiceBusException" }, { "exception.reason", sbEx.Reason.ToString() } }));

            return new MessageCompletionResult
            {
                Success = false,
                ErrorMessage = $"Service Bus error: {sbEx.Reason}"
            };
        }
        catch (InvalidOperationException invalidOpEx)
        {
            _logger.LogError(
                invalidOpEx,
                "Invalid operation dead-lettering message {MessageId} - message may already be settled",
                message.MessageId);
            activity?.SetStatus(ActivityStatusCode.Error, "Invalid operation");
            activity?.AddEvent(new ActivityEvent("exception", tags: new ActivityTagsCollection { { "exception.type", "InvalidOperationException" }, { "exception.message", invalidOpEx.Message } }));

            return new MessageCompletionResult
            {
                Success = false,
                ErrorMessage = "Message already settled"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Unexpected error dead-lettering message: MessageId={MessageId}, Reason={Reason}",
                message.MessageId,
                reason);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddEvent(new ActivityEvent("exception", tags: new ActivityTagsCollection { { "exception.type", ex.GetType().FullName }, { "exception.message", ex.Message } }));

            return new MessageCompletionResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <inheritdoc/>
    public async Task CloseAsync(CancellationToken cancellationToken = default)
    {
        using var activity = TelemetryConfig.ActivitySource.StartActivity("ServiceBusConnector.Close");
        activity?.SetTag("connector.type", "ServiceBus");

        if (!_isInitialized)
        {
            return;
        }

        _logger.LogInformation("Closing Service Bus connector");

        try
        {
            if (_receiver != null)
            {
                await _receiver.CloseAsync(cancellationToken);
                await _receiver.DisposeAsync();
                _receiver = null;
            }

            if (_client != null)
            {
                await _client.DisposeAsync();
                _client = null;
            }

            _isInitialized = false;

            _logger.LogInformation("Service Bus connector closed successfully");
            activity?.SetStatus(ActivityStatusCode.Ok);
        }
        catch (Azure.Messaging.ServiceBus.ServiceBusException sbEx)
        {
            _logger.LogError(sbEx, "Service Bus error closing connector: {Reason}", sbEx.Reason);
            activity?.SetStatus(ActivityStatusCode.Error, $"Service Bus error: {sbEx.Reason}");
            activity?.AddEvent(new ActivityEvent("exception", tags: new ActivityTagsCollection { { "exception.type", "ServiceBusException" }, { "exception.reason", sbEx.Reason.ToString() } }));
            throw;
        }
        catch (ObjectDisposedException)
        {
            _logger.LogWarning("Service Bus connector already disposed");
            activity?.SetStatus(ActivityStatusCode.Ok);
            // Don't throw - already disposed is acceptable
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error closing Service Bus connector");
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddEvent(new ActivityEvent("exception", tags: new ActivityTagsCollection { { "exception.type", ex.GetType().FullName }, { "exception.message", ex.Message } }));
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
        if (!_isInitialized || _receiver == null)
        {
            throw new InvalidOperationException(
                "Service Bus connector is not initialized. Call InitializeAsync first.");
        }
    }

    private static ReceivedMessage ConvertToReceivedMessage(ServiceBusReceivedMessage sbMessage)
    {
        var properties = new Dictionary<string, object>();

        foreach (var kvp in sbMessage.ApplicationProperties)
        {
            properties[kvp.Key] = kvp.Value;
        }

        return new ReceivedMessage
        {
            MessageId = sbMessage.MessageId,
            Body = sbMessage.Body.ToString(),
            CorrelationId = sbMessage.CorrelationId,
            Properties = properties,
            DeliveryCount = sbMessage.DeliveryCount,
            EnqueuedTime = sbMessage.EnqueuedTime,
            AckContext = sbMessage
        };
    }

    private static ServiceBusReceivedMessage GetServiceBusMessage(ReceivedMessage message)
    {
        if (message.AckContext is not ServiceBusReceivedMessage sbMessage)
        {
            throw new InvalidOperationException(
                $"Invalid AckContext: expected {nameof(ServiceBusReceivedMessage)}");
        }

        return sbMessage;
    }
}
