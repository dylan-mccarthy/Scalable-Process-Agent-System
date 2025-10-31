using ControlPlane.Api.Models;
using ControlPlane.Api.Services;
using ControlPlane.Api.AgentRuntime;
using ControlPlane.Api.Data;
using ControlPlane.Api.Grpc;
using ControlPlane.Api.Observability;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using NATS.Client.Core;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// Configure OpenTelemetry
var otelConfig = builder.Configuration.GetSection("OpenTelemetry");
var serviceName = otelConfig.GetValue<string>("ServiceName") ?? TelemetryConfig.ServiceName;
var serviceVersion = otelConfig.GetValue<string>("ServiceVersion") ?? TelemetryConfig.ServiceVersion;
var otlpEndpoint = otelConfig.GetValue<string>("OtlpExporter:Endpoint") ?? "http://localhost:4317";
var consoleExporterEnabled = otelConfig.GetValue<bool>("ConsoleExporter:Enabled", false);
var tracesEnabled = otelConfig.GetValue<bool>("Traces:Enabled", true);
var metricsEnabled = otelConfig.GetValue<bool>("Metrics:Enabled", true);

var resourceBuilder = ResourceBuilder.CreateDefault()
    .AddService(serviceName: serviceName, serviceVersion: serviceVersion)
    .AddAttributes(new Dictionary<string, object>
    {
        ["deployment.environment"] = builder.Environment.EnvironmentName,
        ["host.name"] = Environment.MachineName
    });

// Configure Tracing
if (tracesEnabled)
{
    builder.Services.AddOpenTelemetry()
        .WithTracing(tracing =>
        {
            tracing.SetResourceBuilder(resourceBuilder)
                .AddSource(TelemetryConfig.ActivitySource.Name)
                .AddAspNetCoreInstrumentation(options =>
                {
                    options.RecordException = true;
                    options.Filter = httpContext =>
                    {
                        // Don't trace health check endpoints
                        return !httpContext.Request.Path.StartsWithSegments("/health");
                    };
                })
                .AddGrpcClientInstrumentation()
                .AddHttpClientInstrumentation();

            if (consoleExporterEnabled)
            {
                tracing.AddConsoleExporter();
            }

            tracing.AddOtlpExporter(options =>
            {
                options.Endpoint = new Uri(otlpEndpoint);
            });
        });
}

// Configure Metrics
if (metricsEnabled)
{
    builder.Services.AddOpenTelemetry()
        .WithMetrics(metrics =>
        {
            metrics.SetResourceBuilder(resourceBuilder)
                .AddMeter(TelemetryConfig.Meter.Name)
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation();

            if (consoleExporterEnabled)
            {
                metrics.AddConsoleExporter();
            }

            metrics.AddOtlpExporter(options =>
            {
                options.Endpoint = new Uri(otlpEndpoint);
            });
        });
}

// Configure Logging
builder.Logging.AddOpenTelemetry(logging =>
{
    logging.SetResourceBuilder(resourceBuilder);
    logging.IncludeFormattedMessage = otelConfig.GetValue<bool>("Logs:IncludeFormattedMessage", true);
    logging.IncludeScopes = otelConfig.GetValue<bool>("Logs:IncludeScopes", true);

    if (consoleExporterEnabled)
    {
        logging.AddConsoleExporter();
    }

    logging.AddOtlpExporter(options =>
    {
        options.Endpoint = new Uri(otlpEndpoint);
    });
});

// Add services to the container
builder.Services.AddOpenApi();
builder.Services.AddGrpc();

// Configure authentication
var authConfig = builder.Configuration.GetSection("Authentication").Get<AuthenticationOptions>() 
    ?? new AuthenticationOptions();

if (authConfig.Enabled)
{
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.Authority = authConfig.Authority;
            options.Audience = authConfig.Audience;
            options.RequireHttpsMetadata = authConfig.RequireHttpsMetadata;
            
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = authConfig.ValidateIssuer,
                ValidateAudience = authConfig.ValidateAudience,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ClockSkew = TimeSpan.FromMinutes(5)
            };

            if (authConfig.ValidIssuers?.Length > 0)
            {
                options.TokenValidationParameters.ValidIssuers = authConfig.ValidIssuers;
            }

            if (authConfig.ValidAudiences?.Length > 0)
            {
                options.TokenValidationParameters.ValidAudiences = authConfig.ValidAudiences;
            }

            if (!string.IsNullOrEmpty(authConfig.MetadataAddress))
            {
                options.MetadataAddress = authConfig.MetadataAddress;
            }

            options.Events = new JwtBearerEvents
            {
                OnAuthenticationFailed = context =>
                {
                    var logger = context.HttpContext.RequestServices
                        .GetRequiredService<ILogger<Program>>();
                    logger.LogWarning(context.Exception, 
                        "Authentication failed for {Path}", context.Request.Path);
                    return Task.CompletedTask;
                },
                OnTokenValidated = context =>
                {
                    var logger = context.HttpContext.RequestServices
                        .GetRequiredService<ILogger<Program>>();
                    logger.LogDebug("Token validated for {User}", 
                        context.Principal?.Identity?.Name ?? "unknown");
                    return Task.CompletedTask;
                }
            };
        });

    builder.Services.AddAuthorization();
}


// Configure stores based on environment - use PostgreSQL stores by default, in-memory for tests
var useInMemoryStores = builder.Configuration.GetValue<bool>("UseInMemoryStores", false);

if (useInMemoryStores)
{
    builder.Services.AddSingleton<IAgentStore, InMemoryAgentStore>();
    builder.Services.AddSingleton<INodeStore, InMemoryNodeStore>();
    builder.Services.AddSingleton<IRunStore, InMemoryRunStore>();
    builder.Services.AddSingleton<IDeploymentStore, InMemoryDeploymentStore>();
}
else
{
    // Add Database context only when using PostgreSQL
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
    
    builder.Services.AddScoped<IAgentStore, PostgresAgentStore>();
    builder.Services.AddScoped<INodeStore, PostgresNodeStore>();
    builder.Services.AddScoped<IRunStore, PostgresRunStore>();
    builder.Services.AddScoped<IDeploymentStore, PostgresDeploymentStore>();
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
builder.Services.AddSingleton<IAzureAIFoundryToolProvider, AzureAIFoundryToolProvider>();
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

// Add Scheduler service
builder.Services.AddSingleton<IScheduler, LeastLoadedScheduler>();

// Add LeaseService for gRPC
builder.Services.AddSingleton<ILeaseService, LeaseServiceLogic>();

// Add Agent Spec Validator
builder.Services.AddSingleton<IAgentSpecValidator, AgentSpecValidator>();

// Add Metrics Service for observable gauges
builder.Services.AddSingleton<IMetricsService, MetricsService>();

var app = builder.Build();

// Initialize observable gauges for metrics
var metricsService = app.Services.GetRequiredService<IMetricsService>();

TelemetryConfig.ActiveRunsGauge = TelemetryConfig.Meter.CreateObservableGauge(
    "active_runs",
    () => metricsService.GetActiveRunsCount(),
    description: "Current number of active runs (running or pending)");

TelemetryConfig.ActiveNodesGauge = TelemetryConfig.Meter.CreateObservableGauge(
    "active_nodes",
    () => metricsService.GetActiveNodesCount(),
    description: "Current number of active nodes");

TelemetryConfig.TotalSlotsGauge = TelemetryConfig.Meter.CreateObservableGauge(
    "total_slots",
    () => metricsService.GetTotalSlots(),
    description: "Total number of slots across all active nodes");

TelemetryConfig.UsedSlotsGauge = TelemetryConfig.Meter.CreateObservableGauge(
    "used_slots",
    () => metricsService.GetUsedSlots(),
    description: "Number of slots currently in use");

TelemetryConfig.AvailableSlotsGauge = TelemetryConfig.Meter.CreateObservableGauge(
    "available_slots",
    () => metricsService.GetAvailableSlots(),
    description: "Number of slots currently available");

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

// Initialize Azure AI Foundry tools with the tool registry
try
{
    var azureAIFoundryProvider = app.Services.GetRequiredService<IAzureAIFoundryToolProvider>();
    await azureAIFoundryProvider.RegisterAzureAIFoundryToolsAsync();
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogInformation("Azure AI Foundry tools registered successfully");
}
catch (Exception ex)
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogError(ex, "Failed to register Azure AI Foundry tools");
}

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// Add authentication and authorization middleware if enabled
if (authConfig.Enabled)
{
    app.UseAuthentication();
    app.UseAuthorization();
}

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

// Agent Version endpoints
app.MapPost("/v1/agents/{agentId}:version", async (string agentId, CreateAgentVersionRequest request, IAgentStore store, IAgentSpecValidator specValidator) =>
{
    if (string.IsNullOrWhiteSpace(request.Version))
    {
        return Results.BadRequest(new { error = "Version is required" });
    }

    try
    {
        // Validate semantic version format
        VersionValidator.ValidateOrThrow(request.Version);
        
        // Validate agent specification
        var validationResult = specValidator.Validate(request.Spec);
        if (!validationResult.IsValid)
        {
            return Results.BadRequest(new { error = "Spec validation failed", errors = validationResult.Errors });
        }
        
        var version = await store.CreateVersionAsync(agentId, request);
        return Results.Created($"/v1/agents/{agentId}/versions/{version.Version}", version);
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
    catch (InvalidOperationException ex)
    {
        return Results.Conflict(new { error = ex.Message });
    }
})
.WithName("CreateAgentVersion")
.WithTags("Agents");

app.MapGet("/v1/agents/{agentId}/versions", async (string agentId, IAgentStore store) =>
{
    var versions = await store.GetVersionsAsync(agentId);
    return Results.Ok(versions);
})
.WithName("GetAgentVersions")
.WithTags("Agents");

app.MapGet("/v1/agents/{agentId}/versions/{version}", async (string agentId, string version, IAgentStore store) =>
{
    var versionResponse = await store.GetVersionAsync(agentId, version);
    return versionResponse != null ? Results.Ok(versionResponse) : Results.NotFound();
})
.WithName("GetAgentVersion")
.WithTags("Agents");

app.MapDelete("/v1/agents/{agentId}/versions/{version}", async (string agentId, string version, IAgentStore store) =>
{
    var deleted = await store.DeleteVersionAsync(agentId, version);
    return deleted ? Results.NoContent() : Results.NotFound();
})
.WithName("DeleteAgentVersion")
.WithTags("Agents");

// Deployment endpoints
app.MapGet("/v1/deployments", async (IDeploymentStore store) =>
{
    var deployments = await store.GetAllDeploymentsAsync();
    return Results.Ok(deployments);
})
.WithName("GetDeployments")
.WithTags("Deployments");

app.MapGet("/v1/deployments/{depId}", async (string depId, IDeploymentStore store) =>
{
    var deployment = await store.GetDeploymentAsync(depId);
    return deployment != null ? Results.Ok(deployment) : Results.NotFound();
})
.WithName("GetDeployment")
.WithTags("Deployments");

app.MapGet("/v1/agents/{agentId}/deployments", async (string agentId, IDeploymentStore store) =>
{
    var deployments = await store.GetDeploymentsByAgentAsync(agentId);
    return Results.Ok(deployments);
})
.WithName("GetAgentDeployments")
.WithTags("Deployments");

app.MapPost("/v1/deployments", async (CreateDeploymentRequest request, IDeploymentStore store, IAgentStore agentStore) =>
{
    if (string.IsNullOrWhiteSpace(request.AgentId))
    {
        return Results.BadRequest(new { error = "AgentId is required" });
    }

    if (string.IsNullOrWhiteSpace(request.Version))
    {
        return Results.BadRequest(new { error = "Version is required" });
    }

    if (string.IsNullOrWhiteSpace(request.Env))
    {
        return Results.BadRequest(new { error = "Env is required" });
    }

    // Validate that the agent version exists
    var version = await agentStore.GetVersionAsync(request.AgentId, request.Version);
    if (version == null)
    {
        return Results.NotFound(new { error = $"Agent version {request.AgentId}:{request.Version} not found" });
    }

    var deployment = await store.CreateDeploymentAsync(request);
    return Results.Created($"/v1/deployments/{deployment.DepId}", deployment);
})
.WithName("CreateDeployment")
.WithTags("Deployments");

app.MapPut("/v1/deployments/{depId}", async (string depId, UpdateDeploymentStatusRequest request, IDeploymentStore store) =>
{
    var deployment = await store.UpdateDeploymentStatusAsync(depId, request);
    return deployment != null ? Results.Ok(deployment) : Results.NotFound();
})
.WithName("UpdateDeploymentStatus")
.WithTags("Deployments");

app.MapDelete("/v1/deployments/{depId}", async (string depId, IDeploymentStore store) =>
{
    var deleted = await store.DeleteDeploymentAsync(depId);
    return deleted ? Results.NoContent() : Results.NotFound();
})
.WithName("DeleteDeployment")
.WithTags("Deployments");


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
