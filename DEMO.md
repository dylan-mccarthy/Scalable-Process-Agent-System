# Invoice Classifier Demo - Walkthrough Guide

This demo provides a complete end-to-end walkthrough of the Business Process Agents platform, showcasing invoice classification using the Microsoft Agent Framework, Azure AI integration, and distributed orchestration.

## Overview

The demo demonstrates:
- ✅ Complete agent deployment workflow
- ✅ End-to-end invoice processing
- ✅ LLM-based classification (GPT-4)
- ✅ Distributed scheduling and execution
- ✅ Observability with OpenTelemetry
- ✅ Service resilience and error handling

## Quick Start

### Prerequisites

Before running the demo, ensure you have:
- **Docker** (version 20.10 or later)
- **Docker Compose** (version 2.0 or later)
- **jq** (JSON processor)
- **curl** (command-line HTTP client)
- **4GB+ RAM** available for containers

> **Note**: This demo uses Docker Compose and accesses the Control Plane API on port 8080.  
> If running services locally with `dotnet run`, the API uses port 5109 by default.  
> Set `CONTROL_PLANE_URL` environment variable to override the default URL.

### Installation

Install prerequisites on different platforms:

**macOS:**
```bash
brew install jq curl
```

**Ubuntu/Debian:**
```bash
sudo apt-get update
sudo apt-get install -y jq curl
```

**Windows (WSL2):**
```bash
sudo apt-get update
sudo apt-get install -y jq curl
```

### Running the Demo

From the repository root:

```bash
./demo-invoice-classifier.sh
```

The script will:
1. ✅ Check prerequisites
2. ✅ Start infrastructure services (PostgreSQL, Redis, NATS)
3. ✅ Deploy Control Plane API
4. ✅ Start Node Runtime workers
5. ✅ Seed the Invoice Classifier agent
6. ✅ Create an agent deployment
7. ✅ Display fleet status
8. ✅ Show sample invoices and expected classifications
9. ✅ Demonstrate observability features
10. ✅ Provide API examples for exploration

### Cleanup

To stop and remove all demo services:

```bash
./demo-invoice-classifier.sh cleanup
```

To remove all data including volumes:

```bash
docker compose down -v
```

## Demo Walkthrough

### 1. System Architecture

The platform consists of:

#### Control Plane
- **REST API**: Agent, node, and run management
- **gRPC LeaseService**: Efficient node communication
- **Scheduler**: Least-loaded strategy with placement constraints
- **PostgreSQL**: Persistent storage for agents, deployments, runs
- **Redis**: Distributed leases and locks with TTL
- **NATS JetStream**: Event streaming for state changes

#### Node Runtime
- **Worker Service**: Background worker that executes agents
- **gRPC Client**: Lease pull loop for work assignment
- **Service Bus Connector**: Azure Service Bus input integration
- **HTTP Connector**: Output delivery with retry and idempotency
- **Agent.Host**: Isolated process for agent execution

#### Microsoft Agent Framework
- **LLM Orchestration**: GPT-4 and Azure AI Foundry integration
- **Tool Registry**: Extensible tool system
- **Budget Enforcement**: Token and time constraints

### 2. Invoice Processing Flow

```
┌─────────────────┐      ┌──────────────┐      ┌─────────────┐      ┌──────────────┐
│  Azure Service  │      │     Node     │      │   Agent     │      │  Invoice API │
│      Bus        │─────▶│   Runtime    │─────▶│   (GPT-4)   │─────▶│  (HTTP POST) │
│   (invoices)    │      │ (SB Input)   │      │ Classifier  │      │              │
└─────────────────┘      └──────────────┘      └─────────────┘      └──────────────┘
         │                                              │
         │ (DLQ on failure)                            │ (Metrics/Traces)
         ▼                                              ▼
┌─────────────────┐                            ┌─────────────┐
│      DLQ        │                            │  OpenTelem. │
│  (Failed msgs)  │                            │   Metrics   │
└─────────────────┘                            └─────────────┘
```

**Process Steps:**
1. Invoice arrives in Azure Service Bus queue
2. Node Runtime receives message via Service Bus Input Connector
3. Control Plane schedules run to least-loaded node
4. Node spawns Agent.Host process with budget constraints
5. Agent (GPT-4) classifies invoice by vendor category
6. Agent determines routing destination (department)
7. HTTP Output Connector sends classification to Invoice API
8. Service Bus message is completed (or sent to DLQ on failure)
9. Metrics and traces are exported to observability stack

### 3. Sample Invoices

The demo processes three sample invoices:

**1. Office Supplies Invoice**
- **Vendor**: Office Depot
- **Amount**: $542.75
- **Expected Classification**: Office Supplies → Procurement Department

**2. Technology Invoice**
- **Vendor**: Dell Technologies
- **Amount**: $2,899.99
- **Expected Classification**: Technology/Hardware → IT Department

**3. Professional Services Invoice**
- **Vendor**: Accenture Consulting
- **Amount**: $15,000.00
- **Expected Classification**: Professional Services → Finance Department

### 4. Classification Categories

The Invoice Classifier agent supports the following categories:

| Category | Department | Examples |
|----------|-----------|----------|
| Office Supplies | Procurement | Paper, pens, desk supplies |
| Technology/Hardware | IT | Laptops, monitors, servers |
| Professional Services | Finance | Consulting, legal, audit |
| Utilities | Facilities | Electric, water, gas |
| Travel & Expenses | HR | Flights, hotels, meals |
| Other | General AP | Uncategorized vendors |

### 5. Observability

The platform includes comprehensive observability via OpenTelemetry:

#### Metrics
- `runs_started_total` - Total runs initiated
- `runs_completed_total` - Successfully completed runs
- `runs_failed_total` - Failed runs
- `run_duration_ms` - End-to-end latency distribution (p50, p95, p99)
- `run_tokens` - Token usage per run
- `run_cost_usd` - Estimated LLM cost per run

#### Distributed Tracing
- `ServiceBus.Receive` - Message retrieval from queue
- `Scheduler.Plan` - Lease assignment to node
- `Agent.Execute` - LLM classification
- `Http.Post` - API delivery with retry
- `ServiceBus.Complete` - Message acknowledgment

#### Structured Logging
- JSON-formatted logs with trace correlation
- Run IDs, Agent IDs, and Trace IDs for correlation
- Performance metrics (duration, tokens, cost)
- Error details and stack traces

**View logs:**
```bash
# Control Plane logs
docker compose logs -f control-plane

# Node Runtime logs
docker compose logs -f node-runtime

# All logs
docker compose logs -f
```

## API Examples

### 1. List All Agents

```bash
curl http://localhost:8080/v1/agents | jq
```

**Expected Output:**
```json
[
  {
    "agentId": "invoice-classifier",
    "name": "Invoice Classifier",
    "description": "Classifies invoices by vendor and routes appropriately",
    "createdAt": "2024-10-30T12:00:00Z",
    "updatedAt": "2024-10-30T12:00:00Z"
  }
]
```

### 2. Get Agent Details

```bash
curl http://localhost:8080/v1/agents/invoice-classifier | jq
```

### 3. List Agent Versions

```bash
curl http://localhost:8080/v1/agents/invoice-classifier/versions | jq
```

**Expected Output:**
```json
[
  {
    "versionId": "ver-abc123",
    "agentId": "invoice-classifier",
    "version": "1.0.0",
    "createdAt": "2024-10-30T12:00:00Z",
    "spec": { ... }
  }
]
```

### 4. List Deployments

```bash
curl http://localhost:8080/v1/deployments | jq
```

### 5. List Registered Nodes

```bash
curl http://localhost:8080/v1/nodes | jq
```

**Expected Output:**
```json
[
  {
    "nodeId": "node-1",
    "status": {
      "state": "active",
      "activeRuns": 0,
      "availableSlots": 8
    },
    "capacity": {
      "slots": 8,
      "cpu": "4",
      "memory": "8Gi"
    },
    "heartbeatAt": "2024-10-30T12:05:00Z"
  }
]
```

### 6. List Recent Runs

```bash
curl http://localhost:8080/v1/runs | jq
```

### 7. Get Run Details

```bash
curl http://localhost:8080/v1/runs/{runId} | jq
```

## Advanced Usage

### Enable Admin UI

The Admin UI provides a web interface for monitoring and management:

```bash
docker compose up -d admin-ui
```

Access at: http://localhost:3000

Features:
- Fleet dashboard (nodes and active runs)
- Runs list with status and duration
- Agent editor with model selection
- Real-time updates

### Deploy with Observability Stack

For full observability with Grafana dashboards:

```bash
docker compose --profile observability up -d
```

Access points:
- **Grafana**: http://localhost:3001
- **Prometheus**: http://localhost:9090
- **Jaeger**: http://localhost:16686

### Deploy to Kubernetes (k3d)

For a production-like environment:

```bash
./infra/scripts/setup-k3d.sh
```

This creates a local Kubernetes cluster with:
- 1 control plane node
- 2 worker nodes
- Full Helm chart deployment
- Ingress controller
- Persistent volumes

Access:
- **Control Plane API**: http://localhost:8080
- **Admin UI**: http://localhost:3000
- **Grafana**: http://localhost:3001

Cleanup:
```bash
./infra/scripts/cleanup-k3d.sh
```

### Azure AI Foundry Integration

To use Azure AI Foundry (GPT-4) instead of mocks:

1. Set environment variables in `docker-compose.yml`:
```yaml
environment:
  - AZURE_OPENAI_ENDPOINT=https://your-resource.openai.azure.com
  - AZURE_OPENAI_KEY=your-api-key
  - AZURE_OPENAI_DEPLOYMENT=gpt-4
```

2. Restart services:
```bash
docker compose restart control-plane node-runtime
```

See [Azure AI Foundry Integration](docs/AZURE_AI_FOUNDRY_INTEGRATION.md) for detailed setup.

## Testing

### Run E2E Tests

The E2E tests validate the complete invoice processing flow:

```bash
dotnet test tests/E2E.Tests/E2E.Tests.csproj
```

**Test suites:**
- **Invoice Processing**: Process 100 invoices with ≥95% success rate
- **Chaos Tests**: Simulate node failures and verify reassignment

### Run Unit Tests

```bash
# All tests
dotnet test

# Control Plane tests only
dotnet test tests/ControlPlane.Api.Tests/

# Node Runtime tests only
dotnet test tests/Node.Runtime.Tests/
```

### Manual Testing with Service Bus

To test with real Azure Service Bus:

1. Create a Service Bus namespace and queue named `invoices`

2. Configure connection string:
```bash
export SERVICE_BUS_CONNECTION_STRING="Endpoint=sb://..."
export INVOICE_API_ENDPOINT="https://your-api.example.com/invoices"
export INVOICE_API_KEY="your-api-key"
```

3. Send a test invoice:
```bash
az servicebus queue message send \
  --namespace-name your-namespace \
  --queue-name invoices \
  --body '{
    "vendorName": "Acme Office Supplies",
    "invoiceNumber": "TEST-001",
    "invoiceDate": "2024-10-30",
    "totalAmount": 542.75,
    "currency": "USD",
    "lineItems": [...]
  }'
```

4. Monitor processing:
```bash
# Watch runs
curl http://localhost:8080/v1/runs | jq

# View logs
docker compose logs -f node-runtime
```

## Troubleshooting

### Services Won't Start

**Issue**: Docker services fail to start

**Solutions:**
1. Check Docker is running: `docker ps`
2. Check available resources: Ensure 4GB+ RAM available
3. Check port conflicts: Ensure ports 5432, 6379, 4222, 8080 are free
4. View logs: `docker compose logs [service-name]`

### Control Plane Not Ready

**Issue**: Control Plane health check fails

**Solutions:**
1. Check database connection: `docker compose logs postgres`
2. Check Redis connection: `docker compose logs redis`
3. Wait longer (up to 60 seconds for first startup)
4. View API logs: `docker compose logs control-plane`

### Node Not Registering

**Issue**: Node Runtime doesn't appear in fleet status

**Solutions:**
1. Check gRPC connectivity: `docker compose logs node-runtime`
2. Verify Control Plane is accessible
3. Check network: `docker network inspect bpa-network`
4. Restart node: `docker compose restart node-runtime`

### Agent Seed Fails

**Issue**: Invoice Classifier agent creation fails

**Solutions:**
1. Verify Control Plane is ready: `curl http://localhost:8080/health`
2. Check jq is installed: `which jq`
3. Verify agent definition file exists: `ls agents/definitions/`
4. Run seed script manually: `./agents/seed-invoice-classifier.sh`

## Performance Characteristics

Based on E2E testing (E7-T3):

| Metric | Target | Actual (MVP) |
|--------|--------|--------------|
| Throughput | ≥50 runs/min | 100+ runs/min |
| Latency (p95) | <2s | ~0.047s (mock), ~1.2s (Azure AI) |
| Success Rate | ≥95% | 100% |
| LLM Call (p95) | <1s | ~0.8s (GPT-4 Turbo) |
| HTTP Post (p95) | <500ms | ~0.1s (local mock) |

**Resource Utilization:**
- CPU: ~0.5 cores per concurrent run
- Memory: ~512MB per agent process
- Token Usage: ~150-200 tokens per invoice
- Cost: ~$0.003-$0.005 per invoice (GPT-4)

## Security Considerations

1. **Secrets Management**
   - Connection strings via environment variables
   - Use Azure Key Vault or Kubernetes Secrets in production
   - Never commit secrets to source control

2. **API Authentication**
   - JWT Bearer tokens (Keycloak/Entra ID)
   - API key authentication for HTTP output
   - mTLS for node-control plane communication

3. **Network Security**
   - Service Bus over TLS 1.2+
   - HTTPS for all HTTP calls
   - Private endpoints in production

4. **Data Privacy**
   - Invoice data may contain PII
   - Ensure compliance with data protection regulations
   - Consider encryption at rest

## Next Steps

After completing the demo:

1. **Explore the Admin UI**
   ```bash
   docker compose up -d admin-ui
   # Visit http://localhost:3000
   ```

2. **Deploy to Kubernetes**
   ```bash
   ./infra/scripts/setup-k3d.sh
   ```

3. **Configure Azure AI Foundry**
   - See [Azure AI Foundry Integration](docs/AZURE_AI_FOUNDRY_INTEGRATION.md)

4. **Run Performance Tests**
   ```bash
   dotnet test tests/E2E.Tests/ --filter "FullyQualifiedName~InvoiceProcessingE2ETests"
   ```

5. **Explore Observability**
   ```bash
   docker compose --profile observability up -d
   # Visit http://localhost:3001 (Grafana)
   ```

6. **Deploy to Azure AKS**
   ```bash
   ./infra/scripts/deploy-azure.sh
   ```

## Documentation

- [System Architecture Document](sad.md) - High-level design
- [Invoice Classifier Agent](docs/INVOICE_CLASSIFIER.md) - Agent details
- [Agent Definitions](agents/README.md) - Agent configuration
- [Azure AI Foundry](docs/AZURE_AI_FOUNDRY_INTEGRATION.md) - LLM setup
- [Deployment Guide](DEPLOYMENT.md) - Production deployment
- [Observability](OBSERVABILITY.md) - Monitoring setup
- [CI/CD](CI-CD.md) - Pipeline configuration

## Support

For issues or questions:
- **Epic**: E8 – Documentation & Demo
- **Task**: E8-T3 – Demo script
- **Owner**: Platform Engineering

## License

This demo is part of the Business Process Agents MVP project.

---

**Version**: 1.0.0  
**Last Updated**: 2024-10-31  
**Status**: Complete
