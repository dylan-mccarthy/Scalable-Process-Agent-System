using Node.Runtime;
using Node.Runtime.Configuration;
using Node.Runtime.Services;
using ControlPlane.Api.Grpc;
using Grpc.Net.Client;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Metrics;
using OpenTelemetry.Logs;

var builder = Host.CreateApplicationBuilder(args);

// Configure options
builder.Services.Configure<NodeRuntimeOptions>(builder.Configuration.GetSection("NodeRuntime"));
builder.Services.Configure<AgentRuntimeOptions>(builder.Configuration.GetSection("AgentRuntime"));
builder.Services.Configure<OpenTelemetryOptions>(builder.Configuration.GetSection("OpenTelemetry"));

// Get configuration values for service setup
var nodeRuntimeConfig = builder.Configuration.GetSection("NodeRuntime").Get<NodeRuntimeOptions>() ?? new NodeRuntimeOptions();
var otelConfig = builder.Configuration.GetSection("OpenTelemetry").Get<OpenTelemetryOptions>() ?? new OpenTelemetryOptions();

// Configure HttpClient for Control Plane API
builder.Services.AddHttpClient<INodeRegistrationService, NodeRegistrationService>(client =>
{
    client.BaseAddress = new Uri(nodeRuntimeConfig.ControlPlaneUrl);
    client.DefaultRequestHeaders.Add("User-Agent", "Node.Runtime/1.0");
});

// Configure gRPC client for LeaseService
builder.Services.AddSingleton(sp =>
{
    var channel = GrpcChannel.ForAddress(nodeRuntimeConfig.ControlPlaneUrl);
    return new LeaseService.LeaseServiceClient(channel);
});

// Register services
// Use SandboxExecutor for process isolation (E2-T5)
builder.Services.AddSingleton<IAgentExecutor, SandboxExecutorService>();
builder.Services.AddSingleton<ISandboxExecutor, SandboxExecutorService>();
builder.Services.AddSingleton<ILeasePullService, LeasePullService>();

// Add Metrics Service for observable gauges
builder.Services.AddSingleton<INodeMetricsService, NodeMetricsService>();

// Configure OpenTelemetry
var resourceBuilder = ResourceBuilder.CreateDefault()
    .AddService(otelConfig.ServiceName, serviceVersion: otelConfig.ServiceVersion)
    .AddAttributes(new Dictionary<string, object>
    {
        ["node.id"] = nodeRuntimeConfig.NodeId,
        ["node.region"] = nodeRuntimeConfig.Metadata.GetValueOrDefault("Region", "unknown"),
        ["node.environment"] = nodeRuntimeConfig.Metadata.GetValueOrDefault("Environment", "unknown")
    });

if (otelConfig.Traces.Enabled || otelConfig.Metrics.Enabled)
{
    if (otelConfig.Traces.Enabled)
    {
        builder.Services.AddOpenTelemetry()
            .WithTracing(tracing =>
            {
                tracing
                    .SetResourceBuilder(resourceBuilder)
                    .AddHttpClientInstrumentation()
                    .AddGrpcClientInstrumentation()
                    .AddSource(Node.Runtime.Observability.TelemetryConfig.ActivitySource.Name)
                    .SetSampler(new TraceIdRatioBasedSampler(otelConfig.Traces.SamplingRatio));

                if (otelConfig.ConsoleExporter.Enabled)
                {
                    tracing.AddConsoleExporter();
                }

                if (!string.IsNullOrEmpty(otelConfig.OtlpExporter.Endpoint))
                {
                    tracing.AddOtlpExporter(options =>
                    {
                        options.Endpoint = new Uri(otelConfig.OtlpExporter.Endpoint);
                    });
                }
            });
    }

    if (otelConfig.Metrics.Enabled)
    {
        builder.Services.AddOpenTelemetry()
            .WithMetrics(metrics =>
            {
                metrics
                    .SetResourceBuilder(resourceBuilder)
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation()
                    .AddMeter(Node.Runtime.Observability.TelemetryConfig.Meter.Name);

                if (otelConfig.ConsoleExporter.Enabled)
                {
                    metrics.AddConsoleExporter();
                }

                if (!string.IsNullOrEmpty(otelConfig.OtlpExporter.Endpoint))
                {
                    metrics.AddOtlpExporter(options =>
                    {
                        options.Endpoint = new Uri(otelConfig.OtlpExporter.Endpoint);
                    });
                }
            });
    }
}

// Configure Logging
builder.Logging.AddOpenTelemetry(logging =>
{
    logging.SetResourceBuilder(resourceBuilder);
    logging.IncludeFormattedMessage = true;
    logging.IncludeScopes = true;

    if (otelConfig.ConsoleExporter.Enabled)
    {
        logging.AddConsoleExporter();
    }

    if (!string.IsNullOrEmpty(otelConfig.OtlpExporter.Endpoint))
    {
        logging.AddOtlpExporter(options =>
        {
            options.Endpoint = new Uri(otelConfig.OtlpExporter.Endpoint);
        });
    }
});

// Add the Worker as a hosted service
builder.Services.AddHostedService<Worker>();

var host = builder.Build();

var logger = host.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("Node Runtime starting with NodeId: {NodeId}", nodeRuntimeConfig.NodeId);

// Initialize observable gauges for metrics
if (otelConfig.Metrics.Enabled)
{
    var metricsService = host.Services.GetRequiredService<INodeMetricsService>();

    Node.Runtime.Observability.TelemetryConfig.ActiveLeasesGauge = 
        Node.Runtime.Observability.TelemetryConfig.Meter.CreateObservableGauge(
            "active_leases",
            () => metricsService.GetActiveLeases(),
            description: "Current number of active leases being processed");

    Node.Runtime.Observability.TelemetryConfig.AvailableSlotsGauge = 
        Node.Runtime.Observability.TelemetryConfig.Meter.CreateObservableGauge(
            "available_slots",
            () => metricsService.GetAvailableSlots(),
            description: "Current number of available slots for lease processing");

    logger.LogInformation("Observable gauges initialized for Node.Runtime metrics");
}

host.Run();

