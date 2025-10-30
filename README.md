# Business Process Agents - Control Plane API

This is the Control Plane API for the Business Process Agents MVP project. It provides REST endpoints for managing Agents, Nodes, and Runs.

## Project Structure

```
├── src/
│   └── ControlPlane.Api/          # Main API project
│       ├── AgentRuntime/           # Agent runtime and tool registry (E1-T2)
│       ├── Data/                   # Database entities and migrations (E1-T3)
│       ├── Models/                 # Data models
│       ├── Services/               # Business logic and storage
│       └── Program.cs              # API endpoints and configuration
├── tests/
│   └── ControlPlane.Api.Tests/    # Integration tests
└── BusinessProcessAgents.sln      # Solution file
```

## Prerequisites

- .NET 9.0 SDK or later
- PostgreSQL 14 or later (for production use)
- Redis 6.0 or later (for lease and lock management)
- NATS Server 2.10+ with JetStream enabled (for event streaming)

## Database Setup

### PostgreSQL Database

The application uses PostgreSQL for persistent storage. The database schema includes:

- **agents**: Agent definitions
- **agent_versions**: Version history of agents
- **deployments**: Agent deployments with replicas and placement
- **nodes**: Worker nodes
- **runs**: Agent execution runs

### Connection Configuration

Update `appsettings.json` to configure the PostgreSQL connection:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=bpa;Username=postgres;Password=postgres",
    "Redis": "localhost:6379",
    "Nats": "nats://localhost:4222"
  }
}
```

> **Security Note**: For production deployments, use strong passwords and store credentials securely using environment variables or Azure Key Vault. Never commit production credentials to source control.

### Running Migrations

To create or update the database schema, use Entity Framework Core migrations:

```bash
# Navigate to the API project
cd src/ControlPlane.Api

# Apply migrations to create/update the database
dotnet ef database update
```

To create new migrations (for developers):

```bash
dotnet ef migrations add <MigrationName> --output-dir Data/Migrations
```

### Development Mode

For development and testing, you can use in-memory stores by setting `UseInMemoryStores` to `true` in `appsettings.json`:

```json
{
  "UseInMemoryStores": true
}
```

This bypasses PostgreSQL and uses in-memory storage (data is lost on restart).

## Building

```bash
dotnet build
```

## Running

```bash
cd src/ControlPlane.Api
dotnet run
```

The API will be available at `http://localhost:5109` (or the port specified in `launchSettings.json`).

## Testing

Run all tests:
```bash
dotnet test
```

Run tests with verbose output:
```bash
dotnet test --verbosity normal
```

## API Endpoints

### Agents

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/v1/agents` | List all agents |
| GET | `/v1/agents/{agentId}` | Get a specific agent |
| POST | `/v1/agents` | Create a new agent |
| PUT | `/v1/agents/{agentId}` | Update an agent |
| DELETE | `/v1/agents/{agentId}` | Delete an agent |

**Create Agent Request:**
```json
{
  "name": "Invoice Classifier",
  "instructions": "Classify invoices by vendor and route appropriately",
  "modelProfile": {
    "model": "gpt-4",
    "temperature": 0.7
  }
}
```

### Nodes

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/v1/nodes` | List all nodes |
| GET | `/v1/nodes/{nodeId}` | Get a specific node |
| POST | `/v1/nodes:register` | Register a new node |
| POST | `/v1/nodes/{nodeId}:heartbeat` | Update node heartbeat |
| DELETE | `/v1/nodes/{nodeId}` | Delete a node |

**Register Node Request:**
```json
{
  "nodeId": "node-1",
  "metadata": {
    "region": "us-east-1",
    "environment": "production"
  },
  "capacity": {
    "slots": 8,
    "cpu": "4",
    "memory": "8Gi"
  }
}
```

**Heartbeat Request:**
```json
{
  "status": {
    "state": "active",
    "activeRuns": 2,
    "availableSlots": 6
  }
}
```

### Runs

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/v1/runs` | List all runs |
| GET | `/v1/runs/{runId}` | Get a specific run |
| POST | `/v1/runs/{runId}:complete` | Mark a run as completed |
| POST | `/v1/runs/{runId}:fail` | Mark a run as failed |
| POST | `/v1/runs/{runId}:cancel` | Cancel a run |

**Complete Run Request:**
```json
{
  "result": {
    "classification": "vendor-a",
    "confidence": 0.95
  },
  "timings": {
    "duration": 1500
  },
  "costs": {
    "tokens": 100,
    "usd": 0.002
  }
}
```

**Fail Run Request:**
```json
{
  "errorMessage": "Failed to classify invoice",
  "errorDetails": "Model timeout",
  "timings": {
    "duration": 500
  }
}
```

**Cancel Run Request:**
```json
{
  "reason": "User requested cancellation"
}
```

## Architecture

This is an ASP.NET Core Minimal API implementation using:
- **Models**: Define the data structures for Agents, Nodes, and Runs
- **Services**: In-memory storage implementations (will be replaced with PostgreSQL in future tasks)
- **Endpoints**: REST API endpoints following the design specified in the System Architecture Document

## Current Implementation

This implementation provides:

- ✅ Full CRUD operations for Agents
- ✅ Node registration and heartbeat endpoints
- ✅ Run state management endpoints (complete, fail, cancel)
- ✅ **Microsoft Agent Framework SDK integration** (E1-T2)
- ✅ **Agent runtime base classes** for executing agents
- ✅ **Tool registry** for managing agent tools
- ✅ Configuration support for agent runtime options
- ✅ **PostgreSQL database schema** (E1-T3)
- ✅ **Entity Framework Core migrations**
- ✅ **Database-backed store implementations**
- ✅ Configurable in-memory or PostgreSQL storage
- ✅ **Redis integration for leases and locks** (E1-T4)
- ✅ **Lease store with TTL expiry** for preventing double-assignment of runs
- ✅ **Lock store with TTL expiry** for distributed coordination
- ✅ **NATS JetStream event streaming** (E1-T5)
- ✅ **gRPC LeaseService** for node communication (E1-T6)
- ✅ **Scheduler service with least-loaded strategy** (E1-T7)
- ✅ **OpenTelemetry instrumentation** with metrics, tracing, and logging (E1-T8)
- ✅ Comprehensive integration tests (121 tests)

### Database Schema

The PostgreSQL schema implements the data model defined in the System Architecture Document (SAD):

| Table | Description | Key Fields |
|-------|-------------|-----------|
| `agents` | Agent definitions | `agent_id` (PK), `name`, `instructions`, `model_profile` (JSONB) |
| `agent_versions` | Version history | `version_id` (PK), `agent_id` (FK), `version`, `spec` (JSONB) |
| `deployments` | Agent deployments | `dep_id` (PK), `agent_id` (FK), `version`, `env`, `target` (JSONB), `status` (JSONB) |
| `nodes` | Worker nodes | `node_id` (PK), `metadata` (JSONB), `capacity` (JSONB), `status` (JSONB), `heartbeat_at` |
| `runs` | Agent execution runs | `run_id` (PK), `agent_id` (FK), `version`, `dep_id` (FK), `node_id` (FK), `status`, `timings` (JSONB), `costs` (JSONB), `trace_id` |

### Storage Implementations

The application supports two storage backends:

1. **PostgreSQL Stores** (Production): `PostgresAgentStore`, `PostgresNodeStore`, `PostgresRunStore`
   - Persistent storage with full ACID guarantees
   - Configured via connection string in `appsettings.json`
   
2. **In-Memory Stores** (Development/Testing): `InMemoryAgentStore`, `InMemoryNodeStore`, `InMemoryRunStore`
   - Fast, no external dependencies
   - Data lost on restart
   - Enabled via `UseInMemoryStores: true` configuration

### Redis Lease and Lock Management

The application uses Redis for distributed leases and locks with TTL expiry (E1-T4):

1. **Lease Store** (`ILeaseStore`, `RedisLeaseStore`)
   - Prevents double-assignment of runs to nodes
   - Atomic lease acquisition using Redis SET NX (set if not exists)
   - Automatic expiration via TTL
   - Supports lease extension for heartbeat/keepalive scenarios
   - Used by the scheduler for run placement

2. **Lock Store** (`ILockStore`, `RedisLockStore`)
   - Distributed locks for coordinating operations across multiple control plane instances
   - Owner-based lock management (only the owner can release/extend)
   - Atomic operations using Lua scripts
   - Automatic expiration via TTL
   - Used for critical sections requiring coordination

**Redis Configuration:**
```json
{
  "ConnectionStrings": {
    "Redis": "localhost:6379"
  }
}
```

For production, use Redis Sentinel or Redis Cluster for high availability.

### NATS Event Streaming

The application uses NATS JetStream for internal event streaming to support event-driven architecture and decoupling between components.

#### JetStream Streams

The `BPA_EVENTS` stream is automatically provisioned on startup with the following subjects:
- `bpa.events.run.*` - Run state change events
- `bpa.events.node.*` - Node lifecycle events  
- `bpa.events.agent.*` - Agent deployment events

#### Event Types

The following system events are published to NATS:

| Event Type | Subject | Description |
|------------|---------|-------------|
| `RunStateChangedEvent` | `bpa.events.run.state-changed` | Published when a run transitions states |
| `NodeRegisteredEvent` | `bpa.events.node.registered` | Published when a node registers |
| `NodeHeartbeatEvent` | `bpa.events.node.heartbeat` | Published on node heartbeat |
| `NodeDisconnectedEvent` | `bpa.events.node.disconnected` | Published when a node disconnects |
| `AgentDeployedEvent` | `bpa.events.agent.deployed` | Published when an agent is deployed |

#### Testing NATS

A test endpoint is available to verify JetStream setup:

```bash
curl -X POST http://localhost:5109/v1/events:test
```

This publishes a sample `RunStateChangedEvent` to verify the NATS connection and stream configuration.

#### NATS Configuration

```json
{
  "ConnectionStrings": {
    "Nats": "nats://localhost:4222"
  }
}
```

For production, use NATS clustering with JetStream for high availability and durability.

#### Running NATS Locally

```bash
# Run NATS with JetStream enabled
docker run -p 4222:4222 -p 8222:8222 nats:latest --jetstream

# Or using Docker Compose (add to your docker-compose.yml)
services:
  nats:
    image: nats:latest
    ports:
      - "4222:4222"
      - "8222:8222"
    command: "--jetstream"
```

**Note:** If NATS is not available on startup, the application will log a warning and continue without event publishing.
   - Enabled via `UseInMemoryStores: true` configuration

### Microsoft Agent Framework Integration

The project now includes Microsoft Agent Framework SDK integration with the following components:

#### Agent Runtime Service (`IAgentRuntime`)
- Creates agent instances from agent definitions
- Executes agents with input messages
- Validates agent configurations
- Integrates with tool registry for agent capabilities

#### Tool Registry (`IToolRegistry`)
- Manages tools available to agents
- Associates tools with specific agents
- Supports function, API, and connector tools
- In-memory implementation for MVP

#### Configuration
Agent runtime can be configured via `appsettings.json`:
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

#### NuGet Packages Added
- `Microsoft.Agents.AI` (v1.0.0-preview.251028.1)
- `Microsoft.Agents.AI.AzureAI` (v1.0.0-preview.251028.1)
- `Microsoft.Agents.AI.OpenAI` (v1.0.0-preview.251028.1)
- `StackExchange.Redis` (v2.8.16)
- `NATS.Client.Core` (v2.5.3)
- `NATS.Client.JetStream` (v2.5.3)

**Note:** Actual agent execution requires Azure AI Foundry or OpenAI credentials, which will be configured in task E3-T4 (Azure AI Foundry integration).

### gRPC LeaseService

The application includes a gRPC service for node communication, providing lease management for distributed run execution (E1-T6):

#### Service Definition

The `LeaseService` provides four RPC methods:

1. **Pull** - Server-streaming RPC for nodes to pull work leases
   - Nodes request available runs to execute
   - Server streams leases as they become available
   - Each lease includes run specification, deadline, and trace ID

2. **Ack** - Unary RPC to acknowledge lease receipt
   - Nodes acknowledge they've received a lease
   - Used for telemetry and diagnostics

3. **Complete** - Unary RPC to mark a run as completed
   - Nodes report successful run completion
   - Includes timing information and costs
   - Automatically releases the lease

4. **Fail** - Unary RPC to mark a run as failed
   - Nodes report run failures
   - Includes error details and retry information
   - Supports automatic retry logic (max 3 attempts)

#### Proto Contract

The service contract is defined in `Protos/lease_service.proto`. Key message types:

- `Lease` - Work assignment with run spec, deadline, and trace ID
- `RunSpec` - Execution specification including agent ID, version, and budget constraints
- `BudgetConstraints` - Max tokens and duration limits
- `TimingInfo` - Execution timing metrics
- `CostInfo` - Token usage and cost tracking

#### gRPC Endpoint

The gRPC service is available at the same base address as the HTTP API:
- **Development**: `http://localhost:5109` (or configured port)
- **Proto namespace**: `ControlPlane.Api.Grpc`

**Example client connection:**
```csharp
using var channel = GrpcChannel.ForAddress("http://localhost:5109");
var client = new LeaseService.LeaseServiceClient(channel);

// Pull leases
using var call = client.Pull(new PullRequest 
{ 
    NodeId = "node-1", 
    MaxLeases = 5 
});

await foreach (var lease in call.ResponseStream.ReadAllAsync())
{
    // Process lease
    Console.WriteLine($"Received lease {lease.LeaseId} for run {lease.RunId}");
}
```

**NuGet Packages Added:**
- `Grpc.AspNetCore` (v2.70.0) - Server-side gRPC support
- `Grpc.Net.Client` (v2.70.0) - Client-side gRPC support (for testing)

### Scheduler Service

The application includes a sophisticated scheduler service that implements a **least-loaded scheduling strategy with region constraints** (E1-T7), as specified in the System Architecture Document.

#### Scheduling Strategy

The `LeastLoadedScheduler` assigns runs to worker nodes based on:

1. **Load Balancing**: Selects the node with the lowest load percentage (active runs / total slots)
2. **Capacity Awareness**: Only considers nodes with available slots
3. **Region Constraints**: Respects placement requirements for geographic affinity
4. **Environment Constraints**: Supports environment-based placement (e.g., production vs. staging)
5. **Tie-Breaking**: When load is equal, prefers nodes with more available slots

#### Placement Constraints

Deployments can specify placement constraints that the scheduler honors:

```json
{
  "placement": {
    "region": "us-east-1",
    "environment": "production"
  }
}
```

**Supported constraint types:**
- `region` - Single region (string) or multiple regions (array)
- `environment` - Target environment (e.g., "production", "staging", "dev")

#### Scheduler Interface

The `IScheduler` interface provides:

```csharp
// Schedule a run to the most appropriate node
Task<string?> ScheduleRunAsync(
    Run run, 
    Dictionary<string, object>? placementConstraints = null, 
    CancellationToken cancellationToken = default);

// Get current load information for all nodes
Task<Dictionary<string, NodeLoadInfo>> GetNodeLoadAsync(
    CancellationToken cancellationToken = default);
```

#### Integration with LeaseService

The scheduler is automatically used by the `LeaseService` when nodes request work:

1. Node requests leases via gRPC `Pull` stream
2. Scheduler evaluates pending runs and determines best node for each
3. Only runs scheduled to the requesting node are streamed back
4. Lease is acquired atomically via Redis to prevent double-assignment

**Configuration:**

The scheduler is registered as a singleton service and automatically integrated:

```csharp
builder.Services.AddSingleton<IScheduler, LeastLoadedScheduler>();
```

#### Example Scenarios

**Basic Load Balancing:**
- Node A: 75% load (3/4 slots used)
- Node B: 25% load (1/4 slots used)
- New run → Scheduled to Node B

**Region Constraint:**
- Node A: us-east-1, 25% load
- Node B: us-west-1, 10% load
- Run requires region: us-east-1
- New run → Scheduled to Node A (only eligible node)

**Multiple Regions:**
- Run allows regions: ["us-east-1", "eu-west-1"]
- Only nodes in these regions are considered
- Least-loaded eligible node is selected

### OpenTelemetry Observability

The application includes comprehensive OpenTelemetry (OTel) instrumentation for end-to-end observability (E1-T8), providing metrics, distributed tracing, and structured logging.

#### Configuration

OpenTelemetry is configured in `appsettings.json`:

```json
{
  "OpenTelemetry": {
    "ServiceName": "ControlPlane.Api",
    "ServiceVersion": "1.0.0",
    "OtlpExporter": {
      "Endpoint": "http://localhost:4317",
      "Protocol": "grpc"
    },
    "ConsoleExporter": {
      "Enabled": false
    },
    "Traces": {
      "Enabled": true,
      "SamplingRatio": 1.0
    },
    "Metrics": {
      "Enabled": true,
      "ExportIntervalMilliseconds": 60000
    },
    "Logs": {
      "Enabled": true,
      "IncludeFormattedMessage": true,
      "IncludeScopes": true
    }
  }
}
```

#### Metrics

The following custom metrics are automatically collected:

**Counters:**
- `runs_started_total` - Total number of runs started
- `runs_completed_total` - Total number of runs completed successfully
- `runs_failed_total` - Total number of runs failed
- `runs_cancelled_total` - Total number of runs cancelled
- `nodes_registered_total` - Total number of nodes registered
- `nodes_disconnected_total` - Total number of nodes disconnected
- `leases_granted_total` - Total number of leases granted to nodes
- `leases_released_total` - Total number of leases released
- `scheduling_attempts_total` - Total number of scheduling attempts
- `scheduling_failures_total` - Total number of scheduling failures

**Histograms:**
- `run_duration_ms` - Duration of run execution in milliseconds
- `scheduling_duration_ms` - Duration of scheduling operations in milliseconds
- `run_tokens` - Number of tokens used per run
- `run_cost_usd` - Cost of run execution in USD

**Automatic Instrumentation:**
- ASP.NET Core HTTP requests and responses
- gRPC client calls
- HTTP client calls
- .NET runtime metrics (GC, thread pool, etc.)

#### Distributed Tracing

Distributed traces are automatically created for:
- **Run operations**: `RunStore.CreateRun`, `RunStore.CompleteRun`, `RunStore.FailRun`, `RunStore.CancelRun`
- **Node operations**: `NodeStore.RegisterNode`, `NodeStore.DeleteNode`
- **Scheduling**: `Scheduler.ScheduleRun` with load balancing details
- **Lease management**: `LeaseService.Pull` with lease grant tracking
- **HTTP/gRPC requests**: Automatic correlation via trace context propagation

Each trace includes relevant tags (e.g., `run.id`, `agent.id`, `node.id`) and correlates with logs via `trace_id`.

#### Logging

Structured logs are enhanced with OpenTelemetry context:
- **Trace correlation**: Logs include `trace_id` and `span_id` for correlation with traces
- **Formatted messages**: Human-readable log messages
- **Scopes**: Log scopes are included for better context
- **JSON format**: Logs are structured for easy parsing and filtering

#### Exporters

**OTLP Exporter (Production):**
- Sends telemetry to OpenTelemetry Collector at `http://localhost:4317`
- Compatible with Prometheus, Tempo, Loki, and other backends
- Uses gRPC protocol for efficient data transmission

**Console Exporter (Development):**
- Can be enabled for local debugging: `"ConsoleExporter": { "Enabled": true }`
- Outputs metrics, traces, and logs to console for immediate visibility

#### Integration with Observability Stack

The Control Plane integrates with the following observability stack (as defined in the System Architecture Document):

- **Prometheus**: Metrics collection and storage
- **Tempo/Jaeger**: Distributed tracing backend
- **Loki**: Log aggregation and querying
- **Grafana**: Unified dashboards for metrics, traces, and logs

**Example trace flow:**
```
receive → plan → lease → think → http.out → complete
```

Each step is instrumented with activities that record timing, attributes, and correlation IDs.

#### Running with OTel Collector

**Local Development (Docker Compose):**
```yaml
services:
  otel-collector:
    image: otel/opentelemetry-collector-contrib:latest
    ports:
      - "4317:4317"  # OTLP gRPC
      - "4318:4318"  # OTLP HTTP
    volumes:
      - ./otel-collector-config.yaml:/etc/otelcol/config.yaml
```

**Configuration Example:**
```yaml
receivers:
  otlp:
    protocols:
      grpc:
        endpoint: 0.0.0.0:4317

exporters:
  prometheus:
    endpoint: 0.0.0.0:8889
  logging:
    loglevel: debug

service:
  pipelines:
    metrics:
      receivers: [otlp]
      exporters: [prometheus, logging]
    traces:
      receivers: [otlp]
      exporters: [logging]
    logs:
      receivers: [otlp]
      exporters: [logging]
```

**NuGet Packages Added:**
- `OpenTelemetry.Exporter.Console` (v1.10.0)
- `OpenTelemetry.Exporter.OpenTelemetryProtocol` (v1.10.0)
- `OpenTelemetry.Extensions.Hosting` (v1.10.0)
- `OpenTelemetry.Instrumentation.AspNetCore` (v1.10.0)
- `OpenTelemetry.Instrumentation.GrpcNetClient` (v1.10.0-beta.1)
- `OpenTelemetry.Instrumentation.Http` (v1.10.0)
- `OpenTelemetry.Instrumentation.Runtime` (v1.10.0)
- `OpenTelemetry.Instrumentation.StackExchangeRedis` (v1.10.0-beta.1)

## Next Steps

See `tasks.yaml` for the full project roadmap. The next tasks include:
- ✅ **E1-T1**: API skeleton (Complete)
- ✅ **E1-T2**: Integrate Microsoft Agent Framework SDK (Complete)
- ✅ **E1-T3**: Database setup (Complete)
- ✅ **E1-T4**: Add Redis for lease and lock management (Complete)
- ✅ **E1-T5**: Set up NATS for event streaming (Complete)
- ✅ **E1-T6**: Implement gRPC service for node communication (Complete)
- ✅ **E1-T7**: Scheduler service (Complete)
- ✅ **E1-T8**: OpenTelemetry wiring (Complete)
- ✅ **E1-T9**: Authentication setup (Complete)

## Authentication

The Control Plane API supports OIDC authentication with JWT Bearer tokens. Authentication is configurable and disabled by default for ease of development.

**For detailed authentication setup and configuration, see [AUTHENTICATION.md](./AUTHENTICATION.md).**

**Quick Start:**
- Keycloak for development (docker-compose.dev.yml included)
- Microsoft Entra ID supported for production
- Configure via `appsettings.json` Authentication section
- Enable/disable authentication without code changes

## OpenAPI/Swagger

In development mode, OpenAPI documentation is available at:
- `/openapi/v1.json` - OpenAPI specification

## Contributing

Follow the branching strategy defined in `.github/copilot-instructions.md`:
- Use feature branches: `feature/E1-T<number>-<description>`
- Keep branches short-lived (< 3 days of work)
- Create pull requests for all changes to `main`
