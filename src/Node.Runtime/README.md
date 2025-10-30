# Node Runtime

The Node Runtime is a .NET Worker Service that executes business process agents on behalf of the Control Plane. It implements the worker node architecture described in the System Architecture Document.

## Overview

The Node Runtime provides:

- **Node Registration**: Registers with the Control Plane on startup
- **Heartbeat Management**: Sends periodic heartbeats to report node health and capacity
- **Lease Pull Loop**: Streams work assignments (leases) from the Control Plane via gRPC
- **Agent Execution**: Executes agents using Microsoft Agent Framework (MAF) SDK with budget enforcement
- **OpenTelemetry Integration**: Full observability with metrics, traces, and logs

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                       Node Runtime                           │
├─────────────────────────────────────────────────────────────┤
│                                                              │
│  ┌──────────────┐     ┌──────────────┐    ┌──────────────┐ │
│  │   Worker     │────▶│Registration  │    │  Heartbeat   │ │
│  │   Service    │     │   Service    │    │    Timer     │ │
│  └──────────────┘     └──────────────┘    └──────────────┘ │
│         │                    │                    │         │
│         │                    └────────────────────┘         │
│         │                                                   │
│         ▼                                                   │
│  ┌──────────────┐                                          │
│  │ Lease Pull   │◀────────────────────────────────────────│
│  │   Service    │         gRPC Stream (Pull)               │
│  └──────────────┘                                          │
│         │                                                   │
│         ▼                                                   │
│  ┌──────────────┐                                          │
│  │   Agent      │  Microsoft Agent Framework (MAF)         │
│  │  Executor    │  - Budget enforcement (tokens/time)      │
│  └──────────────┘  - Token estimation & cost tracking      │
└─────────────────────────────────────────────────────────────┘
         │                           ▲
         │  HTTP/gRPC               │ gRPC
         ▼                           │
┌─────────────────────────────────────────────────────────────┐
│                    Control Plane API                         │
│  - Node Registration (/v1/nodes:register)                   │
│  - Node Heartbeat (/v1/nodes/{id}:heartbeat)                │
│  - Lease Service (gRPC Pull, Ack, Complete, Fail)           │
└─────────────────────────────────────────────────────────────┘
```

## Configuration

Configuration is managed through `appsettings.json`:

```json
{
  "NodeRuntime": {
    "NodeId": "node-1",
    "ControlPlaneUrl": "http://localhost:5109",
    "MaxConcurrentLeases": 5,
    "HeartbeatIntervalSeconds": 30,
    "Capacity": {
      "Slots": 8,
      "Cpu": "4",
      "Memory": "8Gi"
    },
    "Metadata": {
      "Region": "us-east-1",
      "Environment": "development"
    }
  },
  "OpenTelemetry": {
    "ServiceName": "Node.Runtime",
    "ServiceVersion": "1.0.0",
    "OtlpExporter": {
      "Endpoint": "http://localhost:4317",
      "Protocol": "grpc"
    },
    "Traces": {
      "Enabled": true,
      "SamplingRatio": 1.0
    },
    "Metrics": {
      "Enabled": true
    }
  }
}
```

### Configuration Options

#### NodeRuntime

- **NodeId**: Unique identifier for this node instance (required)
- **ControlPlaneUrl**: Base URL of the Control Plane API (required)
- **MaxConcurrentLeases**: Maximum number of concurrent agent executions (default: 5)
- **HeartbeatIntervalSeconds**: Interval between heartbeat updates (default: 30)
- **Capacity**: Node resource capacity
  - **Slots**: Total execution slots available (default: 8)
  - **Cpu**: CPU allocation (e.g., "4" cores)
  - **Memory**: Memory allocation (e.g., "8Gi")
- **Metadata**: Custom metadata for placement constraints
  - **Region**: Geographic region (e.g., "us-east-1")
  - **Environment**: Environment name (e.g., "production", "staging", "development")

#### AgentRuntime

- **DefaultModel**: Default model to use for agent execution (default: "gpt-4")
- **DefaultTemperature**: Default temperature for model inference (default: 0.7)
- **MaxTokens**: Maximum tokens allowed per agent execution (default: 4000)
- **MaxDurationSeconds**: Maximum duration in seconds for agent execution (default: 60)

#### OpenTelemetry

- **ServiceName**: Service name for telemetry (default: "Node.Runtime")
- **ServiceVersion**: Service version (default: "1.0.0")
- **OtlpExporter**: OTLP exporter configuration
  - **Endpoint**: OTLP endpoint URL (default: "http://localhost:4317")
  - **Protocol**: Protocol type - "grpc" or "http/protobuf" (default: "grpc")
- **ConsoleExporter**: Console exporter configuration
  - **Enabled**: Enable console output for telemetry (default: false)
- **Traces**: Distributed tracing configuration
  - **Enabled**: Enable tracing (default: true)
  - **SamplingRatio**: Sampling ratio from 0.0 to 1.0 (default: 1.0)
- **Metrics**: Metrics configuration
  - **Enabled**: Enable metrics collection (default: true)

## Building

```bash
dotnet build
```

## Running

```bash
cd src/Node.Runtime
dotnet run
```

### Prerequisites

Before running the Node Runtime, ensure the following services are available:

1. **Control Plane API**: Must be running and accessible at the configured `ControlPlaneUrl`
2. **OpenTelemetry Collector** (optional): For telemetry export, running at the configured OTLP endpoint

## Testing

Run unit tests:

```bash
dotnet test tests/Node.Runtime.Tests/
```

Run all solution tests:

```bash
dotnet test
```

## Lifecycle

### Startup

1. **Registration**: Node registers with Control Plane via `POST /v1/nodes:register`
2. **Heartbeat Timer**: Starts periodic heartbeat updates
3. **Lease Pull Loop**: Initiates gRPC stream to receive work assignments

### Runtime

1. **Lease Reception**: Receives leases via gRPC `Pull` stream
2. **Lease Acknowledgment**: Sends `Ack` for each received lease
3. **Agent Execution**: Executes agent using MAF SDK with budget constraints
4. **Result Reporting**: Sends `Complete` or `Fail` via gRPC with timing and cost information

### Shutdown

1. **Stop Heartbeat**: Cancels heartbeat timer
2. **Stop Lease Pull**: Gracefully closes gRPC stream
3. **Cleanup**: Releases resources and stops all background services

## Services

### NodeRegistrationService

Handles HTTP communication with the Control Plane for node lifecycle:

- **RegisterNodeAsync**: Registers the node with capacity and metadata
- **SendHeartbeatAsync**: Sends periodic heartbeat with active runs and available slots

### AgentExecutorService

Executes agents using Microsoft Agent Framework SDK:

- **ExecuteAsync**: Executes an agent with the given input and budget constraints
- Applies token and duration limits from budget constraints
- Estimates token usage and cost
- Handles timeouts and errors gracefully
- Returns detailed execution results with timing and cost information

**Note**: The agent executor requires Azure AI Foundry or OpenAI credentials to be configured (task E3-T4). Until then, execution will return a NotImplementedException indicating the chat client needs configuration.

### LeasePullService

Manages gRPC communication for work assignment:

- **StartAsync**: Initiates gRPC `Pull` stream to receive leases
- **StopAsync**: Gracefully stops lease streaming
- **AcknowledgeLeaseAsync**: Sends acknowledgment for received leases
- **ProcessLeaseAsync**: Executes agent via AgentExecutorService and reports results

### Worker

Background service that orchestrates the node runtime lifecycle:

- Registers node on startup
- Manages heartbeat timer
- Coordinates lease pull service
- Handles graceful shutdown

## gRPC Contract

The Node Runtime uses the `LeaseService` gRPC contract defined in `lease_service.proto`:

### Pull (Server Streaming)

```protobuf
rpc Pull(PullRequest) returns (stream Lease);
```

Streams work assignments from the Control Plane to the node.

### Ack (Unary)

```protobuf
rpc Ack(AckRequest) returns (AckResponse);
```

Acknowledges receipt of a lease.

### Complete (Unary)

```protobuf
rpc Complete(CompleteRequest) returns (CompleteResponse);
```

Reports successful completion of a run with timing and cost information.

### Fail (Unary)

```protobuf
rpc Fail(FailRequest) returns (FailResponse);
```

Reports failure of a run with error details and retry information.

## Observability

The Node Runtime includes comprehensive OpenTelemetry instrumentation:

### Traces

- HTTP client calls to Control Plane
- gRPC calls to LeaseService
- Custom spans for agent execution (to be implemented)

### Metrics

- HTTP client metrics
- .NET runtime metrics (GC, thread pool, etc.)
- Custom metrics for lease processing (to be implemented)

### Logs

All logs are structured and include:

- Node ID
- Lease ID and Run ID (when applicable)
- Trace context for correlation

## Microsoft Agent Framework Integration

The Node Runtime integrates with Microsoft Agent Framework (MAF) SDK to execute business process agents (E2-T2).

### Components

**AgentExecutorService** (`IAgentExecutor`)
- Creates and executes agents using MAF SDK
- Enforces budget constraints (token limits, execution timeouts)
- Tracks token usage and estimates costs
- Returns detailed execution results

**AgentSpec**
- Defines agent configuration including ID, version, name, and instructions
- Specifies model profile and budget constraints
- Passed from Control Plane via gRPC lease specification

**Budget Enforcement**
- **Token Limits**: Configurable maximum tokens per execution (default: 4000)
- **Time Limits**: Configurable maximum duration (default: 60 seconds)
- **Cost Tracking**: Estimates USD cost based on token usage

### Configuration

Agent runtime behavior is configured via `AgentRuntime` section in `appsettings.json`:

```json
{
  "AgentRuntime": {
    "DefaultModel": "gpt-4",
    "DefaultTemperature": 0.7,
    "MaxTokens": 4000,
    "MaxDurationSeconds": 60
  }
}
```

### Dependencies

The following MAF SDK packages are included:
- `Microsoft.Agents.AI` (v1.0.0-preview.251028.1)
- `Microsoft.Agents.AI.AzureAI` (v1.0.0-preview.251028.1)
- `Microsoft.Agents.AI.OpenAI` (v1.0.0-preview.251028.1)

### Azure AI Foundry Integration

**Note**: Actual agent execution requires Azure AI Foundry or OpenAI API credentials. This will be configured in **E3-T4 (Azure AI Foundry integration)**. 

Until credentials are configured, the agent executor will return a `NotImplementedException` with a message indicating the chat client needs to be configured. The integration is ready to execute agents once the model provider is set up.

## Next Steps

Node Runtime core functionality is implemented (E2-T1, E2-T2). Upcoming tasks will enhance it:

- **E3-T4**: Configure Azure AI Foundry credentials for actual LLM execution
- **E2-T3**: Enhanced node registration with full lifecycle management
- **E2-T4**: Complete lease pull loop with full error handling
- **E2-T5**: Sandbox process model with budget enforcement
- **E2-T9**: Node telemetry with custom metrics
- **E2-T10**: Secure mTLS communication

## Development

### Project Structure

```
src/Node.Runtime/
├── Configuration/          # Configuration options classes
│   ├── NodeRuntimeOptions.cs
│   ├── AgentRuntimeOptions.cs
│   └── OpenTelemetryOptions.cs
├── Services/              # Core services
│   ├── NodeRegistrationService.cs
│   ├── AgentExecutorService.cs
│   └── LeasePullService.cs
├── Worker.cs              # Background worker service
├── Program.cs             # Entry point and DI setup
└── appsettings.json       # Configuration

tests/Node.Runtime.Tests/
└── Services/              # Unit tests
    ├── NodeRegistrationServiceTests.cs
    └── AgentExecutorServiceTests.cs
```

### Adding New Services

1. Create service interface and implementation in `Services/`
2. Register service in `Program.cs`
3. Add unit tests in `tests/Node.Runtime.Tests/Services/`

### Debugging

Set `Logging:LogLevel:Grpc` to `Debug` in `appsettings.json` to see detailed gRPC communication logs.

## License

See the main repository README for license information.
