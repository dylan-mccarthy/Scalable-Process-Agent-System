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
    "DefaultConnection": "Host=localhost;Port=5432;Database=bpa;Username=postgres;Password=postgres"
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
- ✅ Comprehensive integration tests (39 tests)

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

**Note:** Actual agent execution requires Azure AI Foundry or OpenAI credentials, which will be configured in task E3-T4 (Azure AI Foundry integration).

## Next Steps

See `tasks.yaml` for the full project roadmap. The next tasks include:
- ✅ **E1-T1**: API skeleton (Complete)
- ✅ **E1-T2**: Integrate Microsoft Agent Framework SDK (Complete)
- ✅ **E1-T3**: Database setup (Complete)
- **E1-T4**: Add Redis for lease and lock management
- **E1-T5**: Set up NATS for event streaming
- **E1-T6**: Implement gRPC service for node communication

## OpenAPI/Swagger

In development mode, OpenAPI documentation is available at:
- `/openapi/v1.json` - OpenAPI specification

## Contributing

Follow the branching strategy defined in `.github/copilot-instructions.md`:
- Use feature branches: `feature/E1-T<number>-<description>`
- Keep branches short-lived (< 3 days of work)
- Create pull requests for all changes to `main`
