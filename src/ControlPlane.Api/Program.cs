using ControlPlane.Api.Models;
using ControlPlane.Api.Services;
using ControlPlane.Api.AgentRuntime;
using ControlPlane.Api.Data;
using ControlPlane.Api.Grpc;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using NATS.Client.Core;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddOpenApi();
builder.Services.AddGrpc();

// Configure stores based on environment - use PostgreSQL stores by default, in-memory for tests
var useInMemoryStores = builder.Configuration.GetValue<bool>("UseInMemoryStores", false);

if (useInMemoryStores)
{
    builder.Services.AddSingleton<IAgentStore, InMemoryAgentStore>();
    builder.Services.AddSingleton<INodeStore, InMemoryNodeStore>();
    builder.Services.AddSingleton<IRunStore, InMemoryRunStore>();
}
else
{
    // Add Database context only when using PostgreSQL
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
    
    builder.Services.AddScoped<IAgentStore, PostgresAgentStore>();
    builder.Services.AddScoped<INodeStore, PostgresNodeStore>();
    builder.Services.AddScoped<IRunStore, PostgresRunStore>();
}

// Add Redis connection
var redisConnectionString = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379";
builder.Services.AddSingleton<IConnectionMultiplexer>(sp => 
    ConnectionMultiplexer.Connect(redisConnectionString));

// Add Redis-backed lease and lock stores
builder.Services.AddSingleton<ILeaseStore, RedisLeaseStore>();
builder.Services.AddSingleton<ILockStore, RedisLockStore>();

// Add NATS connection
var natsUrl = builder.Configuration.GetConnectionString("Nats") ?? "nats://localhost:4222";
builder.Services.AddSingleton<INatsConnection>(sp =>
{
    var opts = NatsOpts.Default with { Url = natsUrl };
    return new NatsConnection(opts);
});

// Add NATS event publisher
builder.Services.AddSingleton<INatsEventPublisher, NatsEventPublisher>();

// Add Microsoft Agent Framework runtime services
builder.Services.AddSingleton<IToolRegistry, InMemoryToolRegistry>();
builder.Services.AddSingleton(sp =>
{
    var configuration = builder.Configuration.GetSection("AgentRuntime");
    return new AgentRuntimeOptions
    {
        DefaultModel = configuration.GetValue<string>("DefaultModel") ?? "gpt-4",
        DefaultTemperature = configuration.GetValue<double>("DefaultTemperature", 0.7),
        MaxTokens = configuration.GetValue<int>("MaxTokens", 4000),
        MaxDurationSeconds = configuration.GetValue<int>("MaxDurationSeconds", 60)
    };
});
builder.Services.AddSingleton<IAgentRuntime, AgentRuntimeService>();

// Add LeaseService for gRPC
builder.Services.AddSingleton<ILeaseService, LeaseServiceLogic>();

var app = builder.Build();

// Initialize NATS JetStream streams (optional - fail gracefully if NATS is not available)
try
{
    var natsPublisher = app.Services.GetRequiredService<INatsEventPublisher>();
    await natsPublisher.InitializeStreamsAsync();
}
catch (Exception ex)
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogWarning(ex, "Failed to initialize NATS JetStream - events will not be published");
}

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// Map gRPC services
app.MapGrpcService<LeaseServiceImpl>();

// Agent endpoints
app.MapGet("/v1/agents", async (IAgentStore store) =>
{
    var agents = await store.GetAllAgentsAsync();
    return Results.Ok(agents);
})
.WithName("GetAgents")
.WithTags("Agents");

app.MapGet("/v1/agents/{agentId}", async (string agentId, IAgentStore store) =>
{
    var agent = await store.GetAgentAsync(agentId);
    return agent != null ? Results.Ok(agent) : Results.NotFound();
})
.WithName("GetAgent")
.WithTags("Agents");

app.MapPost("/v1/agents", async (CreateAgentRequest request, IAgentStore store) =>
{
    if (string.IsNullOrWhiteSpace(request.Name))
    {
        return Results.BadRequest(new { error = "Name is required" });
    }

    var agent = await store.CreateAgentAsync(request);
    return Results.Created($"/v1/agents/{agent.AgentId}", agent);
})
.WithName("CreateAgent")
.WithTags("Agents");

app.MapPut("/v1/agents/{agentId}", async (string agentId, UpdateAgentRequest request, IAgentStore store) =>
{
    var agent = await store.UpdateAgentAsync(agentId, request);
    return agent != null ? Results.Ok(agent) : Results.NotFound();
})
.WithName("UpdateAgent")
.WithTags("Agents");

app.MapDelete("/v1/agents/{agentId}", async (string agentId, IAgentStore store) =>
{
    var deleted = await store.DeleteAgentAsync(agentId);
    return deleted ? Results.NoContent() : Results.NotFound();
})
.WithName("DeleteAgent")
.WithTags("Agents");

// Node endpoints
app.MapGet("/v1/nodes", async (INodeStore store) =>
{
    var nodes = await store.GetAllNodesAsync();
    return Results.Ok(nodes);
})
.WithName("GetNodes")
.WithTags("Nodes");

app.MapGet("/v1/nodes/{nodeId}", async (string nodeId, INodeStore store) =>
{
    var node = await store.GetNodeAsync(nodeId);
    return node != null ? Results.Ok(node) : Results.NotFound();
})
.WithName("GetNode")
.WithTags("Nodes");

app.MapPost("/v1/nodes:register", async (RegisterNodeRequest request, INodeStore store) =>
{
    if (string.IsNullOrWhiteSpace(request.NodeId))
    {
        return Results.BadRequest(new { error = "NodeId is required" });
    }

    var node = await store.RegisterNodeAsync(request);
    return Results.Created($"/v1/nodes/{node.NodeId}", node);
})
.WithName("RegisterNode")
.WithTags("Nodes");

app.MapPost("/v1/nodes/{nodeId}:heartbeat", async (string nodeId, HeartbeatRequest request, INodeStore store) =>
{
    var node = await store.UpdateHeartbeatAsync(nodeId, request);
    return node != null ? Results.Ok(node) : Results.NotFound();
})
.WithName("Heartbeat")
.WithTags("Nodes");

app.MapDelete("/v1/nodes/{nodeId}", async (string nodeId, INodeStore store) =>
{
    var deleted = await store.DeleteNodeAsync(nodeId);
    return deleted ? Results.NoContent() : Results.NotFound();
})
.WithName("DeleteNode")
.WithTags("Nodes");

// Run endpoints
app.MapGet("/v1/runs", async (IRunStore store) =>
{
    var runs = await store.GetAllRunsAsync();
    return Results.Ok(runs);
})
.WithName("GetRuns")
.WithTags("Runs");

app.MapGet("/v1/runs/{runId}", async (string runId, IRunStore store) =>
{
    var run = await store.GetRunAsync(runId);
    return run != null ? Results.Ok(run) : Results.NotFound();
})
.WithName("GetRun")
.WithTags("Runs");

app.MapPost("/v1/runs/{runId}:complete", async (string runId, CompleteRunRequest request, IRunStore store) =>
{
    var run = await store.CompleteRunAsync(runId, request);
    return run != null ? Results.Ok(run) : Results.NotFound();
})
.WithName("CompleteRun")
.WithTags("Runs");

app.MapPost("/v1/runs/{runId}:fail", async (string runId, FailRunRequest request, IRunStore store) =>
{
    if (string.IsNullOrWhiteSpace(request.ErrorMessage))
    {
        return Results.BadRequest(new { error = "ErrorMessage is required" });
    }

    var run = await store.FailRunAsync(runId, request);
    return run != null ? Results.Ok(run) : Results.NotFound();
})
.WithName("FailRun")
.WithTags("Runs");

app.MapPost("/v1/runs/{runId}:cancel", async (string runId, CancelRunRequest request, IRunStore store) =>
{
    if (string.IsNullOrWhiteSpace(request.Reason))
    {
        return Results.BadRequest(new { error = "Reason is required" });
    }

    var run = await store.CancelRunAsync(runId, request);
    return run != null ? Results.Ok(run) : Results.NotFound();
})
.WithName("CancelRun")
.WithTags("Runs");

// NATS test endpoint - publishes a test event to verify JetStream setup
app.MapPost("/v1/events:test", async (INatsEventPublisher publisher) =>
{
    var testEvent = new ControlPlane.Api.Events.RunStateChangedEvent
    {
        RunId = "test-run-" + Guid.NewGuid().ToString()[..8],
        AgentId = "test-agent",
        NodeId = "test-node",
        PreviousState = "pending",
        NewState = "running",
        CorrelationId = Guid.NewGuid().ToString()
    };

    try
    {
        await publisher.PublishAsync(testEvent);
        return Results.Ok(new
        {
            message = "Test event published successfully",
            eventId = testEvent.EventId,
            eventType = testEvent.EventType,
            timestamp = testEvent.Timestamp
        });
    }
    catch (Exception ex)
    {
        return Results.Problem(
            detail: ex.Message,
            statusCode: 500,
            title: "Failed to publish test event"
        );
    }
})
.WithName("PublishTestEvent")
.WithTags("Events");

app.Run();

// Make the implicit Program class public for testing
public partial class Program { }
