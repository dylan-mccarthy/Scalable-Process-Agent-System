# Business Process Agents - MVP Platform

[![CI Pipeline](https://github.com/dylan-mccarthy/Scalable-Process-Agent-System/actions/workflows/ci.yml/badge.svg)](https://github.com/dylan-mccarthy/Scalable-Process-Agent-System/actions/workflows/ci.yml)
[![Code Quality](https://github.com/dylan-mccarthy/Scalable-Process-Agent-System/actions/workflows/code-quality.yml/badge.svg)](https://github.com/dylan-mccarthy/Scalable-Process-Agent-System/actions/workflows/code-quality.yml)

This is the Business Process Agents MVP project, providing a complete platform for deploying and executing business process agents using the Microsoft Agent Framework.

## Project Structure

```
├── src/
│   ├── ControlPlane.Api/          # Control Plane API (E1)
│   │   ├── AgentRuntime/           # Agent runtime and tool registry (E1-T2)
│   │   ├── Data/                   # Database entities and migrations (E1-T3)
│   │   ├── Models/                 # Data models
│   │   ├── Services/               # Business logic and storage
│   │   ├── Grpc/                   # gRPC services (E1-T6)
│   │   └── Program.cs              # API endpoints and configuration
│   └── Node.Runtime/              # Worker Node Runtime (E2)
│       ├── Configuration/          # Configuration options
│       ├── Services/               # Core services
│       ├── Worker.cs               # Background worker service
│       └── Program.cs              # Entry point and DI setup
├── tests/
│   ├── ControlPlane.Api.Tests/    # Control Plane integration tests
│   └── Node.Runtime.Tests/        # Node Runtime unit tests
└── BusinessProcessAgents.sln      # Solution file
```

## Quick Start

### Option 1: Local Kubernetes with k3d (Recommended)

The fastest way to get a complete environment running locally:

```bash
# Clone the repository
git clone https://github.com/dylan-mccarthy/Scalable-Process-Agent-System.git
cd Scalable-Process-Agent-System

# Run the k3d setup script
./infra/scripts/setup-k3d.sh
```

This will create a local Kubernetes cluster and deploy all services:
- ✅ PostgreSQL, Redis, NATS
- ✅ Control Plane API
- ✅ Node Runtime (2 replicas)
- ✅ Admin UI

**Access Points:**
- Control Plane API: http://localhost:8080
- Admin UI: http://localhost:3000

> **Note**: The k3d setup does not include Azure AI Foundry. Configure Azure AI Foundry credentials for the Node Runtime to enable agent execution. See [Azure AI Foundry Configuration](#azure-ai-foundry-configuration) section below.

**Cleanup:**
```bash
./infra/scripts/cleanup-k3d.sh
```

**See:** [infra/scripts/README.md](infra/scripts/README.md) for detailed k3d documentation.

### Option 2: Docker Compose

Run all services with Docker Compose:

```bash
# Start all services
docker-compose up -d

# View logs
docker-compose logs -f

# Stop all services
docker-compose down
```

**Access Points:**
- Control Plane API: http://localhost:8080
- Admin UI: http://localhost:3000

> **Note**: Docker Compose does not include Azure AI Foundry. You must configure Azure AI Foundry credentials in `src/Node.Runtime/appsettings.json` or use environment variables. See [Azure AI Foundry Configuration](#azure-ai-foundry-configuration) section below.

### Option 3: Local Development (No Docker)

Build and run individual services for development:

```bash
# Clone the repository
git clone https://github.com/dylan-mccarthy/Scalable-Process-Agent-System.git
cd Scalable-Process-Agent-System

# Build the solution
dotnet build

# Run tests
dotnet test
```

#### Running Control Plane API

```bash
# Option A: In-memory mode (no external dependencies)
cd src/ControlPlane.Api
# Set UseInMemoryStores=true in appsettings.json
dotnet run

# Option B: Full mode with PostgreSQL, Redis, and NATS
# Start dependencies (requires Docker)
docker run -d -p 5432:5432 -e POSTGRES_PASSWORD=postgres postgres:14
docker run -d -p 6379:6379 redis:6
docker run -d -p 4222:4222 -p 8222:8222 nats:latest --jetstream

# Run migrations and start API
cd src/ControlPlane.Api
dotnet ef database update
dotnet run
```

The API will be available at `http://localhost:5109`.

#### Running Node Runtime

Before running the Node Runtime, **configure Azure AI Foundry** (required for agent execution):

```bash
# Option 1: Use user secrets (recommended for development)
cd src/Node.Runtime
dotnet user-secrets set "AgentRuntime:AzureAIFoundry:Endpoint" "https://your-resource.services.ai.azure.com/models"
dotnet user-secrets set "AgentRuntime:AzureAIFoundry:ApiKey" "your-api-key"
dotnet user-secrets set "AgentRuntime:AzureAIFoundry:DeploymentName" "gpt-4o-mini"

# Option 2: Use environment variables
export AgentRuntime__AzureAIFoundry__Endpoint="https://your-resource.services.ai.azure.com/models"
export AgentRuntime__AzureAIFoundry__ApiKey="your-api-key"
export AgentRuntime__AzureAIFoundry__DeploymentName="gpt-4o-mini"
```

Then start the Node Runtime:

```bash
cd src/Node.Runtime
dotnet run
```

The Node Runtime will:
1. Register with the Control Plane
2. Start sending heartbeats
3. Begin pulling leases for agent execution

## Prerequisites

- .NET 9.0 SDK or later
- PostgreSQL 14 or later (for production use)
- Redis 6.0 or later (for lease and lock management)
- NATS Server 2.10+ with JetStream enabled (for event streaming)
- **Azure AI Foundry** or **Azure OpenAI Service** (for LLM-powered agent execution)

## Azure AI Foundry Configuration

The platform uses **Azure AI Foundry** (or Azure OpenAI Service) to power LLM-based agent execution. You must configure Azure AI Foundry for agents to process requests using AI models like GPT-4.

### Quick Setup

1. **Create Azure AI Foundry Resource**:
   ```bash
   # Create resource group
   az group create --name rg-bpa-agents --location eastus
   
   # Create Azure AI Foundry resource
   az cognitiveservices account create \
     --name my-ai-foundry \
     --resource-group rg-bpa-agents \
     --kind AIServices \
     --sku S0 \
     --location eastus
   ```

2. **Deploy a Model**:
   - Navigate to your Azure AI Foundry resource in the Azure Portal
   - Go to "Deployments" → "Create new deployment"
   - Select model: `gpt-4o-mini` (recommended for cost-effective MVP)
   - Name: `gpt-4o-mini`
   - Note your endpoint: `https://your-resource.services.ai.azure.com/models`

3. **Configure Node Runtime** (edit `src/Node.Runtime/appsettings.json`):
   ```json
   {
     "AgentRuntime": {
       "DefaultModel": "gpt-4o-mini",
       "DefaultTemperature": 0.7,
       "MaxTokens": 4000,
       "MaxDurationSeconds": 60,
       "AzureAIFoundry": {
         "Endpoint": "https://your-resource.services.ai.azure.com/models",
         "DeploymentName": "gpt-4o-mini",
         "ApiKey": "your-api-key-here",
         "UseManagedIdentity": false
       }
     }
   }
   ```

   > **Security Best Practice**: Never commit API keys to source control. Use one of these approaches:
   > - **Development**: `dotnet user-secrets set "AgentRuntime:AzureAIFoundry:ApiKey" "your-key"`
   > - **Production**: Use Managed Identity (set `UseManagedIdentity: true`) or Azure Key Vault
   > - **Environment Variables**: `export AgentRuntime__AzureAIFoundry__ApiKey="your-key"`

### Supported Models

Azure AI Foundry supports various models for different use cases:

| Model Family | Model | Best For | Cost |
|-------------|-------|----------|------|
| **GPT-4 Optimized** | `gpt-4o` | Latest performance, multimodal | $$$ |
| | `gpt-4o-mini` | Cost-effective, fast, recommended for MVP | $ |
| **GPT-4** | `gpt-4` | Complex reasoning tasks | $$$$ |
| | `gpt-4-32k` | Extended context (32K tokens) | $$$$$ |
| **GPT-3.5** | `gpt-3.5-turbo` | Fast, cost-effective | $ |
| | `gpt-3.5-turbo-16k` | Extended context (16K tokens) | $$ |

### Configuration Options

| Setting | Required | Default | Description |
|---------|----------|---------|-------------|
| `Endpoint` | ✓ | - | Azure AI Foundry endpoint URL |
| `DeploymentName` | ✓ | - | Model deployment name in Azure |
| `ApiKey` | ✓* | - | API key for authentication |
| `UseManagedIdentity` | | `false` | Use Azure Managed Identity instead of API key |

\* Required if `UseManagedIdentity` is `false`

### Using Managed Identity (Recommended for Production)

Managed Identity eliminates the need for API keys:

```json
{
  "AgentRuntime": {
    "AzureAIFoundry": {
      "Endpoint": "https://your-resource.services.ai.azure.com/models",
      "DeploymentName": "gpt-4o-mini",
      "UseManagedIdentity": true
    }
  }
}
```

Grant your Node Runtime's managed identity access:

```bash
# Get Node Runtime's managed identity principal ID
PRINCIPAL_ID=$(az aks show --name my-aks --resource-group my-rg --query identityProfile.kubeletidentity.clientId -o tsv)

# Get Azure AI Foundry resource ID
AI_RESOURCE_ID=$(az cognitiveservices account show --name my-ai-foundry --resource-group rg-bpa-agents --query id -o tsv)

# Assign Cognitive Services User role
az role assignment create \
  --assignee $PRINCIPAL_ID \
  --role "Cognitive Services User" \
  --scope $AI_RESOURCE_ID
```

### Budget & Cost Management

Control costs by setting budget constraints in agent definitions:

```json
{
  "agentId": "invoice-classifier",
  "budget": {
    "maxTokens": 2000,
    "maxDurationSeconds": 30
  }
}
```

The platform automatically tracks token usage and costs for each run. Monitor in:
- Azure Portal: Cost analysis for Azure AI Foundry
- Application logs: Token usage per execution
- OpenTelemetry metrics: `run_tokens`, `run_cost_usd`

**For detailed Azure AI Foundry configuration, see [docs/AZURE_AI_FOUNDRY_INTEGRATION.md](docs/AZURE_AI_FOUNDRY_INTEGRATION.md).**

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

### Deployments

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/v1/deployments` | List all deployments |
| GET | `/v1/deployments/{depId}` | Get a specific deployment |
| GET | `/v1/agents/{agentId}/deployments` | Get deployments for an agent |
| POST | `/v1/deployments` | Create a new deployment |
| PUT | `/v1/deployments/{depId}` | Update deployment status |
| DELETE | `/v1/deployments/{depId}` | Delete a deployment |

**Create Deployment Request:**
```json
{
  "agentId": "agent-123",
  "version": "1.0.0",
  "env": "production",
  "target": {
    "replicas": 3,
    "placement": {
      "region": "us-east-1",
      "environment": "production"
    }
  }
}
```

**Update Deployment Status Request:**
```json
{
  "status": {
    "state": "active",
    "readyReplicas": 3,
    "message": "All replicas ready"
  }
}
```

## Architecture

This is an ASP.NET Core Minimal API implementation using:
- **Models**: Define the data structures for Agents, Nodes, Runs, and Deployments
- **Services**: In-memory storage implementations (will be replaced with PostgreSQL in future tasks)
- **Endpoints**: REST API endpoints following the design specified in the System Architecture Document

## Current Implementation

This implementation provides:

- ✅ Full CRUD operations for Agents
- ✅ Agent versioning with semantic versioning validation (E3-T2)
- ✅ **Deployment API with replicas and placement labels** (E3-T3)
- ✅ **Invoice Classifier agent definition with Service Bus input and HTTP output** (E3-T6)
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
- ✅ Comprehensive integration tests (302 tests)

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

See `tasks.yaml` for the full project roadmap. The completed tasks include:

**Epic 1 – Control Plane Foundations:**
- ✅ **E1-T1**: API skeleton (Complete)
- ✅ **E1-T2**: Integrate Microsoft Agent Framework SDK (Complete)
- ✅ **E1-T3**: Database setup (Complete)
- ✅ **E1-T4**: Add Redis for lease and lock management (Complete)
- ✅ **E1-T5**: Set up NATS for event streaming (Complete)
- ✅ **E1-T6**: Implement gRPC service for node communication (Complete)
- ✅ **E1-T7**: Scheduler service (Complete)
- ✅ **E1-T8**: OpenTelemetry wiring (Complete)
- ✅ **E1-T9**: Authentication setup (Complete)
- ✅ **E1-T10**: Containerization (Complete)
- ✅ **E1-T11**: CI pipeline (Complete)

**Epic 3 – Agent Definition & Deployment Flow:**
- ✅ **E3-T1**: AgentDefinition model (Complete)
- ✅ **E3-T2**: Versioning endpoint (Complete)
- ✅ **E3-T3**: Deployment API (Complete)
- ⏳ **E3-T4**: Azure AI Foundry integration (Next)
- ⏳ **E3-T5**: Tool registry setup
- ⏳ **E3-T6**: Invoice Classifier agent
- ⏳ **E3-T7**: Integration test

## Authentication

The Control Plane API supports OIDC authentication with JWT Bearer tokens. Authentication is configurable and disabled by default for ease of development.

**For detailed authentication setup and configuration, see [AUTHENTICATION.md](./AUTHENTICATION.md).**

**Quick Start:**
- Keycloak for development (docker-compose.dev.yml included)
- Microsoft Entra ID supported for production
- Configure via `appsettings.json` Authentication section
- Enable/disable authentication without code changes

## CI/CD Pipeline

The project includes a comprehensive CI/CD pipeline with automated builds, tests, security scanning, SBOM generation, and container image signing.

**For detailed CI/CD pipeline documentation, see [CI-CD.md](./CI-CD.md).**

**Key Features:**
- Automated build and test on every push and PR
- SBOM generation for compliance (SPDX format)
- Container image signing with Sigstore/Cosign
- Security scanning with Trivy and CodeQL
- Dependency review and secret scanning
- Automated releases with semantic versioning

**Epic 2 – Node Runtime & Connectors:**
- ✅ **E2-T1**: Node runtime skeleton (Complete)
- ⏳ **E2-T2**: Integrate MAF runtime (Next)
- ⏳ **E2-T3**: Node registration enhancement
- ⏳ **E2-T4**: Lease pull loop completion
- ⏳ **E2-T5**: Sandbox process model
- ⏳ **E2-T6**: Service Bus connector
- ⏳ **E2-T7**: HTTP output connector
- ⏳ **E2-T8**: DLQ handling
- ⏳ **E2-T9**: Node telemetry
- ⏳ **E2-T10**: Secure communication

## Containerization & Deployment

The platform is fully containerized and can be deployed using Docker Compose or Kubernetes with Helm.

### Docker Deployment

#### Using Docker Compose (Full Stack)

Run all services locally with Docker Compose:

```bash
# Build and start all services
docker-compose up --build

# Start with observability stack
docker-compose --profile observability up --build

# Stop all services
docker-compose down

# Clean up volumes
docker-compose down -v
```

Services will be available at:
- **Control Plane API**: http://localhost:8080
- **Admin UI**: http://localhost:3000
- **Grafana** (with observability profile): http://localhost:3001

#### Building Individual Services

```bash
# Build Control Plane API
docker build -t business-process-agents/control-plane:latest -f src/ControlPlane.Api/Dockerfile .

# Build Node Runtime
docker build -t business-process-agents/node-runtime:latest -f src/Node.Runtime/Dockerfile .

# Build Admin UI
docker build -t business-process-agents/admin-ui:latest -f src/admin-ui/Dockerfile ./src/admin-ui
```

### Kubernetes Deployment with Helm

#### Prerequisites

- Kubernetes 1.24+
- Helm 3.8+
- kubectl configured to access your cluster

#### Quick Start (Local k3d)

```bash
# Create k3d cluster
k3d cluster create bpa-dev --servers 1 --agents 2

# Install the Helm chart
helm install bpa ./helm/business-process-agents

# Port forward to access services
kubectl port-forward svc/bpa-business-process-agents-control-plane 8080:8080
kubectl port-forward svc/bpa-business-process-agents-admin-ui 3000:3000
```

#### Production Deployment

```bash
# Create a custom values file
cat > values-production.yaml <<EOF
controlPlane:
  replicaCount: 3
  autoscaling:
    enabled: true
    minReplicas: 3
    maxReplicas: 10
  ingress:
    enabled: true
    className: nginx
    hosts:
      - host: api.bpa.example.com
        paths:
          - path: /
            pathType: Prefix

adminUI:
  ingress:
    enabled: true
    className: nginx
    hosts:
      - host: admin.bpa.example.com
        paths:
          - path: /
            pathType: Prefix

postgresql:
  persistence:
    size: 50Gi
  auth:
    password: <secure-password>

nodeRuntime:
  autoscaling:
    enabled: true
    minReplicas: 5
    maxReplicas: 50
EOF

# Install with production values
helm install bpa ./helm/business-process-agents -f values-production.yaml

# Verify deployment
kubectl get pods -l app.kubernetes.io/instance=bpa
```

#### Helm Chart Configuration

See [Helm Chart README](helm/business-process-agents/README.md) for detailed configuration options.

Key configuration areas:
- **Control Plane**: Replicas, autoscaling, ingress, resources
- **Node Runtime**: Capacity, placement metadata, autoscaling
- **Admin UI**: Ingress configuration
- **PostgreSQL**: Persistence, credentials, size
- **Redis**: Persistence, size
- **NATS**: JetStream configuration, persistence
- **Observability**: OpenTelemetry, Prometheus, Grafana

#### Upgrading

```bash
# Upgrade the release
helm upgrade bpa ./helm/business-process-agents -f values-production.yaml

# Rollback if needed
helm rollback bpa
```

#### Uninstalling

```bash
# Uninstall the release
helm uninstall bpa

# Clean up PVCs (optional)
kubectl delete pvc -l app.kubernetes.io/instance=bpa
```

### Container Images

The project includes Dockerfiles for all services:

- **Control Plane API** (`src/ControlPlane.Api/Dockerfile`):
  - Multi-stage build with .NET SDK and ASP.NET runtime
  - Non-root user execution
  - Health checks configured
  - Base image: `mcr.microsoft.com/dotnet/aspnet:9.0`

- **Node Runtime** (`src/Node.Runtime/Dockerfile`):
  - Multi-stage build with .NET SDK and ASP.NET runtime
  - Non-root user execution
  - Base image: `mcr.microsoft.com/dotnet/aspnet:9.0`

- **Admin UI** (`src/admin-ui/Dockerfile`):
  - Multi-stage build with Node.js
  - Next.js standalone output
  - Non-root user execution
  - Base image: `node:20-alpine`

All images follow security best practices:
- Non-root user execution
- Minimal base images (Alpine where possible)
- Multi-stage builds to reduce image size
- Health checks configured
- No secrets in images

## Components

### Control Plane API

The Control Plane provides centralized management and orchestration of the agent platform. See [Control Plane API documentation](src/ControlPlane.Api/README.md) for details.

**Key Features:**
- REST API for agent, node, and run management
- gRPC LeaseService for efficient node communication
- PostgreSQL for persistent storage
- Redis for distributed leases and locks
- NATS JetStream for event streaming
- OpenTelemetry for full observability

### Node Runtime

The Node Runtime executes business process agents on worker nodes. See [Node Runtime documentation](src/Node.Runtime/README.md) for details.

**Key Features:**
- .NET Worker Service architecture
- Automatic node registration and heartbeat
- gRPC client for lease pull loop
- OpenTelemetry instrumentation
- Configurable capacity and placement metadata
- Agent execution with budget enforcement (to be implemented in E2-T2)

## OpenAPI/Swagger

In development mode, OpenAPI documentation is available at:
- `/openapi/v1.json` - OpenAPI specification

## Agents

The platform includes the following business process agents:

### Invoice Classifier Agent

The **Invoice Classifier** is the MVP demonstration agent that showcases end-to-end message processing:

- **Agent ID**: `invoice-classifier`
- **Purpose**: Classifies invoices by vendor category and routes to appropriate departments
- **Input**: Azure Service Bus queue (`invoices`)
- **Output**: HTTP POST to target API with idempotency
- **Model**: GPT-4 with temperature 0.3 for consistent classification

**Vendor Categories:**
- Office Supplies → Procurement Department
- Technology/Hardware → IT Department
- Professional Services → Finance Department
- Utilities → Facilities Management
- Travel & Expenses → HR Department
- Other → General Accounts Payable

**Seeding the Agent:**

```bash
cd agents
./seed-invoice-classifier.sh
```

**Documentation:**
- [Invoice Classifier Technical Documentation](docs/INVOICE_CLASSIFIER.md)
- [Agent Definition](agents/definitions/invoice-classifier.json)
- [Agent Definitions Guide](agents/README.md)

## Documentation

- [System Architecture Document (SAD)](sad.md) - High-level system design and architecture
- [Invoice Classifier Agent](docs/INVOICE_CLASSIFIER.md) - Technical documentation for the MVP Invoice Classifier agent
- [Agent Definitions Guide](agents/README.md) - Guide to agent definitions and seeding agents
- [Agent Versioning and Validation](docs/VERSIONING.md) - Guide to agent versioning, semantic versioning, and spec validation
- [Azure AI Foundry Tool Registry](docs/AZURE_AI_FOUNDRY_TOOLS.md) - Azure AI Foundry tool provider and MAF SDK integration
- [Authentication](AUTHENTICATION.md) - Authentication and authorization setup
- [Deployment](DEPLOYMENT.md) - Deployment guides for local and cloud environments
- [Observability](OBSERVABILITY.md) - Monitoring, logging, and tracing configuration
- [CI/CD](CI-CD.md) - Continuous integration and deployment pipelines

## Contributing

Follow the branching strategy defined in `.github/copilot-instructions.md`:
- Use feature branches: `feature/E1-T<number>-<description>`
- Keep branches short-lived (< 3 days of work)
- Create pull requests for all changes to `main`
