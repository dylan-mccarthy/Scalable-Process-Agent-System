using System.Diagnostics;
using Microsoft.Extensions.Options;
using Node.Runtime.Configuration;
using Node.Runtime.Connectors;
using Node.Runtime.Observability;

namespace Node.Runtime.Services;

/// <summary>
/// Service that processes messages from Service Bus input connector with DLQ handling.
/// Implements E2-T8: Routes failed messages to Service Bus DLQ.
/// </summary>
public interface IMessageProcessingService
{
    /// <summary>
    /// Starts processing messages from the input connector.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops processing messages.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task StopAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Implementation of message processing service with DLQ handling.
/// </summary>
public sealed class MessageProcessingService : IMessageProcessingService
{
    private readonly IInputConnector _inputConnector;
    private readonly IAgentExecutor _agentExecutor;
    private readonly ServiceBusConnectorOptions _options;
    private readonly ILogger<MessageProcessingService> _logger;
    private Task? _processingTask;
    private CancellationTokenSource? _processingCts;

    public MessageProcessingService(
        IInputConnector inputConnector,
        IAgentExecutor agentExecutor,
        IOptions<ServiceBusConnectorOptions> options,
        ILogger<MessageProcessingService> logger)
    {
        _inputConnector = inputConnector ?? throw new ArgumentNullException(nameof(inputConnector));
        _agentExecutor = agentExecutor ?? throw new ArgumentNullException(nameof(agentExecutor));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting message processing service for queue: {QueueName}", _options.QueueName);

        // Initialize the input connector
        await _inputConnector.InitializeAsync(cancellationToken);

        // Start the message processing loop
        _processingCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _processingTask = Task.Run(() => ProcessMessagesAsync(_processingCts.Token), _processingCts.Token);

        _logger.LogInformation("Message processing service started successfully");
    }

    /// <inheritdoc/>
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Stopping message processing service");

        if (_processingCts != null)
        {
            await _processingCts.CancelAsync();
        }

        if (_processingTask != null)
        {
            try
            {
                await _processingTask;
            }
            catch (OperationCanceledException)
            {
                // Expected when stopping
            }
        }

        await _inputConnector.CloseAsync(cancellationToken);

        _processingCts?.Dispose();
        _processingCts = null;
        _processingTask = null;

        _logger.LogInformation("Message processing service stopped");
    }

    private async Task ProcessMessagesAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                using var activity = TelemetryConfig.ActivitySource.StartActivity("MessageProcessingService.ProcessMessages");
                activity?.SetTag("queue.name", _options.QueueName);

                // Receive messages from Service Bus
                var messages = await _inputConnector.ReceiveMessagesAsync(
                    maxMessages: _options.MaxConcurrentCalls,
                    maxWaitTime: _options.MaxWaitTime,
                    cancellationToken);

                if (messages.Count == 0)
                {
                    _logger.LogDebug("No messages received, continuing to poll");
                    continue;
                }

                _logger.LogInformation("Received {MessageCount} messages for processing", messages.Count);

                // Process each message concurrently (up to MaxConcurrentCalls)
                var processingTasks = messages.Select(message =>
                    ProcessSingleMessageAsync(message, cancellationToken));

                await Task.WhenAll(processingTasks);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Message processing cancelled");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in message processing loop");
                // Brief delay before retrying to avoid tight loop on persistent errors
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            }
        }
    }

    private async Task ProcessSingleMessageAsync(ReceivedMessage message, CancellationToken cancellationToken)
    {
        using var activity = TelemetryConfig.ActivitySource.StartActivity("MessageProcessingService.ProcessSingleMessage");
        activity?.SetTag("message.id", message.MessageId);
        activity?.SetTag("delivery.count", message.DeliveryCount);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            _logger.LogInformation(
                "Processing message: MessageId={MessageId}, DeliveryCount={DeliveryCount}",
                message.MessageId,
                message.DeliveryCount);

            // Check for poison messages (should not occur with proper Service Bus config)
            // This is a safety check in case DeliveryCount somehow exceeds MaxDeliveryCount
            if (message.DeliveryCount > _options.MaxDeliveryCount)
            {
                await DeadLetterPoisonMessageAsync(message, cancellationToken);
                return;
            }

            // Extract agent specification from message properties
            // In production, this would be more sophisticated
            var agentSpec = CreateAgentSpecFromMessage(message);

            // Execute the agent
            var result = await _agentExecutor.ExecuteAsync(agentSpec, message.Body, cancellationToken);

            stopwatch.Stop();

            if (result.Success)
            {
                // Complete the message on success
                var completionResult = await _inputConnector.CompleteMessageAsync(message, cancellationToken);

                if (completionResult.Success)
                {
                    _logger.LogInformation(
                        "Successfully processed and completed message: MessageId={MessageId}, Duration={DurationMs}ms",
                        message.MessageId,
                        stopwatch.ElapsedMilliseconds);

                    activity?.SetStatus(ActivityStatusCode.Ok);
                }
                else
                {
                    _logger.LogWarning(
                        "Agent execution succeeded but message completion failed: MessageId={MessageId}, Error={Error}",
                        message.MessageId,
                        completionResult.ErrorMessage);
                }
            }
            else
            {
                // Handle failure based on error type
                await HandleProcessingFailureAsync(message, result, cancellationToken);
                stopwatch.Stop();

                activity?.SetStatus(ActivityStatusCode.Error, result.Error);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Message processing cancelled for MessageId={MessageId}", message.MessageId);
            // Abandon the message so it can be reprocessed
            await _inputConnector.AbandonMessageAsync(message, cancellationToken);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Unexpected error processing message: MessageId={MessageId}", message.MessageId);

            // For unexpected errors, abandon the message for retry
            try
            {
                await _inputConnector.AbandonMessageAsync(message, cancellationToken);
            }
            catch (Exception abandonEx)
            {
                _logger.LogError(abandonEx, "Failed to abandon message after error: MessageId={MessageId}", message.MessageId);
            }

            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddEvent(new ActivityEvent("exception",
                tags: new ActivityTagsCollection
                {
                    { "exception.type", ex.GetType().FullName },
                    { "exception.message", ex.Message }
                }));
        }
    }

    private async Task HandleProcessingFailureAsync(
        ReceivedMessage message,
        AgentExecutionResult result,
        CancellationToken cancellationToken)
    {
        // Determine if the error is retryable
        var isRetryable = IsRetryableError(result.Error);

        if (!isRetryable)
        {
            // Non-retryable errors go directly to DLQ
            _logger.LogWarning(
                "Non-retryable error for message: MessageId={MessageId}, Error={Error}",
                message.MessageId,
                result.Error);

            await DeadLetterMessageAsync(
                message,
                "NonRetryableError",
                result.Error ?? "Agent execution failed with non-retryable error",
                cancellationToken);
        }
        else if (message.DeliveryCount >= _options.MaxDeliveryCount)
        {
            // Exceeded max retries - move to DLQ
            _logger.LogWarning(
                "Message exceeded max delivery count: MessageId={MessageId}, DeliveryCount={DeliveryCount}",
                message.MessageId,
                message.DeliveryCount);

            await DeadLetterMessageAsync(
                message,
                "MaxDeliveryCountExceeded",
                $"Message exceeded {_options.MaxDeliveryCount} delivery attempts. Last error: {result.Error}",
                cancellationToken);
        }
        else
        {
            // Retryable error - abandon for redelivery
            _logger.LogInformation(
                "Abandoning message for retry: MessageId={MessageId}, DeliveryCount={DeliveryCount}, Error={Error}",
                message.MessageId,
                message.DeliveryCount,
                result.Error);

            await _inputConnector.AbandonMessageAsync(message, cancellationToken);
        }
    }

    private async Task DeadLetterPoisonMessageAsync(ReceivedMessage message, CancellationToken cancellationToken)
    {
        _logger.LogWarning(
            "Poison message detected - moving to DLQ: MessageId={MessageId}, DeliveryCount={DeliveryCount}",
            message.MessageId,
            message.DeliveryCount);

        await DeadLetterMessageAsync(
            message,
            "PoisonMessage",
            $"Message exceeded maximum delivery count of {_options.MaxDeliveryCount}",
            cancellationToken);
    }

    private async Task DeadLetterMessageAsync(
        ReceivedMessage message,
        string reason,
        string errorDescription,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _inputConnector.DeadLetterMessageAsync(
                message,
                reason,
                errorDescription,
                cancellationToken);

            if (result.Success)
            {
                _logger.LogWarning(
                    "Message moved to DLQ: MessageId={MessageId}, Reason={Reason}",
                    message.MessageId,
                    reason);

                TelemetryConfig.MessagesDeadLetteredCounter.Add(1,
                    new KeyValuePair<string, object?>("queue.name", _options.QueueName),
                    new KeyValuePair<string, object?>("reason", reason));
            }
            else
            {
                _logger.LogError(
                    "Failed to move message to DLQ: MessageId={MessageId}, Reason={Reason}, Error={Error}",
                    message.MessageId,
                    reason,
                    result.ErrorMessage);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error moving message to DLQ: MessageId={MessageId}", message.MessageId);
        }
    }

    private bool IsRetryableError(string? error)
    {
        if (string.IsNullOrEmpty(error))
        {
            return true; // Default to retryable if no error message
        }

        // Non-retryable error patterns
        var nonRetryablePatterns = new[]
        {
            "timeout",
            "exceeded maximum duration",
            "deserialization",
            "invalid format",
            "bad request",
            "unauthorized",
            "forbidden",
            "not found",
            "conflict"
        };

        return !nonRetryablePatterns.Any(pattern =>
            error.Contains(pattern, StringComparison.OrdinalIgnoreCase));
    }

    private AgentSpec CreateAgentSpecFromMessage(ReceivedMessage message)
    {
        // Extract agent information from message properties
        // In production, this would fetch the agent definition from the Control Plane
        var agentId = message.Properties.TryGetValue("AgentId", out var aid)
            ? aid.ToString() ?? "default-agent"
            : "default-agent";

        var version = message.Properties.TryGetValue("Version", out var ver)
            ? ver.ToString() ?? "1.0"
            : "1.0";

        var name = message.Properties.TryGetValue("AgentName", out var aname)
            ? aname.ToString() ?? agentId
            : agentId;

        var instructions = message.Properties.TryGetValue("Instructions", out var instr)
            ? instr.ToString() ?? "Process the input message."
            : "Process the input message.";

        return new AgentSpec
        {
            AgentId = agentId,
            Version = version,
            Name = name,
            Instructions = instructions
        };
    }
}
