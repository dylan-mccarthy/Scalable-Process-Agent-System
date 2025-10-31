# Security Remediation - Code Examples

This document provides specific before/after examples for fixing the CodeQL alerts in our codebase.

## 1. Fixing Generic Catch Blocks in MessageProcessingService.cs

### Current Code (4 alerts)

The file has 4 generic catch blocks that need to be made more specific:

#### Example 1: ProcessMessagesAsync - Line ~130

**Before:**
```csharp
catch (OperationCanceledException)
{
    _logger.LogInformation("Message processing cancelled");
    break;
}
catch (Exception ex)  // ❌ Too generic
{
    _logger.LogError(ex, "Error in message processing loop");
    await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
}
```

**After:**
```csharp
catch (OperationCanceledException)
{
    _logger.LogInformation("Message processing cancelled");
    break;
}
catch (Azure.Messaging.ServiceBus.ServiceBusException sbEx) when (sbEx.IsTransient)
{
    _logger.LogWarning(sbEx, "Transient Service Bus error in message processing loop - will retry after delay");
    await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
}
catch (Azure.Messaging.ServiceBus.ServiceBusException sbEx)
{
    _logger.LogError(sbEx, "Non-transient Service Bus error: {Reason}", sbEx.Reason);
    // For non-transient errors, wait longer before retry
    await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
}
catch (Exception ex)
{
    // Only catch unexpected exceptions at the top-level loop
    _logger.LogCritical(ex, "Unexpected fatal error in message processing loop - this should not happen");
    throw; // Rethrow to allow application to crash and restart
}
```

#### Example 2: ProcessSingleMessageAsync - Lines ~181, ~206, ~212

**Before:**
```csharp
catch (OperationCanceledException)
{
    _logger.LogInformation("Message processing cancelled for MessageId={MessageId}", message.MessageId);
    await _inputConnector.AbandonMessageAsync(message, cancellationToken);
}
catch (Exception ex)  // ❌ Too generic
{
    stopwatch.Stop();
    _logger.LogError(ex, "Unexpected error processing message: MessageId={MessageId}", message.MessageId);

    try
    {
        await _inputConnector.AbandonMessageAsync(message, cancellationToken);
    }
    catch (Exception abandonEx)  // ❌ Too generic
    {
        _logger.LogError(abandonEx, "Failed to abandon message after error: MessageId={MessageId}", message.MessageId);
    }

    activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
}
```

**After:**
```csharp
catch (OperationCanceledException)
{
    _logger.LogInformation("Message processing cancelled for MessageId={MessageId}", message.MessageId);
    await _inputConnector.AbandonMessageAsync(message, cancellationToken);
}
catch (JsonException jsonEx)
{
    // Deserialization errors are non-retryable
    stopwatch.Stop();
    _logger.LogError(jsonEx, "Message deserialization failed for MessageId={MessageId}", message.MessageId);
    
    await DeadLetterMessageAsync(
        message,
        "DeserializationError",
        $"Failed to deserialize message: {jsonEx.Message}",
        CancellationToken.None); // Use None to ensure DLQ on cancellation
    
    activity?.SetStatus(ActivityStatusCode.Error, "Deserialization failed");
}
catch (InvalidOperationException invalidEx) when (invalidEx.Message.Contains("agent", StringComparison.OrdinalIgnoreCase))
{
    // Agent configuration errors
    stopwatch.Stop();
    _logger.LogError(invalidEx, "Agent configuration error for MessageId={MessageId}", message.MessageId);
    
    await DeadLetterMessageAsync(
        message,
        "AgentConfigurationError",
        $"Invalid agent configuration: {invalidEx.Message}",
        CancellationToken.None);
    
    activity?.SetStatus(ActivityStatusCode.Error, "Agent configuration error");
}
catch (TimeoutException timeoutEx)
{
    // Timeout during processing - abandon for retry
    stopwatch.Stop();
    _logger.LogWarning(timeoutEx, "Timeout processing message MessageId={MessageId}", message.MessageId);
    
    await SafeAbandonAsync(message, "Processing timeout", CancellationToken.None);
    activity?.SetStatus(ActivityStatusCode.Error, "Timeout");
}
catch (Azure.Messaging.ServiceBus.ServiceBusException sbEx)
{
    // Service Bus specific errors
    stopwatch.Stop();
    _logger.LogError(sbEx, "Service Bus error processing MessageId={MessageId}, Reason={Reason}", 
        message.MessageId, sbEx.Reason);
    
    await SafeAbandonAsync(message, $"ServiceBus error: {sbEx.Reason}", CancellationToken.None);
    activity?.SetStatus(ActivityStatusCode.Error, sbEx.Reason.ToString());
}
catch (Exception ex)
{
    // Truly unexpected exceptions
    stopwatch.Stop();
    _logger.LogCritical(ex, "UNEXPECTED error processing message MessageId={MessageId}, Type={ExceptionType}", 
        message.MessageId, ex.GetType().FullName);

    await SafeAbandonAsync(message, $"Unexpected error: {ex.GetType().Name}", CancellationToken.None);

    activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
    activity?.AddEvent(new ActivityEvent("unexpected_exception",
        tags: new ActivityTagsCollection
        {
            { "exception.type", ex.GetType().FullName },
            { "exception.message", ex.Message },
            { "exception.stacktrace", ex.StackTrace }
        }));
}

// Helper method to add to the class
private async Task SafeAbandonAsync(ReceivedMessage message, string reason, CancellationToken cancellationToken)
{
    try
    {
        await _inputConnector.AbandonMessageAsync(message, cancellationToken);
        _logger.LogInformation("Abandoned message MessageId={MessageId}, Reason={Reason}", message.MessageId, reason);
    }
    catch (Azure.Messaging.ServiceBus.ServiceBusException sbEx)
    {
        _logger.LogError(sbEx, "ServiceBus error abandoning MessageId={MessageId}, Reason={SBReason}", 
            message.MessageId, sbEx.Reason);
        // Message may have already been picked up by another receiver or moved to DLQ
        // Log but don't throw - we've already handled the original message
    }
    catch (InvalidOperationException invalidEx)
    {
        _logger.LogWarning(invalidEx, "Cannot abandon MessageId={MessageId} - likely already completed/abandoned", 
            message.MessageId);
        // This is expected in some race conditions
    }
}
```

#### Example 3: DeadLetterMessageAsync - Line ~282

**Before:**
```csharp
catch (Exception ex)  // ❌ Too generic
{
    _logger.LogError(ex, "Error moving message to DLQ: MessageId={MessageId}", message.MessageId);
}
```

**After:**
```csharp
catch (Azure.Messaging.ServiceBus.ServiceBusException sbEx) when (sbEx.Reason == ServiceBusFailureReason.MessageLockLost)
{
    _logger.LogWarning(sbEx, "Lock lost while moving MessageId={MessageId} to DLQ - message may have been auto-completed", 
        message.MessageId);
    // This can happen if processing took too long - message will be retried automatically
}
catch (Azure.Messaging.ServiceBus.ServiceBusException sbEx)
{
    _logger.LogError(sbEx, "Service Bus error moving MessageId={MessageId} to DLQ: {Reason}", 
        message.MessageId, sbEx.Reason);
    throw; // Rethrow as this is a critical error
}
catch (InvalidOperationException invalidEx)
{
    _logger.LogError(invalidEx, "Invalid operation moving MessageId={MessageId} to DLQ - message state issue", 
        message.MessageId);
    // Message may already be in DLQ or completed elsewhere
}
```

**Required using statements to add:**
```csharp
using Azure.Messaging.ServiceBus;
using System.Text.Json;
```

---

## 2. Fixing Resource Disposal Issues

### Example: Program.cs - Missing Disposal

**Before:**
```csharp
var certificateValidator = new CertificateValidator(
    logger: loggerFactory.CreateLogger<CertificateValidator>());

// certificateValidator is IDisposable but never disposed ❌
```

**After:**
```csharp
using var certificateValidator = new CertificateValidator(
    logger: loggerFactory.CreateLogger<CertificateValidator>());

// Will be disposed automatically when going out of scope ✅
```

### Example: Test File - Missing Disposal in Tests

**Before:**
```csharp
[Fact]
public async Task ProcessMessage_ShouldCompleteSuccessfully()
{
    var testMessage = CreateTestMessage(); // Returns IDisposable ❌
    
    await _service.ProcessAsync(testMessage);
    
    Assert.True(testMessage.IsCompleted);
}
```

**After - Option 1: Using statement**
```csharp
[Fact]
public async Task ProcessMessage_ShouldCompleteSuccessfully()
{
    using var testMessage = CreateTestMessage(); // ✅
    
    await _service.ProcessAsync(testMessage);
    
    Assert.True(testMessage.IsCompleted);
}
```

**After - Option 2: IAsyncLifetime for complex setup**
```csharp
public class MessageProcessingServiceTests : IAsyncLifetime
{
    private ServiceBusClient? _client;
    private ServiceBusReceiver? _receiver;
    
    public async Task InitializeAsync()
    {
        _client = new ServiceBusClient(connectionString);
        _receiver = _client.CreateReceiver(queueName);
    }
    
    public async Task DisposeAsync()
    {
        if (_receiver != null)
            await _receiver.DisposeAsync();
        if (_client != null)
            await _client.DisposeAsync();
    }
    
    [Fact]
    public async Task ProcessMessage_ShouldCompleteSuccessfully()
    {
        // Use _receiver that will be properly disposed
        using var testMessage = await _receiver!.ReceiveMessageAsync();
        
        await _service.ProcessAsync(testMessage);
        
        Assert.True(testMessage != null);
    }
}
```

---

## 3. Fixing Path.Combine Issues in MTlsIntegrationTests.cs

### Pattern to Find and Replace

**Before:**
```csharp
var certPath = _certDirectory + "/ca/ca-cert.pem";
var keyPath = _certDirectory + "/client/client-key.pem";
var pfxPath = Path.GetTempPath() + "client-cert.pfx";
```

**After:**
```csharp
var certPath = Path.Combine(_certDirectory, "ca", "ca-cert.pem");
var keyPath = Path.Combine(_certDirectory, "client", "client-key.pem");
var pfxPath = Path.Combine(Path.GetTempPath(), "client-cert.pfx");
```

### Bulk Replacement Strategy

Since all 15 instances are in one file, you can:

1. Create helper method:
```csharp
private string GetCertPath(params string[] parts)
{
    return Path.Combine(_certDirectory, Path.Combine(parts));
}
```

2. Use it:
```csharp
var certPath = GetCertPath("ca", "ca-cert.pem");
var keyPath = GetCertPath("client", "client-key.pem");
```

---

## 4. Fixing Nested If Statements

### Example: Program.cs

**Before:**
```csharp
if (options.MtlsEnabled)
{
    if (!string.IsNullOrEmpty(options.ClientCertPath))
    {
        LoadClientCertificate(options.ClientCertPath);
    }
}
```

**After - Option 1: Combined condition**
```csharp
if (options.MtlsEnabled && !string.IsNullOrEmpty(options.ClientCertPath))
{
    LoadClientCertificate(options.ClientCertPath);
}
```

**After - Option 2: Guard clause (preferred when this is early validation)**
```csharp
if (!options.MtlsEnabled)
    return;
    
if (string.IsNullOrEmpty(options.ClientCertPath))
{
    _logger.LogWarning("mTLS enabled but no client certificate path provided");
    return;
}

LoadClientCertificate(options.ClientCertPath);
```

---

## 5. Fixing LINQ Optimization

### Example: Missed Where

**Before:**
```csharp
var activeNodes = new List<Node>();
foreach (var node in allNodes)
{
    if (node.Status == NodeStatus.Active)
    {
        activeNodes.Add(node);
    }
}
```

**After:**
```csharp
var activeNodes = allNodes
    .Where(node => node.Status == NodeStatus.Active)
    .ToList();
```

### Example: Missed Select

**Before:**
```csharp
var nodeIds = new List<string>();
foreach (var node in nodes)
{
    nodeIds.Add(node.Id);
}
```

**After:**
```csharp
var nodeIds = nodes
    .Select(node => node.Id)
    .ToList();
```

---

## Testing Checklist After Changes

### Unit Tests
- [ ] All existing tests pass
- [ ] Test specific exception scenarios
- [ ] Verify no resource leaks (use memory profiler if needed)

### Integration Tests
- [ ] E2E tests pass
- [ ] Test DLQ scenarios explicitly
- [ ] Test timeout scenarios
- [ ] Test cancellation scenarios

### Static Analysis
- [ ] Run CodeQL locally (if possible)
- [ ] Run `dotnet format --verify-no-changes`
- [ ] Check for warnings in build output

### Code Review
- [ ] Each catch block has appropriate exception type
- [ ] All IDisposable objects have disposal path
- [ ] Error logging includes sufficient context
- [ ] Activity/trace IDs propagated correctly

---

## Commit Strategy

Since these are related but independent changes, consider separate commits:

```bash
# Commit 1: Resource disposal
git add src/Node.Runtime/Program.cs tests/**/*Tests.cs
git commit -m "fix(E2-T10): ensure proper disposal of IDisposable resources

- Add using statements for disposable objects in Node.Runtime
- Implement IAsyncLifetime in test fixtures
- Prevents resource leaks in long-running services

Fixes CodeQL alerts: cs/local-not-disposed (3 instances)"

# Commit 2: Exception handling
git add src/Node.Runtime/Services/MessageProcessingService.cs src/Node.Runtime/Program.cs
git commit -m "fix(E2-T10): replace generic catch blocks with specific exception handling

- Handle ServiceBusException with transient/non-transient logic
- Handle JsonException for deserialization errors
- Handle TimeoutException for processing timeouts
- Improve error observability and retry logic

Fixes CodeQL alerts: cs/catch-of-all-exceptions (8 instances)"

# Commit 3: Code quality
git add tests/ControlPlane.Api.Tests/MTlsIntegrationTests.cs
git commit -m "refactor(E2-T10): replace string concatenation with Path.Combine

- Ensures cross-platform path compatibility in tests
- Adds helper method for certificate path construction

Fixes CodeQL alerts: cs/path-combine (15 instances)"

# Commit 4: LINQ and nested ifs
git add src/Node.Runtime/Program.cs tests/E2E.Tests/ChaosTests.cs
git commit -m "refactor(E2-T10): improve code clarity with LINQ and simplified conditionals

- Replace foreach+filter patterns with LINQ Where/Select
- Simplify nested if statements with guard clauses
- Improves readability and maintainability

Fixes CodeQL alerts: cs/nested-if-statements (2), cs/linq/missed-* (2)"
```

---

## Useful Commands

### Run local static analysis
```powershell
# .NET Format
dotnet format --verify-no-changes --verbosity diagnostic

# Build with warnings as errors
dotnet build /p:TreatWarningsAsErrors=true

# Run tests with coverage
dotnet test --collect:"XPlat Code Coverage"
```

### Check for CodeQL alerts locally (if CodeQL CLI installed)
```powershell
# Create database
codeql database create codeql-db --language=csharp --command="dotnet build"

# Run analysis
codeql database analyze codeql-db csharp-security-and-quality.qls --format=sarif-latest --output=results.sarif

# View results
codeql database interpret-results codeql-db --format=csv
```

