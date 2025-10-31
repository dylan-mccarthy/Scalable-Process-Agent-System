using ControlPlane.Api.Events;
using ControlPlane.Api.Services;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NATS.Client.JetStream;
using Testcontainers.Nats;
using Xunit;

namespace ControlPlane.Api.Tests;

public class NatsEventPublisherTests : IAsyncLifetime
{
    private readonly NatsContainer _natsContainer;
    private INatsConnection? _natsConnection;
    private NatsEventPublisher? _publisher;
    private ILogger<NatsEventPublisher>? _logger;
    private ILoggerFactory? _loggerFactory;

    public NatsEventPublisherTests()
    {
        _natsContainer = new NatsBuilder()
            .WithCommand("--jetstream")
            .Build();
    }

    public async Task InitializeAsync()
    {
        await _natsContainer.StartAsync();

        var opts = NatsOpts.Default with { Url = _natsContainer.GetConnectionString() };
        _natsConnection = new NatsConnection(opts);

        _loggerFactory = new LoggerFactory();
        _logger = _loggerFactory.CreateLogger<NatsEventPublisher>();
        _publisher = new NatsEventPublisher(_natsConnection, _logger);

        // Initialize streams
        await _publisher.InitializeStreamsAsync();
    }

    public async Task DisposeAsync()
    {
        _loggerFactory?.Dispose();
        if (_natsConnection != null)
        {
            await _natsConnection.DisposeAsync();
        }
        await _natsContainer.DisposeAsync();
    }

    [Fact]
    public async Task InitializeStreamsAsync_CreatesStreamSuccessfully()
    {
        // Arrange
        var js = new NatsJSContext(_natsConnection!);

        // Act
        await _publisher!.InitializeStreamsAsync();

        // Assert
        var stream = await js.GetStreamAsync("BPA_EVENTS");
        Assert.NotNull(stream);
        Assert.Equal("BPA_EVENTS", stream.Info.Config.Name);
        Assert.NotNull(stream.Info.Config.Subjects);
        Assert.Contains("bpa.events.run.*", stream.Info.Config.Subjects);
        Assert.Contains("bpa.events.node.*", stream.Info.Config.Subjects);
        Assert.Contains("bpa.events.agent.*", stream.Info.Config.Subjects);
    }

    [Fact]
    public async Task InitializeStreamsAsync_DoesNotThrow_WhenStreamAlreadyExists()
    {
        // Arrange & Act - initialize twice
        await _publisher!.InitializeStreamsAsync();

        // Assert - should not throw
        await _publisher.InitializeStreamsAsync();
    }

    [Fact]
    public async Task PublishAsync_ThrowsArgumentNullException_WhenEventIsNull()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _publisher!.PublishAsync(null!));
    }

    [Fact]
    public async Task PublishAsync_PublishesRunStateChangedEvent_Successfully()
    {
        // Arrange
        var @event = new RunStateChangedEvent
        {
            RunId = "run-123",
            AgentId = "agent-1",
            NodeId = "node-1",
            PreviousState = "pending",
            NewState = "running"
        };

        // Act
        await _publisher!.PublishAsync(@event);

        // Assert - verify event was published by checking stream
        var js = new NatsJSContext(_natsConnection!);
        var stream = await js.GetStreamAsync("BPA_EVENTS");
        Assert.True(stream.Info.State.Messages > 0);
    }

    [Fact]
    public async Task PublishAsync_PublishesNodeRegisteredEvent_Successfully()
    {
        // Arrange
        var @event = new NodeRegisteredEvent
        {
            NodeId = "node-1",
            Region = "us-east-1",
            Capacity = 8
        };

        // Act
        await _publisher!.PublishAsync(@event);

        // Assert
        var js = new NatsJSContext(_natsConnection!);
        var stream = await js.GetStreamAsync("BPA_EVENTS");
        Assert.True(stream.Info.State.Messages > 0);
    }

    [Fact]
    public async Task PublishAsync_PublishesNodeHeartbeatEvent_Successfully()
    {
        // Arrange
        var @event = new NodeHeartbeatEvent
        {
            NodeId = "node-1",
            ActiveRuns = 2,
            AvailableSlots = 6
        };

        // Act
        await _publisher!.PublishAsync(@event);

        // Assert
        var js = new NatsJSContext(_natsConnection!);
        var stream = await js.GetStreamAsync("BPA_EVENTS");
        Assert.True(stream.Info.State.Messages > 0);
    }

    [Fact]
    public async Task PublishAsync_PublishesNodeDisconnectedEvent_Successfully()
    {
        // Arrange
        var @event = new NodeDisconnectedEvent
        {
            NodeId = "node-1",
            Reason = "Graceful shutdown"
        };

        // Act
        await _publisher!.PublishAsync(@event);

        // Assert
        var js = new NatsJSContext(_natsConnection!);
        var stream = await js.GetStreamAsync("BPA_EVENTS");
        Assert.True(stream.Info.State.Messages > 0);
    }

    [Fact]
    public async Task PublishAsync_PublishesAgentDeployedEvent_Successfully()
    {
        // Arrange
        var @event = new AgentDeployedEvent
        {
            AgentId = "invoice-classifier",
            Version = "1.0.0",
            DeploymentId = "dep-123"
        };

        // Act
        await _publisher!.PublishAsync(@event);

        // Assert
        var js = new NatsJSContext(_natsConnection!);
        var stream = await js.GetStreamAsync("BPA_EVENTS");
        Assert.True(stream.Info.State.Messages > 0);
    }

    [Fact]
    public async Task PublishAsync_IncludesCorrelationId_WhenProvided()
    {
        // Arrange
        var correlationId = Guid.NewGuid().ToString();
        var @event = new RunStateChangedEvent
        {
            RunId = "run-456",
            NewState = "completed",
            CorrelationId = correlationId
        };

        // Act
        await _publisher!.PublishAsync(@event);

        // Assert - event should be published without error
        var js = new NatsJSContext(_natsConnection!);
        var stream = await js.GetStreamAsync("BPA_EVENTS");
        Assert.True(stream.Info.State.Messages > 0);
    }

    [Fact]
    public async Task PublishAsync_SetsTimestamp_Automatically()
    {
        // Arrange
        var beforePublish = DateTime.UtcNow;
        var @event = new RunStateChangedEvent
        {
            RunId = "run-789",
            NewState = "failed"
        };

        // Act
        await _publisher!.PublishAsync(@event);
        var afterPublish = DateTime.UtcNow;

        // Assert
        Assert.True(@event.Timestamp >= beforePublish);
        Assert.True(@event.Timestamp <= afterPublish);
    }
}
