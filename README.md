# Business Process Agents - Control Plane API

This is the Control Plane API for the Business Process Agents MVP project. It provides REST endpoints for managing Agents, Nodes, and Runs.

## Project Structure

```
├── src/
│   └── ControlPlane.Api/          # Main API project
│       ├── Models/                # Data models
│       ├── Services/              # Business logic and storage
│       └── Program.cs             # API endpoints and configuration
├── tests/
│   └── ControlPlane.Api.Tests/    # Integration tests
└── BusinessProcessAgents.sln      # Solution file
```

## Prerequisites

- .NET 9.0 SDK or later

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

This is a skeleton implementation (E1-T1) that provides:
- ✅ Full CRUD operations for Agents
- ✅ Node registration and heartbeat endpoints
- ✅ Run state management endpoints (complete, fail, cancel)
- ✅ In-memory storage for all entities
- ✅ Input validation and error handling
- ✅ Comprehensive integration tests (21 tests)

## Next Steps

See `tasks.yaml` for the full project roadmap. The next tasks include:
- **E1-T2**: Integrate Microsoft Agent Framework SDK
- **E1-T3**: Replace in-memory storage with PostgreSQL
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
