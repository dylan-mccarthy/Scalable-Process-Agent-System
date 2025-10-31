using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Node.Runtime.Configuration;
using Node.Runtime.Connectors;
using Node.Runtime.Services;

namespace Node.Runtime.Tests.Integration;

/// <summary>
/// Integration tests for end-to-end DLQ handling flow.
/// These tests validate the complete message processing pipeline with DLQ routing.
/// </summary>
public sealed class DLQHandlingIntegrationTests
{
    private readonly Mock<ILogger<MessageProcessingService>> _loggerMock;
    private readonly Mock<ILogger<ServiceBusInputConnector>> _connectorLoggerMock;
    private readonly ServiceBusConnectorOptions _options;

    public DLQHandlingIntegrationTests()
    {
        _loggerMock = new Mock<ILogger<MessageProcessingService>>();
        _connectorLoggerMock = new Mock<ILogger<ServiceBusInputConnector>>();
        _options = new ServiceBusConnectorOptions
        {
            QueueName = "integration-test-queue",
            MaxConcurrentCalls = 1,
            MaxWaitTime = TimeSpan.FromMilliseconds(100),
            MaxDeliveryCount = 3
        };
    }

    [Fact]
    public async Task EndToEnd_PoisonMessage_ShouldDeadLetterImmediately()
    {
        // Arrange
        var connectorMock = new Mock<IInputConnector>();
        var executorMock = new Mock<IAgentExecutor>();

        var poisonMessage = new ReceivedMessage
        {
            MessageId = "poison-msg-1",
            Body = "Poison message content",
            DeliveryCount = 4, // Exceeds MaxDeliveryCount of 3
            EnqueuedTime = DateTimeOffset.UtcNow,
            Properties = new Dictionary<string, object>
            {
                { "AgentId", "test-agent" },
                { "Version", "1.0" }
            }
        };

        var messageQueue = new Queue<List<ReceivedMessage>>();
        messageQueue.Enqueue(new List<ReceivedMessage> { poisonMessage });
        messageQueue.Enqueue(new List<ReceivedMessage>()); // Empty list to stop processing

        connectorMock.Setup(x => x.ReceiveMessagesAsync(
            It.IsAny<int>(),
            It.IsAny<TimeSpan?>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => messageQueue.Count > 0 ? messageQueue.Dequeue() : new List<ReceivedMessage>());

        var deadLetteredMessages = new List<ReceivedMessage>();
        connectorMock.Setup(x => x.DeadLetterMessageAsync(
            It.IsAny<ReceivedMessage>(),
            It.IsAny<string>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()))
            .Callback<ReceivedMessage, string, string?, CancellationToken>(
                (msg, reason, desc, ct) => deadLetteredMessages.Add(msg))
            .ReturnsAsync(new MessageCompletionResult { Success = true });

        var service = new MessageProcessingService(
            connectorMock.Object,
            executorMock.Object,
            Options.Create(_options),
            _loggerMock.Object);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));

        // Act
        await service.StartAsync(cts.Token);
        await Task.Delay(300);
        await service.StopAsync();

        // Assert
        deadLetteredMessages.Should().ContainSingle();
        deadLetteredMessages[0].MessageId.Should().Be("poison-msg-1");

        connectorMock.Verify(
            x => x.DeadLetterMessageAsync(
                poisonMessage,
                "PoisonMessage",
                It.Is<string>(s => s.Contains("exceeded maximum delivery count")),
                It.IsAny<CancellationToken>()),
            Times.Once,
            "Poison message should be dead-lettered immediately");

        executorMock.Verify(
            x => x.ExecuteAsync(It.IsAny<AgentSpec>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "Agent should not execute for poison messages");
    }

    [Fact]
    public async Task EndToEnd_SuccessfulProcessing_ShouldCompleteMessage()
    {
        // Arrange
        var connectorMock = new Mock<IInputConnector>();
        var executorMock = new Mock<IAgentExecutor>();

        var validMessage = new ReceivedMessage
        {
            MessageId = "valid-msg-1",
            Body = "Valid message content",
            DeliveryCount = 1,
            EnqueuedTime = DateTimeOffset.UtcNow,
            Properties = new Dictionary<string, object>
            {
                { "AgentId", "test-agent" },
                { "Version", "1.0" }
            }
        };

        var messageQueue = new Queue<List<ReceivedMessage>>();
        messageQueue.Enqueue(new List<ReceivedMessage> { validMessage });
        messageQueue.Enqueue(new List<ReceivedMessage>());

        connectorMock.Setup(x => x.ReceiveMessagesAsync(
            It.IsAny<int>(),
            It.IsAny<TimeSpan?>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => messageQueue.Count > 0 ? messageQueue.Dequeue() : new List<ReceivedMessage>());

        executorMock.Setup(x => x.ExecuteAsync(
            It.IsAny<AgentSpec>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentExecutionResult
            {
                Success = true,
                Output = "Processed successfully",
                TokensIn = 10,
                TokensOut = 20,
                UsdCost = 0.001
            });

        var completedMessages = new List<ReceivedMessage>();
        connectorMock.Setup(x => x.CompleteMessageAsync(
            It.IsAny<ReceivedMessage>(),
            It.IsAny<CancellationToken>()))
            .Callback<ReceivedMessage, CancellationToken>((msg, ct) => completedMessages.Add(msg))
            .ReturnsAsync(new MessageCompletionResult { Success = true });

        var service = new MessageProcessingService(
            connectorMock.Object,
            executorMock.Object,
            Options.Create(_options),
            _loggerMock.Object);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));

        // Act
        await service.StartAsync(cts.Token);
        await Task.Delay(300);
        await service.StopAsync();

        // Assert
        completedMessages.Should().ContainSingle();
        completedMessages[0].MessageId.Should().Be("valid-msg-1");

        executorMock.Verify(
            x => x.ExecuteAsync(
                It.Is<AgentSpec>(spec => spec.AgentId == "test-agent"),
                "Valid message content",
                It.IsAny<CancellationToken>()),
            Times.Once,
            "Agent should execute with message content");

        connectorMock.Verify(
            x => x.CompleteMessageAsync(validMessage, It.IsAny<CancellationToken>()),
            Times.Once,
            "Message should be completed after successful processing");

        connectorMock.Verify(
            x => x.DeadLetterMessageAsync(
                It.IsAny<ReceivedMessage>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.Never,
            "Successful messages should not be dead-lettered");
    }

    [Fact]
    public async Task EndToEnd_RetryableErrorWithRetries_ShouldEventuallyDeadLetter()
    {
        // Arrange
        var connectorMock = new Mock<IInputConnector>();
        var executorMock = new Mock<IAgentExecutor>();

        // Simulate message being delivered 3 times (at max delivery count)
        var failingMessage = new ReceivedMessage
        {
            MessageId = "failing-msg-1",
            Body = "Message that fails after retries",
            DeliveryCount = 3, // At max delivery count
            EnqueuedTime = DateTimeOffset.UtcNow,
            Properties = new Dictionary<string, object>
            {
                { "AgentId", "test-agent" },
                { "Version", "1.0" }
            }
        };

        var messageQueue = new Queue<List<ReceivedMessage>>();
        messageQueue.Enqueue(new List<ReceivedMessage> { failingMessage });
        messageQueue.Enqueue(new List<ReceivedMessage>());

        connectorMock.Setup(x => x.ReceiveMessagesAsync(
            It.IsAny<int>(),
            It.IsAny<TimeSpan?>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => messageQueue.Count > 0 ? messageQueue.Dequeue() : new List<ReceivedMessage>());

        // Agent execution fails with a retryable error
        executorMock.Setup(x => x.ExecuteAsync(
            It.IsAny<AgentSpec>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentExecutionResult
            {
                Success = false,
                Error = "Temporary network error - please retry"
            });

        var deadLetteredMessages = new List<(ReceivedMessage Message, string Reason, string? Description)>();
        connectorMock.Setup(x => x.DeadLetterMessageAsync(
            It.IsAny<ReceivedMessage>(),
            It.IsAny<string>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()))
            .Callback<ReceivedMessage, string, string?, CancellationToken>(
                (msg, reason, desc, ct) => deadLetteredMessages.Add((msg, reason, desc)))
            .ReturnsAsync(new MessageCompletionResult { Success = true });

        var service = new MessageProcessingService(
            connectorMock.Object,
            executorMock.Object,
            Options.Create(_options),
            _loggerMock.Object);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));

        // Act
        await service.StartAsync(cts.Token);
        await Task.Delay(300);
        await service.StopAsync();

        // Assert
        deadLetteredMessages.Should().ContainSingle();
        deadLetteredMessages[0].Message.MessageId.Should().Be("failing-msg-1");
        deadLetteredMessages[0].Reason.Should().Be("MaxDeliveryCountExceeded");
        deadLetteredMessages[0].Description.Should().Contain("exceeded 3 delivery attempts");

        connectorMock.Verify(
            x => x.AbandonMessageAsync(It.IsAny<ReceivedMessage>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "Should not abandon when at max delivery count");
    }

    [Fact]
    public async Task EndToEnd_NonRetryableError_ShouldDeadLetterImmediately()
    {
        // Arrange
        var connectorMock = new Mock<IInputConnector>();
        var executorMock = new Mock<IAgentExecutor>();

        var message = new ReceivedMessage
        {
            MessageId = "bad-msg-1",
            Body = "Message with non-retryable error",
            DeliveryCount = 1, // First attempt
            EnqueuedTime = DateTimeOffset.UtcNow,
            Properties = new Dictionary<string, object>
            {
                { "AgentId", "test-agent" },
                { "Version", "1.0" }
            }
        };

        var messageQueue = new Queue<List<ReceivedMessage>>();
        messageQueue.Enqueue(new List<ReceivedMessage> { message });
        messageQueue.Enqueue(new List<ReceivedMessage>());

        connectorMock.Setup(x => x.ReceiveMessagesAsync(
            It.IsAny<int>(),
            It.IsAny<TimeSpan?>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => messageQueue.Count > 0 ? messageQueue.Dequeue() : new List<ReceivedMessage>());

        // Agent execution fails with a non-retryable error
        executorMock.Setup(x => x.ExecuteAsync(
            It.IsAny<AgentSpec>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentExecutionResult
            {
                Success = false,
                Error = "Agent execution exceeded maximum duration of 60 seconds"
            });

        var deadLetteredMessages = new List<(ReceivedMessage Message, string Reason)>();
        connectorMock.Setup(x => x.DeadLetterMessageAsync(
            It.IsAny<ReceivedMessage>(),
            It.IsAny<string>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()))
            .Callback<ReceivedMessage, string, string?, CancellationToken>(
                (msg, reason, desc, ct) => deadLetteredMessages.Add((msg, reason)))
            .ReturnsAsync(new MessageCompletionResult { Success = true });

        var service = new MessageProcessingService(
            connectorMock.Object,
            executorMock.Object,
            Options.Create(_options),
            _loggerMock.Object);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));

        // Act
        await service.StartAsync(cts.Token);
        await Task.Delay(300);
        await service.StopAsync();

        // Assert
        deadLetteredMessages.Should().ContainSingle();
        deadLetteredMessages[0].Message.MessageId.Should().Be("bad-msg-1");
        deadLetteredMessages[0].Reason.Should().Be("NonRetryableError");

        connectorMock.Verify(
            x => x.AbandonMessageAsync(It.IsAny<ReceivedMessage>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "Non-retryable errors should not be abandoned");

        connectorMock.Verify(
            x => x.CompleteMessageAsync(It.IsAny<ReceivedMessage>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "Failed messages should not be completed");
    }

    [Fact]
    public async Task EndToEnd_MultipleMessages_ShouldHandleEachAppropriately()
    {
        // Arrange
        var connectorMock = new Mock<IInputConnector>();
        var executorMock = new Mock<IAgentExecutor>();

        var successMessage = new ReceivedMessage
        {
            MessageId = "success-msg",
            Body = "Success",
            DeliveryCount = 1,
            EnqueuedTime = DateTimeOffset.UtcNow,
            Properties = new Dictionary<string, object> { { "AgentId", "test-agent" } }
        };

        var poisonMessage = new ReceivedMessage
        {
            MessageId = "poison-msg",
            Body = "Poison",
            DeliveryCount = 4,
            EnqueuedTime = DateTimeOffset.UtcNow,
            Properties = new Dictionary<string, object> { { "AgentId", "test-agent" } }
        };

        var retryMessage = new ReceivedMessage
        {
            MessageId = "retry-msg",
            Body = "Retry",
            DeliveryCount = 1,
            EnqueuedTime = DateTimeOffset.UtcNow,
            Properties = new Dictionary<string, object> { { "AgentId", "test-agent" } }
        };

        var messageQueue = new Queue<List<ReceivedMessage>>();
        messageQueue.Enqueue(new List<ReceivedMessage> { successMessage, poisonMessage, retryMessage });
        messageQueue.Enqueue(new List<ReceivedMessage>());

        connectorMock.Setup(x => x.ReceiveMessagesAsync(
            It.IsAny<int>(),
            It.IsAny<TimeSpan?>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => messageQueue.Count > 0 ? messageQueue.Dequeue() : new List<ReceivedMessage>());

        executorMock.Setup(x => x.ExecuteAsync(
            It.IsAny<AgentSpec>(),
            "Success",
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentExecutionResult { Success = true, Output = "OK" });

        executorMock.Setup(x => x.ExecuteAsync(
            It.IsAny<AgentSpec>(),
            "Retry",
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentExecutionResult { Success = false, Error = "Temporary error" });

        var completedMessages = new List<string>();
        var abandonedMessages = new List<string>();
        var deadLetteredMessages = new List<string>();

        connectorMock.Setup(x => x.CompleteMessageAsync(
            It.IsAny<ReceivedMessage>(),
            It.IsAny<CancellationToken>()))
            .Callback<ReceivedMessage, CancellationToken>((msg, ct) => completedMessages.Add(msg.MessageId))
            .ReturnsAsync(new MessageCompletionResult { Success = true });

        connectorMock.Setup(x => x.AbandonMessageAsync(
            It.IsAny<ReceivedMessage>(),
            It.IsAny<CancellationToken>()))
            .Callback<ReceivedMessage, CancellationToken>((msg, ct) => abandonedMessages.Add(msg.MessageId))
            .ReturnsAsync(new MessageCompletionResult { Success = true });

        connectorMock.Setup(x => x.DeadLetterMessageAsync(
            It.IsAny<ReceivedMessage>(),
            It.IsAny<string>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()))
            .Callback<ReceivedMessage, string, string?, CancellationToken>(
                (msg, reason, desc, ct) => deadLetteredMessages.Add(msg.MessageId))
            .ReturnsAsync(new MessageCompletionResult { Success = true });

        var service = new MessageProcessingService(
            connectorMock.Object,
            executorMock.Object,
            Options.Create(_options),
            _loggerMock.Object);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));

        // Act
        await service.StartAsync(cts.Token);
        await Task.Delay(300);
        await service.StopAsync();

        // Assert
        completedMessages.Should().ContainSingle().Which.Should().Be("success-msg");
        abandonedMessages.Should().ContainSingle().Which.Should().Be("retry-msg");
        deadLetteredMessages.Should().ContainSingle().Which.Should().Be("poison-msg");
    }
}
