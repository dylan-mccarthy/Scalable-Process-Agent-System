using System.Text.Json;
using ControlPlane.Api.Events;
using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;

namespace ControlPlane.Api.Services;

/// <summary>
/// NATS JetStream event publisher implementation
/// </summary>
public class NatsEventPublisher : INatsEventPublisher
{
    private readonly INatsConnection _natsConnection;
    private readonly ILogger<NatsEventPublisher> _logger;
    private readonly JsonSerializerOptions _jsonOptions;
    private const string StreamName = "BPA_EVENTS";

    public NatsEventPublisher(INatsConnection natsConnection, ILogger<NatsEventPublisher> logger)
    {
        _natsConnection = natsConnection;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
    }

    /// <inheritdoc />
    public async Task PublishAsync(SystemEvent @event, CancellationToken cancellationToken = default)
    {
        if (@event == null)
        {
            throw new ArgumentNullException(nameof(@event));
        }

        var subject = GetSubjectForEvent(@event);
        var json = JsonSerializer.Serialize(@event, @event.GetType(), _jsonOptions);
        
        _logger.LogInformation("Publishing event {EventType} to subject {Subject}", @event.EventType, subject);

        try
        {
            var js = new NatsJSContext(_natsConnection);
            var ack = await js.PublishAsync(subject, json, cancellationToken: cancellationToken);
            
            _logger.LogDebug("Event published successfully. Stream: {Stream}, Sequence: {Sequence}", 
                ack.Stream, ack.Seq);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish event {EventType} to subject {Subject}", 
                @event.EventType, subject);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task InitializeStreamsAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Initializing JetStream streams");

        try
        {
            var js = new NatsJSContext(_natsConnection);
            
            // Define the stream configuration
            var streamConfig = new StreamConfig(
                name: StreamName,
                subjects: new[]
                {
                    "bpa.events.run.*",
                    "bpa.events.node.*",
                    "bpa.events.agent.*"
                })
            {
                Description = "Business Process Agents system events",
                Retention = StreamConfigRetention.Limits,
                MaxAge = TimeSpan.FromDays(7), // Retain events for 7 days
                MaxBytes = 1024 * 1024 * 1024, // 1GB max storage
                Storage = StreamConfigStorage.File,
                NumReplicas = 1, // Single replica for MVP
                Discard = StreamConfigDiscard.Old
            };

            // Try to get existing stream first
            try
            {
                var existingStream = await js.GetStreamAsync(StreamName, cancellationToken: cancellationToken);
                _logger.LogInformation("JetStream stream '{StreamName}' already exists", StreamName);
            }
            catch (NatsJSApiException ex) when (ex.Error.Code == 404)
            {
                // Stream doesn't exist, create it
                _logger.LogInformation("Creating JetStream stream '{StreamName}'", StreamName);
                await js.CreateStreamAsync(streamConfig, cancellationToken);
                _logger.LogInformation("JetStream stream '{StreamName}' created successfully", StreamName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize JetStream streams");
            throw;
        }
    }

    private static string GetSubjectForEvent(SystemEvent @event)
    {
        return @event.EventType switch
        {
            "run.state.changed" => "bpa.events.run.state-changed",
            "node.registered" => "bpa.events.node.registered",
            "node.heartbeat" => "bpa.events.node.heartbeat",
            "node.disconnected" => "bpa.events.node.disconnected",
            "agent.deployed" => "bpa.events.agent.deployed",
            _ => throw new ArgumentException($"Unknown event type: {@event.EventType}", nameof(@event))
        };
    }
}
