using Node.Runtime;
using Node.Runtime.Configuration;
using Node.Runtime.Services;
using ControlPlane.Api.Grpc;
using Grpc.Net.Client;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Metrics;
using OpenTelemetry.Logs;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

var builder = Host.CreateApplicationBuilder(args);

// Configure options
builder.Services.Configure<NodeRuntimeOptions>(builder.Configuration.GetSection("NodeRuntime"));
builder.Services.Configure<AgentRuntimeOptions>(builder.Configuration.GetSection("AgentRuntime"));
builder.Services.Configure<OpenTelemetryOptions>(builder.Configuration.GetSection("OpenTelemetry"));
builder.Services.Configure<MTlsOptions>(builder.Configuration.GetSection("MTls"));

// Get configuration values for service setup
var nodeRuntimeConfig = builder.Configuration.GetSection("NodeRuntime").Get<NodeRuntimeOptions>() ?? new NodeRuntimeOptions();
var otelConfig = builder.Configuration.GetSection("OpenTelemetry").Get<OpenTelemetryOptions>() ?? new OpenTelemetryOptions();
var mtlsConfig = builder.Configuration.GetSection("MTls").Get<MTlsOptions>() ?? new MTlsOptions();

// Configure HttpClient for Control Plane API
builder.Services.AddHttpClient<INodeRegistrationService, NodeRegistrationService>(client =>
{
    client.BaseAddress = new Uri(nodeRuntimeConfig.ControlPlaneUrl);
    client.DefaultRequestHeaders.Add("User-Agent", "Node.Runtime/1.0");
});

// Configure gRPC client for LeaseService with mTLS support
builder.Services.AddSingleton(sp =>
{
    var logger = sp.GetRequiredService<ILogger<Program>>();
    
    GrpcChannel channel;
    
    if (mtlsConfig.Enabled)
    {
        logger.LogInformation("mTLS is enabled for gRPC client connections");

        // Validate required certificate paths
        if (string.IsNullOrWhiteSpace(mtlsConfig.ClientCertificatePath))
        {
            throw new InvalidOperationException("MTls:ClientCertificatePath is required when mTLS is enabled");
        }

        if (string.IsNullOrWhiteSpace(mtlsConfig.ClientKeyPath))
        {
            throw new InvalidOperationException("MTls:ClientKeyPath is required when mTLS is enabled");
        }

        // Load client certificate
        X509Certificate2 clientCertificate;
        try
        {
            var certPem = File.ReadAllText(mtlsConfig.ClientCertificatePath);
            var keyPem = File.ReadAllText(mtlsConfig.ClientKeyPath);
            clientCertificate = X509Certificate2.CreateFromPem(certPem, keyPem);
            logger.LogInformation("Loaded client certificate from {CertPath}", mtlsConfig.ClientCertificatePath);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to load client certificate from {mtlsConfig.ClientCertificatePath}", ex);
        }

        // Load server CA certificate if provided
        X509Certificate2? serverCaCertificate = null;
        if (!string.IsNullOrWhiteSpace(mtlsConfig.ServerCaCertificatePath))
        {
            try
            {
                var caPem = File.ReadAllText(mtlsConfig.ServerCaCertificatePath);
                serverCaCertificate = X509Certificate2.CreateFromPem(caPem);
                logger.LogInformation("Loaded server CA certificate from {CertPath}", mtlsConfig.ServerCaCertificatePath);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to load server CA certificate from {mtlsConfig.ServerCaCertificatePath}", ex);
            }
        }

        // Create HTTP handler with client certificate
        var httpHandler = new HttpClientHandler();
        httpHandler.ClientCertificates.Add(clientCertificate);

        // Configure server certificate validation
        if (serverCaCertificate != null)
        {
            httpHandler.ServerCertificateCustomValidationCallback = (message, certificate, chain, sslPolicyErrors) =>
            {
                if (certificate == null)
                {
                    logger.LogWarning("Server certificate validation failed: No certificate provided");
                    return false;
                }

                logger.LogDebug("Validating server certificate: Subject={Subject}, Issuer={Issuer}",
                    certificate.Subject, certificate.Issuer);

                // Check for basic SSL policy errors if chain validation is enabled
                if (mtlsConfig.ValidateCertificateChain && sslPolicyErrors != SslPolicyErrors.None)
                {
                    // Allow RemoteCertificateChainErrors as we'll validate against custom CA
                    if (sslPolicyErrors != SslPolicyErrors.RemoteCertificateChainErrors)
                    {
                        logger.LogWarning("Server certificate validation failed: SSL policy errors={Errors}", sslPolicyErrors);
                        return false;
                    }
                }

                // Build chain with custom CA
                using var customChain = new X509Chain();
                customChain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
                customChain.ChainPolicy.VerificationFlags = X509VerificationFlags.AllowUnknownCertificateAuthority;
                customChain.ChainPolicy.ExtraStore.Add(serverCaCertificate);

                var serverCert = new X509Certificate2(certificate);
                var chainBuilt = customChain.Build(serverCert);

                if (!chainBuilt)
                {
                    logger.LogWarning("Server certificate validation failed: Chain validation failed");
                    return false;
                }

                // Check if the CA is in the chain
                var isSignedByCA = false;
                foreach (var chainElement in customChain.ChainElements)
                {
                    if (chainElement.Certificate.Thumbprint == serverCaCertificate.Thumbprint)
                    {
                        isSignedByCA = true;
                        break;
                    }
                }

                if (!isSignedByCA)
                {
                    logger.LogWarning("Server certificate validation failed: Certificate not signed by trusted CA");
                    return false;
                }

                // Validate expected subject name if specified
                if (!string.IsNullOrWhiteSpace(mtlsConfig.ExpectedServerCertificateSubject))
                {
                    var subjectCN = certificate.GetNameInfo(X509NameType.SimpleName, false);
                    if (subjectCN != mtlsConfig.ExpectedServerCertificateSubject)
                    {
                        logger.LogWarning(
                            "Server certificate validation failed: Expected subject '{Expected}' but got '{Actual}'",
                            mtlsConfig.ExpectedServerCertificateSubject, subjectCN);
                        return false;
                    }
                }

                logger.LogInformation("Server certificate validated successfully: Subject={Subject}", certificate.Subject);
                return true;
            };
        }

        // Create gRPC channel with mTLS
        channel = GrpcChannel.ForAddress(nodeRuntimeConfig.ControlPlaneUrl, new GrpcChannelOptions
        {
            HttpHandler = httpHandler
        });
    }
    else
    {
        // Create standard gRPC channel without mTLS
        channel = GrpcChannel.ForAddress(nodeRuntimeConfig.ControlPlaneUrl);
    }

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

