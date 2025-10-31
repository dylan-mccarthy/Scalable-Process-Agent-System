# Quick Start - Invoice Classifier Demo

Get the Business Process Agents platform running in under 5 minutes!

## Prerequisites

- Docker and Docker Compose
- 4GB+ RAM available
- Linux, macOS, or Windows (WSL2)

Install required tools:
```bash
# macOS
brew install jq curl

# Ubuntu/Debian
sudo apt-get install -y jq curl
```

## 1-Minute Quick Start

```bash
# Clone the repository
git clone https://github.com/dylan-mccarthy/Scalable-Process-Agent-System.git
cd Scalable-Process-Agent-System

# Run the interactive demo
./demo-invoice-classifier.sh
```

That's it! The demo will:
1. ✅ Start all required services
2. ✅ Deploy the Invoice Classifier agent
3. ✅ Show you the complete workflow
4. ✅ Demonstrate observability features

## What You'll See

The demo showcases invoice classification using AI:

```
Invoice → Azure Service Bus → Node Runtime → GPT-4 Classification → API Delivery
```

**Sample Invoice:**
```json
{
  "vendorName": "Office Depot",
  "invoiceNumber": "DEMO-001",
  "totalAmount": 542.75,
  "currency": "USD"
}
```

**Classification Result:**
```json
{
  "vendorCategory": "Office Supplies",
  "routingDestination": "Procurement Department",
  "confidence": 0.98
}
```

## Services Started

| Service | URL | Purpose |
|---------|-----|---------|
| Control Plane API | http://localhost:8080 | REST + gRPC API |
| PostgreSQL | localhost:5432 | Persistent storage |
| Redis | localhost:6379 | Leases & locks |
| NATS | localhost:4222 | Event streaming |
| Node Runtime | (internal) | Agent execution |

## Explore the API

After the demo starts, try these commands:

```bash
# List agents
curl http://localhost:8080/v1/agents | jq

# List nodes
curl http://localhost:8080/v1/nodes | jq

# View agent details
curl http://localhost:8080/v1/agents/invoice-classifier | jq
```

## View Logs

```bash
# All logs
docker compose logs -f

# Control Plane only
docker compose logs -f control-plane

# Node Runtime only
docker compose logs -f node-runtime
```

## Cleanup

```bash
./demo-invoice-classifier.sh cleanup
```

To remove all data:
```bash
docker compose down -v
```

## Next Steps

1. **Read the full walkthrough**: [DEMO.md](DEMO.md)
2. **Deploy to Kubernetes**: `./infra/scripts/setup-k3d.sh`
3. **Run E2E tests**: `dotnet test tests/E2E.Tests/`
4. **Configure Azure AI**: [docs/AZURE_AI_FOUNDRY_INTEGRATION.md](docs/AZURE_AI_FOUNDRY_INTEGRATION.md)

## Troubleshooting

**Services won't start?**
- Check Docker is running: `docker ps`
- Ensure ports are available: 5432, 6379, 4222, 8080
- Check available RAM: Minimum 4GB required

**Control Plane not ready?**
- Wait up to 60 seconds for first startup
- Check logs: `docker compose logs control-plane`

**Need help?**
- See full troubleshooting guide in [DEMO.md](DEMO.md)

## What's Running?

The platform demonstrates:
- ✅ **Distributed Orchestration**: Control Plane schedules work to nodes
- ✅ **Agent Execution**: Microsoft Agent Framework + GPT-4
- ✅ **Service Integration**: Azure Service Bus + HTTP output
- ✅ **Observability**: OpenTelemetry metrics, traces, and logs
- ✅ **Resilience**: DLQ, retries, lease management

---

**Ready for more?** See the [full demo walkthrough](DEMO.md) for detailed explanations and advanced features.
