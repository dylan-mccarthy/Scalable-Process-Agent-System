using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Node.Runtime.Configuration;
using Node.Runtime.Connectors;
using Node.Runtime.Services;

namespace Node.Runtime.Tests.Services;

public sealed class MessageProcessingServiceTests
{
    private readonly Mock<IInputConnector> _inputConnectorMock;
    private readonly Mock<IAgentExecutor> _agentExecutorMock;
    private readonly Mock<ILogger<MessageProcessingService>> _loggerMock;
    private readonly ServiceBusConnectorOptions _options;

    public MessageProcessingServiceTests()
    {
        _inputConnectorMock = new Mock<IInputConnector>();
        _agentExecutorMock = new Mock<IAgentExecutor>();
        _loggerMock = new Mock<ILogger<MessageProcessingService>>();
        _options = new ServiceBusConnectorOptions
        {
            QueueName = "test-queue",
            MaxConcurrentCalls = 5,
            MaxWaitTime = TimeSpan.FromSeconds(1),
            MaxDeliveryCount = 3
        };
    }

    [Fact]
    public void Constructor_WithValidParameters_ShouldSucceed()
    {
        // Act
        var service = new MessageProcessingService(
            _inputConnectorMock.Object,
            _agentExecutorMock.Object,
            Options.Create(_options),
            _loggerMock.Object);

        // Assert
        service.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithNullInputConnector_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        var act = () => new MessageProcessingService(
            null!,
            _agentExecutorMock.Object,
            Options.Create(_options),
            _loggerMock.Object);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("inputConnector");
    }

    [Fact]
    public void Constructor_WithNullAgentExecutor_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        var act = () => new MessageProcessingService(
            _inputConnectorMock.Object,
            null!,
            Options.Create(_options),
            _loggerMock.Object);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("agentExecutor");
    }

    [Fact]
    public void Constructor_WithNullOptions_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        var act = () => new MessageProcessingService(
            _inputConnectorMock.Object,
            _agentExecutorMock.Object,
            null!,
            _loggerMock.Object);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        var act = () => new MessageProcessingService(
            _inputConnectorMock.Object,
            _agentExecutorMock.Object,
            Options.Create(_options),
            null!);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    [Fact]
    public async Task StartAsync_ShouldInitializeConnector()
    {
        // Arrange
        var service = CreateService();
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        // Setup to return empty messages to avoid processing loop
        _inputConnectorMock.Setup(x => x.ReceiveMessagesAsync(
            It.IsAny<int>(),
            It.IsAny<TimeSpan?>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ReceivedMessage>());

        // Act
        await service.StartAsync(cts.Token);
        await Task.Delay(200); // Let the background task run briefly
        await service.StopAsync();

        // Assert
        _inputConnectorMock.Verify(
            x => x.InitializeAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task StopAsync_ShouldCloseConnector()
    {
        // Arrange
        var service = CreateService();
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        _inputConnectorMock.Setup(x => x.ReceiveMessagesAsync(
            It.IsAny<int>(),
            It.IsAny<TimeSpan?>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ReceivedMessage>());

        await service.StartAsync(cts.Token);

        // Act
        await service.StopAsync();

        // Assert
        _inputConnectorMock.Verify(
            x => x.CloseAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessMessage_WithSuccessfulExecution_ShouldCompleteMessage()
    {
        // Arrange
        var service = CreateService();
        var message = CreateTestMessage("msg-1", deliveryCount: 1);
        var messages = new List<ReceivedMessage> { message };

        var callCount = 0;
        _inputConnectorMock.Setup(x => x.ReceiveMessagesAsync(
            It.IsAny<int>(),
            It.IsAny<TimeSpan?>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                return callCount == 1 ? messages : new List<ReceivedMessage>();
            });

        _agentExecutorMock.Setup(x => x.ExecuteAsync(
            It.IsAny<AgentSpec>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentExecutionResult
            {
                Success = true,
                Output = "Success"
            });

        _inputConnectorMock.Setup(x => x.CompleteMessageAsync(
            It.IsAny<ReceivedMessage>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MessageCompletionResult { Success = true });

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));

        // Act
        await service.StartAsync(cts.Token);
        await Task.Delay(300); // Give time for message processing
        await service.StopAsync();

        // Assert
        _inputConnectorMock.Verify(
            x => x.CompleteMessageAsync(message, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessMessage_WithPoisonMessage_ShouldDeadLetterImmediately()
    {
        // Arrange
        var service = CreateService();
        var message = CreateTestMessage("msg-1", deliveryCount: 4); // Exceeds MaxDeliveryCount of 3
        var messages = new List<ReceivedMessage> { message };

        var callCount = 0;
        _inputConnectorMock.Setup(x => x.ReceiveMessagesAsync(
            It.IsAny<int>(),
            It.IsAny<TimeSpan?>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                return callCount == 1 ? messages : new List<ReceivedMessage>();
            });

        _inputConnectorMock.Setup(x => x.DeadLetterMessageAsync(
            It.IsAny<ReceivedMessage>(),
            It.IsAny<string>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MessageCompletionResult { Success = true });

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));

        // Act
        await service.StartAsync(cts.Token);
        await Task.Delay(300);
        await service.StopAsync();

        // Assert
        _inputConnectorMock.Verify(
            x => x.DeadLetterMessageAsync(
                message,
                "PoisonMessage",
                It.Is<string>(s => s.Contains("exceeded maximum delivery count")),
                It.IsAny<CancellationToken>()),
            Times.Once);

        // Should not execute agent for poison messages
        _agentExecutorMock.Verify(
            x => x.ExecuteAsync(It.IsAny<AgentSpec>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ProcessMessage_WithDeliveryCountAtMax_ShouldProcessAndHandleFailure()
    {
        // Arrange
        var service = CreateService();
        var message = CreateTestMessage("msg-1", deliveryCount: 3); // At MaxDeliveryCount of 3
        var messages = new List<ReceivedMessage> { message };

        var callCount = 0;
        _inputConnectorMock.Setup(x => x.ReceiveMessagesAsync(
            It.IsAny<int>(),
            It.IsAny<TimeSpan?>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                return callCount == 1 ? messages : new List<ReceivedMessage>();
            });

        // Simulate a failure during processing
        _agentExecutorMock.Setup(x => x.ExecuteAsync(
            It.IsAny<AgentSpec>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentExecutionResult
            {
                Success = false,
                Error = "Temporary error"
            });

        _inputConnectorMock.Setup(x => x.DeadLetterMessageAsync(
            It.IsAny<ReceivedMessage>(),
            It.IsAny<string>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MessageCompletionResult { Success = true });

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));

        // Act
        await service.StartAsync(cts.Token);
        await Task.Delay(300);
        await service.StopAsync();

        // Assert - Message at max count should be processed, then dead-lettered on failure
        _agentExecutorMock.Verify(
            x => x.ExecuteAsync(It.IsAny<AgentSpec>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once,
            "Agent should execute for message at max delivery count");

        _inputConnectorMock.Verify(
            x => x.DeadLetterMessageAsync(
                message,
                "MaxDeliveryCountExceeded",
                It.Is<string>(s => s.Contains("exceeded 3 delivery attempts")),
                It.IsAny<CancellationToken>()),
            Times.Once,
            "Failed message at MaxDeliveryCount should be dead-lettered");
    }

    [Fact]
    public async Task ProcessMessage_WithNonRetryableError_ShouldDeadLetter()
    {
        // Arrange
        var service = CreateService();
        var message = CreateTestMessage("msg-1", deliveryCount: 1);
        var messages = new List<ReceivedMessage> { message };

        var callCount = 0;
        _inputConnectorMock.Setup(x => x.ReceiveMessagesAsync(
            It.IsAny<int>(),
            It.IsAny<TimeSpan?>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                return callCount == 1 ? messages : new List<ReceivedMessage>();
            });

        _agentExecutorMock.Setup(x => x.ExecuteAsync(
            It.IsAny<AgentSpec>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentExecutionResult
            {
                Success = false,
                Error = "Agent execution exceeded maximum duration of 60 seconds" // timeout = non-retryable
            });

        _inputConnectorMock.Setup(x => x.DeadLetterMessageAsync(
            It.IsAny<ReceivedMessage>(),
            It.IsAny<string>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MessageCompletionResult { Success = true });

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));

        // Act
        await service.StartAsync(cts.Token);
        await Task.Delay(300);
        await service.StopAsync();

        // Assert
        _inputConnectorMock.Verify(
            x => x.DeadLetterMessageAsync(
                message,
                "NonRetryableError",
                It.Is<string>(s => s.Contains("exceeded maximum duration")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessMessage_WithRetryableError_ShouldAbandon()
    {
        // Arrange
        var service = CreateService();
        var message = CreateTestMessage("msg-1", deliveryCount: 1);
        var messages = new List<ReceivedMessage> { message };

        var callCount = 0;
        _inputConnectorMock.Setup(x => x.ReceiveMessagesAsync(
            It.IsAny<int>(),
            It.IsAny<TimeSpan?>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                return callCount == 1 ? messages : new List<ReceivedMessage>();
            });

        _agentExecutorMock.Setup(x => x.ExecuteAsync(
            It.IsAny<AgentSpec>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentExecutionResult
            {
                Success = false,
                Error = "Temporary network error" // retryable
            });

        _inputConnectorMock.Setup(x => x.AbandonMessageAsync(
            It.IsAny<ReceivedMessage>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MessageCompletionResult { Success = true });

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));

        // Act
        await service.StartAsync(cts.Token);
        await Task.Delay(300);
        await service.StopAsync();

        // Assert
        _inputConnectorMock.Verify(
            x => x.AbandonMessageAsync(message, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessMessage_WithRetryableErrorAndMaxDeliveryCount_ShouldDeadLetter()
    {
        // Arrange
        var service = CreateService();
        var message = CreateTestMessage("msg-1", deliveryCount: 3); // At max delivery count
        var messages = new List<ReceivedMessage> { message };

        var callCount = 0;
        _inputConnectorMock.Setup(x => x.ReceiveMessagesAsync(
            It.IsAny<int>(),
            It.IsAny<TimeSpan?>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                return callCount == 1 ? messages : new List<ReceivedMessage>();
            });

        _agentExecutorMock.Setup(x => x.ExecuteAsync(
            It.IsAny<AgentSpec>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentExecutionResult
            {
                Success = false,
                Error = "Temporary error" // retryable but at max delivery count
            });

        _inputConnectorMock.Setup(x => x.DeadLetterMessageAsync(
            It.IsAny<ReceivedMessage>(),
            It.IsAny<string>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MessageCompletionResult { Success = true });

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));

        // Act
        await service.StartAsync(cts.Token);
        await Task.Delay(300);
        await service.StopAsync();

        // Assert
        _inputConnectorMock.Verify(
            x => x.DeadLetterMessageAsync(
                message,
                "MaxDeliveryCountExceeded",
                It.Is<string>(s => s.Contains("exceeded 3 delivery attempts")),
                It.IsAny<CancellationToken>()),
            Times.Once,
            "Message at max delivery count should be dead-lettered after failure");

        // Should not abandon
        _inputConnectorMock.Verify(
            x => x.AbandonMessageAsync(It.IsAny<ReceivedMessage>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ProcessMessage_WithUnexpectedException_ShouldAbandon()
    {
        // Arrange
        var service = CreateService();
        var message = CreateTestMessage("msg-1", deliveryCount: 1);
        var messages = new List<ReceivedMessage> { message };

        var callCount = 0;
        _inputConnectorMock.Setup(x => x.ReceiveMessagesAsync(
            It.IsAny<int>(),
            It.IsAny<TimeSpan?>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                return callCount == 1 ? messages : new List<ReceivedMessage>();
            });

        _agentExecutorMock.Setup(x => x.ExecuteAsync(
            It.IsAny<AgentSpec>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Unexpected error"));

        _inputConnectorMock.Setup(x => x.AbandonMessageAsync(
            It.IsAny<ReceivedMessage>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MessageCompletionResult { Success = true });

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));

        // Act
        await service.StartAsync(cts.Token);
        await Task.Delay(300);
        await service.StopAsync();

        // Assert
        _inputConnectorMock.Verify(
            x => x.AbandonMessageAsync(message, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Theory]
    [InlineData("timeout")]
    [InlineData("exceeded maximum duration")]
    [InlineData("deserialization error")]
    [InlineData("invalid format")]
    [InlineData("bad request")]
    [InlineData("unauthorized")]
    [InlineData("forbidden")]
    [InlineData("not found")]
    public async Task ProcessMessage_WithNonRetryableErrorPatterns_ShouldDeadLetter(string errorPattern)
    {
        // Arrange
        var service = CreateService();
        var message = CreateTestMessage("msg-1", deliveryCount: 1);
        var messages = new List<ReceivedMessage> { message };

        var callCount = 0;
        _inputConnectorMock.Setup(x => x.ReceiveMessagesAsync(
            It.IsAny<int>(),
            It.IsAny<TimeSpan?>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                return callCount == 1 ? messages : new List<ReceivedMessage>();
            });

        _agentExecutorMock.Setup(x => x.ExecuteAsync(
            It.IsAny<AgentSpec>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentExecutionResult
            {
                Success = false,
                Error = $"Error: {errorPattern}"
            });

        _inputConnectorMock.Setup(x => x.DeadLetterMessageAsync(
            It.IsAny<ReceivedMessage>(),
            It.IsAny<string>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MessageCompletionResult { Success = true });

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));

        // Act
        await service.StartAsync(cts.Token);
        await Task.Delay(300);
        await service.StopAsync();

        // Assert
        _inputConnectorMock.Verify(
            x => x.DeadLetterMessageAsync(
                message,
                "NonRetryableError",
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private MessageProcessingService CreateService()
    {
        return new MessageProcessingService(
            _inputConnectorMock.Object,
            _agentExecutorMock.Object,
            Options.Create(_options),
            _loggerMock.Object);
    }

    private ReceivedMessage CreateTestMessage(string messageId, int deliveryCount)
    {
        return new ReceivedMessage
        {
            MessageId = messageId,
            Body = "Test message body",
            DeliveryCount = deliveryCount,
            EnqueuedTime = DateTimeOffset.UtcNow,
            Properties = new Dictionary<string, object>
            {
                { "AgentId", "test-agent" },
                { "Version", "1.0" }
            }
        };
    }
}
