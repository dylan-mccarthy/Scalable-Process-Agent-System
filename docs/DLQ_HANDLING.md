# Dead-Letter Queue (DLQ) Handling

This document describes the Dead-Letter Queue (DLQ) handling implementation for the Business Process Agents platform, fulfilling **E2-T8: DLQ handling**.

## Overview

The DLQ handling system automatically routes failed messages to Azure Service Bus Dead-Letter Queue based on delivery count, error type, and failure patterns. This ensures that problematic messages don't block the processing queue while maintaining visibility into failures.

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                  Message Processing Flow                     │
└─────────────────────────────────────────────────────────────┘

Azure Service Bus Queue
    ↓
┌──────────────────────────┐
│ ServiceBusInputConnector │
└──────────────────────────┘
    ↓ Receive Message
    ↓
┌──────────────────────────┐
│ MessageProcessingService │
└──────────────────────────┘
    ↓
    ├─── Poison Check (DeliveryCount > Max)
    │    └─→ DeadLetter → DLQ
    │
    ├─── Agent Execution
    │    │
    │    ├─ Success → Complete → ✓ Removed from Queue
    │    │
    │    └─ Failure
    │         │
    │         ├─ Non-Retryable Error → DeadLetter → DLQ
    │         │
    │         └─ Retryable Error
    │              │
    │              ├─ DeliveryCount < Max → Abandon → ↻ Retry
    │              │
    │              └─ DeliveryCount >= Max → DeadLetter → DLQ
```

## Message Flow Scenarios

### 1. Successful Processing
```
Message → Receive → Execute → Success → Complete → ✓ Done
```

### 2. Poison Message (Exceeded Delivery Count)
```
Message (DeliveryCount > 3) → Receive → Check → DeadLetter → DLQ
```
**Note:** Agent execution is skipped for poison messages.

### 3. Non-Retryable Error
```
Message → Receive → Execute → Timeout/BadRequest → DeadLetter → DLQ
```

### 4. Retryable Error (First Attempt)
```
Message → Receive → Execute → Network Error → Abandon → ↻ Retry
```

### 5. Retryable Error (Max Retries)
```
Message (DeliveryCount = 3) → Receive → Execute → Error → DeadLetter → DLQ
```

## Components

### MessageProcessingService

The `MessageProcessingService` is responsible for:
- Receiving messages from the Service Bus input connector
- Checking for poison messages
- Executing agents with message content
- Classifying failures (retryable vs. non-retryable)
- Routing messages to appropriate destinations (Complete/Abandon/DLQ)

**Key Methods:**
- `StartAsync()` - Starts the message processing loop
- `StopAsync()` - Gracefully stops processing
- `ProcessSingleMessageAsync()` - Processes a single message with full DLQ handling
- `HandleProcessingFailureAsync()` - Routes failures based on error type and delivery count
- `IsRetryableError()` - Classifies errors as retryable or non-retryable

### ServiceBusInputConnector

The existing `ServiceBusInputConnector` provides the low-level operations:
- `ReceiveMessagesAsync()` - Receives messages from Service Bus
- `CompleteMessageAsync()` - Marks a message as successfully processed
- `AbandonMessageAsync()` - Returns a message to the queue for retry
- `DeadLetterMessageAsync()` - Moves a message to the DLQ

## Configuration

DLQ behavior is configured via `ServiceBusConnector` section in `appsettings.json`:

```json
{
  "ServiceBusConnector": {
    "ConnectionString": "Endpoint=sb://...;SharedAccessKeyName=...;SharedAccessKey=...",
    "QueueName": "invoices",
    "PrefetchCount": 16,
    "MaxConcurrentCalls": 5,
    "MaxWaitTime": "00:00:05",
    "MaxDeliveryCount": 3,
    "AutoComplete": false,
    "ReceiveMode": "PeekLock"
  }
}
```

**Key Configuration Options:**
- **MaxDeliveryCount**: Maximum delivery attempts before DLQ (default: 3, per SAD)
- **ReceiveMode**: Must be "PeekLock" for DLQ support (not "ReceiveAndDelete")
- **AutoComplete**: Should be `false` for manual message handling

## DLQ Routing Logic

### Poison Message Detection

A message is considered "poisoned" if its `DeliveryCount` exceeds `MaxDeliveryCount`:

```csharp
if (message.DeliveryCount > MaxDeliveryCount)
{
    await DeadLetterMessageAsync(
        message,
        "PoisonMessage",
        $"Message exceeded maximum delivery count of {MaxDeliveryCount}");
}
```

**Action:** Immediately moved to DLQ without agent execution.

### Error Classification

Errors are classified as **retryable** or **non-retryable** based on error message patterns:

**Non-Retryable Error Patterns:**
- `timeout` - Execution timeouts
- `exceeded maximum duration` - Budget enforcement
- `deserialization` - Message format errors
- `invalid format` - Schema validation failures
- `bad request` - HTTP 400 errors
- `unauthorized` - HTTP 401 errors
- `forbidden` - HTTP 403 errors
- `not found` - HTTP 404 errors
- `conflict` - HTTP 409 errors

**Retryable Errors:**
- Network errors
- Temporary service unavailability
- Transient database errors
- Rate limiting (HTTP 429)
- Internal server errors (HTTP 500)

### Routing Decision Matrix

| Scenario | DeliveryCount | Error Type | Action | Reason |
|----------|---------------|------------|--------|---------|
| Success | Any | N/A | Complete | Processed successfully |
| Poison | > Max | N/A | DeadLetter | PoisonMessage |
| Failure | Any | Non-Retryable | DeadLetter | NonRetryableError |
| Failure | < Max | Retryable | Abandon | Retry attempt |
| Failure | >= Max | Retryable | DeadLetter | MaxDeliveryCountExceeded |

## Observability

### Metrics

The DLQ handling includes comprehensive metrics for monitoring:

**Counter:**
```csharp
messages_deadlettered_total
    Tags: queue.name, reason
```

**Reasons tracked:**
- `PoisonMessage` - Exceeded max delivery count
- `NonRetryableError` - Non-retryable failure
- `MaxDeliveryCountExceeded` - Retryable error with max retries

### Distributed Tracing

DLQ operations are fully instrumented with OpenTelemetry:

**Activities:**
- `MessageProcessingService.ProcessMessages` - Overall processing loop
- `MessageProcessingService.ProcessSingleMessage` - Individual message processing
- `ServiceBusConnector.DeadLetterMessage` - DLQ operation (from connector)

**Tags:**
- `message.id` - Unique message identifier
- `delivery.count` - Current delivery attempt
- `queue.name` - Source queue name
- `dlq.reason` - Reason for dead-lettering
- `dlq.error_description` - Detailed error description

### Logging

Structured logs are emitted for all DLQ operations:

```
LogWarning: "Poison message detected - moving to DLQ: MessageId={MessageId}, DeliveryCount={DeliveryCount}"
LogWarning: "Non-retryable error for message: MessageId={MessageId}, Error={Error}"
LogWarning: "Message exceeded max delivery count: MessageId={MessageId}, DeliveryCount={DeliveryCount}"
LogInformation: "Abandoning message for retry: MessageId={MessageId}, DeliveryCount={DeliveryCount}"
```

## Usage Example

### Basic Setup

```csharp
// Register services
services.Configure<ServiceBusConnectorOptions>(
    configuration.GetSection("ServiceBusConnector"));
services.AddSingleton<IInputConnector, ServiceBusInputConnector>();
services.AddSingleton<IAgentExecutor, AgentExecutorService>();
services.AddSingleton<IMessageProcessingService, MessageProcessingService>();

// Start processing
var processingService = serviceProvider.GetRequiredService<IMessageProcessingService>();
await processingService.StartAsync(cancellationToken);
```

### Manual DLQ Operation

For custom scenarios, you can manually dead-letter messages:

```csharp
var connector = serviceProvider.GetRequiredService<IInputConnector>();

// Dead-letter with custom reason
await connector.DeadLetterMessageAsync(
    message,
    "CustomBusinessRule",
    "Message violates business policy XYZ",
    cancellationToken);
```

## Monitoring DLQ

### Query DLQ Messages

Use Azure Service Bus Explorer or SDK to query DLQ:

```csharp
var receiver = serviceBusClient.CreateReceiver(
    queueName,
    new ServiceBusReceiverOptions 
    { 
        SubQueue = SubQueue.DeadLetter 
    });

var dlqMessages = await receiver.ReceiveMessagesAsync(maxMessages: 100);

foreach (var message in dlqMessages)
{
    Console.WriteLine($"MessageId: {message.MessageId}");
    Console.WriteLine($"DeadLetterReason: {message.DeadLetterReason}");
    Console.WriteLine($"DeadLetterErrorDescription: {message.DeadLetterErrorDescription}");
}
```

### DLQ Metrics Dashboard

Monitor DLQ health with Prometheus/Grafana queries:

```promql
# Total messages dead-lettered
sum(messages_deadlettered_total)

# Dead-letter rate by reason
rate(messages_deadlettered_total[5m])

# Dead-letter ratio
rate(messages_deadlettered_total[5m]) / rate(messages_received_total[5m])
```

## Best Practices

### 1. Set Appropriate MaxDeliveryCount

```json
{
  "ServiceBusConnector": {
    "MaxDeliveryCount": 3  // Balance between retries and DLQ speed
  }
}
```

**Recommendations:**
- **3 retries** (default) - Standard for most scenarios
- **1 retry** - Critical real-time processing
- **5 retries** - Highly tolerant to transient failures

### 2. Monitor DLQ Growth

Set up alerts for DLQ message count:
- **Warning:** > 100 messages in DLQ
- **Critical:** > 1000 messages or > 10% of processed messages

### 3. Implement DLQ Replay

Create a separate process to review and replay DLQ messages:

```csharp
// Review DLQ message
var dlqReceiver = client.CreateReceiver(queueName, 
    new ServiceBusReceiverOptions { SubQueue = SubQueue.DeadLetter });
var message = await dlqReceiver.ReceiveMessageAsync();

// Replay to main queue after fixing
var sender = client.CreateSender(queueName);
await sender.SendMessageAsync(new ServiceBusMessage(message.Body)
{
    ApplicationProperties = { /* Fixed properties */ }
});
await dlqReceiver.CompleteMessageAsync(message);
```

### 4. Add Custom Error Patterns

Extend the non-retryable error patterns for your domain:

```csharp
private bool IsRetryableError(string? error)
{
    var nonRetryablePatterns = new[]
    {
        // Standard patterns
        "timeout", "exceeded maximum duration",
        // Custom patterns
        "duplicate invoice", "invalid customer id"
    };
    
    return !nonRetryablePatterns.Any(pattern =>
        error.Contains(pattern, StringComparison.OrdinalIgnoreCase));
}
```

## Testing

### Unit Tests

The implementation includes 21 unit tests covering all DLQ scenarios:
- Poison message detection
- Non-retryable error handling
- Retryable error with abandonment
- Max delivery count enforcement
- Error pattern matching

Run tests:
```bash
dotnet test --filter "FullyQualifiedName~MessageProcessingServiceTests"
```

### Integration Tests

5 integration tests validate end-to-end DLQ flow:
- Poison message → DLQ
- Successful processing → Complete
- Retryable error with max retries → DLQ
- Non-retryable error → DLQ
- Multiple messages with different outcomes

Run integration tests:
```bash
dotnet test --filter "FullyQualifiedName~DLQHandlingIntegrationTests"
```

## Troubleshooting

### Messages Not Being Dead-Lettered

**Symptoms:** Messages keep retrying indefinitely

**Possible Causes:**
1. `AutoComplete` is enabled
2. `ReceiveMode` is set to "ReceiveAndDelete"
3. Exception thrown before DLQ logic runs

**Solution:**
```json
{
  "ServiceBusConnector": {
    "AutoComplete": false,
    "ReceiveMode": "PeekLock"
  }
}
```

### Too Many Messages in DLQ

**Symptoms:** DLQ count increasing rapidly

**Possible Causes:**
1. Agent configuration error affecting all messages
2. External dependency unavailable
3. MaxDeliveryCount set too low

**Solution:**
1. Check agent execution logs for common errors
2. Review non-retryable error patterns
3. Adjust MaxDeliveryCount if needed

### Messages Dead-Lettered Prematurely

**Symptoms:** Retryable errors going straight to DLQ

**Possible Causes:**
1. Error pattern incorrectly classified as non-retryable
2. MaxDeliveryCount set too low

**Solution:**
1. Review `IsRetryableError()` logic
2. Add custom error patterns
3. Increase MaxDeliveryCount

## Security Considerations

### DLQ Access Control

Limit access to DLQ operations:
- **Service Principal:** Grant `Azure Service Bus Data Receiver` role for DLQ read
- **Operators:** Grant `Azure Service Bus Data Owner` for DLQ management
- **Audit:** Enable diagnostic logs for DLQ operations

### Sensitive Data

Messages in DLQ may contain sensitive data:
- Enable encryption at rest for Service Bus
- Set appropriate message TTL
- Implement automated DLQ cleanup policies

## References

- [System Architecture Document (SAD)](../sad.md)
- [Service Bus Input Connector](../src/Node.Runtime/README.md#connectors-e2-t6)
- [OBSERVABILITY.md](../OBSERVABILITY.md)
- [Azure Service Bus DLQ Documentation](https://learn.microsoft.com/en-us/azure/service-bus-messaging/service-bus-dead-letter-queues)

## Related Tasks

- **E2-T6:** Service Bus connector (✅ Complete)
- **E2-T7:** HTTP output connector (⏳ Pending)
- **E2-T8:** DLQ handling (✅ Complete - this document)
- **E2-T9:** Node telemetry (Partial - DLQ metrics added)
- **E3-T6:** Invoice Classifier agent (Uses DLQ handling)
