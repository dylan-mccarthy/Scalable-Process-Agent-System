# Invoice Classifier Agent - Technical Documentation

## Overview

The Invoice Classifier agent is the MVP demonstration agent for the Business Process Agents platform. It showcases end-to-end message processing using the Microsoft Agent Framework, Azure Service Bus, and HTTP output connectors.

**Epic:** E3 – Agent Definition & Deployment Flow  
**Task:** E3-T6 – Invoice Classifier agent  
**Status:** Implementation Complete

## Purpose

The Invoice Classifier agent automates the classification and routing of invoices by:

1. Receiving invoice data from Azure Service Bus queue
2. Using an LLM (GPT-4) to classify invoices by vendor category
3. Determining the appropriate routing destination based on classification
4. Sending classified data to a downstream HTTP API with retry and idempotency
5. Handling failures gracefully with DLQ support

## Architecture

### Data Flow

```
┌─────────────────┐      ┌──────────────┐      ┌─────────────┐      ┌──────────────┐
│  Azure Service  │      │     Node     │      │   Agent     │      │  Invoice API │
│      Bus        │─────>│   Runtime    │─────>│   (GPT-4)   │─────>│  (HTTP POST) │
│   (invoices)    │      │ (SB Input)   │      │ Classifier  │      │              │
└─────────────────┘      └──────────────┘      └─────────────┘      └──────────────┘
         │                                              │
         │                                              │
         v                                              v
┌─────────────────┐                            ┌─────────────┐
│      DLQ        │                            │  Telemetry  │
│  (Failed msgs)  │                            │   (OTel)    │
└─────────────────┘                            └─────────────┘
```

### Component Integration

1. **Service Bus Input Connector**
   - Receives messages from the `invoices` queue
   - Prefetch: 16 messages for throughput
   - PeekLock mode for at-least-once delivery
   - Automatic DLQ after 3 failed deliveries

2. **Agent Executor**
   - Spawns isolated process (Agent.Host)
   - Enforces budget constraints (tokens, time)
   - Executes LLM classification via MAF SDK
   - Captures telemetry (tokens, cost, latency)

3. **HTTP Output Connector**
   - POST to configured endpoint
   - Idempotency via `{RunId}-{MessageId}` header
   - Exponential backoff retry (max 3 attempts)
   - Timeout: 30 seconds

4. **Control Plane Orchestration**
   - Tracks run state (pending → running → completed/failed)
   - Publishes state change events to NATS
   - Records metrics and traces

## Agent Definition

### Core Configuration

| Property | Value | Purpose |
|----------|-------|---------|
| Agent ID | `invoice-classifier` | Unique identifier |
| Name | Invoice Classifier | Display name |
| Model | GPT-4 | LLM for classification |
| Temperature | 0.3 | Low for consistent results |
| Max Tokens | 4000 | Token budget per run |
| Max Duration | 60s | Time budget per run |

### Input Configuration (Service Bus)

```json
{
  "type": "ServiceBus",
  "config": {
    "connectionString": "${SERVICE_BUS_CONNECTION_STRING}",
    "queueName": "invoices",
    "prefetchCount": 16,
    "maxDeliveryCount": 3,
    "receiveMode": "PeekLock",
    "maxWaitTime": "00:00:30"
  }
}
```

**Configuration Parameters:**

- `connectionString`: Azure Service Bus connection string (from environment variable)
- `queueName`: Queue name for invoice messages
- `prefetchCount`: Number of messages to prefetch for better throughput
- `maxDeliveryCount`: Maximum delivery attempts before DLQ
- `receiveMode`: PeekLock for reliable message processing
- `maxWaitTime`: Maximum time to wait for messages

### Output Configuration (HTTP)

```json
{
  "type": "Http",
  "config": {
    "endpoint": "${INVOICE_API_ENDPOINT}",
    "method": "POST",
    "headers": {
      "Content-Type": "application/json",
      "X-API-Key": "${INVOICE_API_KEY}"
    },
    "timeoutSeconds": 30,
    "retryPolicy": {
      "maxRetries": 3,
      "retryDelayMs": 1000,
      "useExponentialBackoff": true,
      "maxRetryDelayMs": 10000
    },
    "idempotencyKeyFormat": "{RunId}-{MessageId}"
  }
}
```

**Configuration Parameters:**

- `endpoint`: Target API URL (from environment variable)
- `method`: HTTP method (POST)
- `headers`: Custom headers including API key authentication
- `timeoutSeconds`: Request timeout
- `retryPolicy`: Retry configuration with exponential backoff
- `idempotencyKeyFormat`: Template for idempotency key header

## System Prompt

The agent uses a structured system prompt that guides the LLM to:

1. **Analyze Invoice Data**: Extract vendor, amounts, dates, line items
2. **Classify Vendor**: Categorize into predefined categories
3. **Determine Routing**: Map category to destination department
4. **Format Output**: Produce structured JSON response

### Classification Categories

| Category | Department | Examples |
|----------|-----------|----------|
| Office Supplies | Procurement | Paper, pens, desk supplies |
| Technology/Hardware | IT | Laptops, monitors, servers |
| Professional Services | Finance | Consulting, legal, audit |
| Utilities | Facilities | Electric, water, gas |
| Travel & Expenses | HR | Flights, hotels, meals |
| Other | General AP | Uncategorized vendors |

### Output Schema

```typescript
interface InvoiceClassification {
  vendorName: string;        // Vendor name
  vendorCategory: string;    // Classification category
  invoiceNumber: string;     // Invoice ID
  invoiceDate: string;       // Invoice date (ISO 8601)
  totalAmount: number;       // Total invoice amount
  currency: string;          // Currency code (e.g., USD)
  routingDestination: string;// Target department
  confidence: number;        // Classification confidence (0.0 - 1.0)
}
```

## Deployment

### Prerequisites

1. **Azure Service Bus**
   - Namespace created
   - Queue named `invoices` created
   - Connection string available

2. **Target API**
   - HTTP endpoint accepting POST requests
   - API key authentication configured
   - Idempotency header support

3. **Control Plane**
   - Running and accessible
   - Database initialized
   - NATS configured

4. **Node Runtime**
   - Running with Service Bus connector enabled
   - Environment variables configured

### Seeding the Agent

Use the provided seed script:

```bash
cd agents
./seed-invoice-classifier.sh
```

Or manually via API:

```bash
# Create agent
curl -X POST http://localhost:5109/v1/agents \
  -H "Content-Type: application/json" \
  -d @definitions/invoice-classifier.json

# Create version 1.0.0
curl -X POST http://localhost:5109/v1/agents/invoice-classifier:version \
  -H "Content-Type: application/json" \
  -d '{
    "version": "1.0.0",
    "spec": { ... agent definition ... }
  }'
```

### Creating Deployment

```bash
curl -X POST http://localhost:5109/v1/deployments \
  -H "Content-Type: application/json" \
  -d '{
    "agentId": "invoice-classifier",
    "version": "1.0.0",
    "env": "dev",
    "replicas": 2,
    "slotBudget": 4,
    "placement": {
      "affinity": {
        "region": ["aus-east"]
      }
    }
  }'
```

### Environment Configuration

Configure these variables in the Node Runtime:

```bash
# Service Bus
export SERVICE_BUS_CONNECTION_STRING="Endpoint=sb://..."

# Invoice API
export INVOICE_API_ENDPOINT="https://api.example.com/invoices"
export INVOICE_API_KEY="your-api-key-here"

# Optional: Azure AI Foundry (when E3-T4 is complete)
export AZURE_AI_FOUNDRY_ENDPOINT="https://..."
export AZURE_AI_FOUNDRY_KEY="..."
```

## Testing

### End-to-End Test

1. **Send test invoice to Service Bus:**

```bash
az servicebus queue message send \
  --namespace-name your-namespace \
  --queue-name invoices \
  --body '{
    "vendorName": "Acme Office Supplies",
    "invoiceNumber": "INV-2024-1001",
    "invoiceDate": "2024-10-30",
    "totalAmount": 542.75,
    "currency": "USD",
    "lineItems": [
      {
        "description": "Paper (A4, White, 500 sheets x 10 reams)",
        "quantity": 10,
        "unitPrice": 25.00,
        "total": 250.00
      },
      {
        "description": "Ballpoint Pens (Blue, Box of 50)",
        "quantity": 5,
        "unitPrice": 15.00,
        "total": 75.00
      },
      {
        "description": "Stapler (Heavy Duty)",
        "quantity": 3,
        "unitPrice": 18.50,
        "total": 55.50
      },
      {
        "description": "File Folders (Letter Size, Box of 100)",
        "quantity": 2,
        "unitPrice": 32.00,
        "total": 64.00
      },
      {
        "description": "Desk Organizer",
        "quantity": 4,
        "unitPrice": 24.50,
        "total": 98.00
      }
    ]
  }'
```

2. **Expected Output (to Invoice API):**

```json
{
  "vendorName": "Acme Office Supplies",
  "vendorCategory": "Office Supplies",
  "invoiceNumber": "INV-2024-1001",
  "invoiceDate": "2024-10-30",
  "totalAmount": 542.75,
  "currency": "USD",
  "routingDestination": "Procurement Department",
  "confidence": 0.98
}
```

3. **Verify execution:**

```bash
# List runs
curl http://localhost:5109/v1/runs

# Get run details
curl http://localhost:5109/v1/runs/{runId}
```

### Unit Testing

Tests should validate:
- Agent definition validation
- System prompt structure
- Input/output connector configuration
- Budget constraints

Example test (xUnit):

```csharp
[Fact]
public async Task InvoiceClassifier_HasValidDefinition()
{
    // Arrange
    var definitionPath = "agents/definitions/invoice-classifier.json";
    var definition = await File.ReadAllTextAsync(definitionPath);
    var agent = JsonSerializer.Deserialize<Agent>(definition);

    // Assert
    Assert.NotNull(agent);
    Assert.Equal("invoice-classifier", agent.AgentId);
    Assert.Equal("Invoice Classifier", agent.Name);
    Assert.NotNull(agent.Instructions);
    Assert.NotNull(agent.Input);
    Assert.Equal("ServiceBus", agent.Input.Type);
    Assert.NotNull(agent.Output);
    Assert.Equal("Http", agent.Output.Type);
    Assert.NotNull(agent.Budget);
    Assert.Equal(4000, agent.Budget.MaxTokens);
    Assert.Equal(60, agent.Budget.MaxDurationSeconds);
}
```

## Observability

### Metrics

| Metric | Type | Description |
|--------|------|-------------|
| `runs_started_total` | Counter | Total runs initiated |
| `runs_completed_total` | Counter | Successfully completed runs |
| `runs_failed_total` | Counter | Failed runs |
| `run_latency_ms` | Histogram | End-to-end latency |
| `sb_queue_lag` | Gauge | Messages in queue |
| `http_out_success_rate` | Gauge | HTTP delivery success rate |
| `tokens_in` | Counter | Input tokens consumed |
| `tokens_out` | Counter | Output tokens generated |
| `usd_cost` | Counter | Estimated LLM cost |

### Traces

Distributed traces include spans for:

1. **ServiceBus.Receive** - Message retrieval from queue
2. **Scheduler.Plan** - Lease assignment
3. **Agent.Execute** - LLM classification
4. **Http.Post** - API delivery
5. **ServiceBus.Complete** - Message acknowledgment

### Logs

Structured logs (JSON) include:

```json
{
  "timestamp": "2024-10-30T12:34:56.789Z",
  "level": "Information",
  "message": "Agent run completed",
  "runId": "run-abc123",
  "agentId": "invoice-classifier",
  "traceId": "trace-xyz789",
  "duration": 1234,
  "tokensIn": 125,
  "tokensOut": 85,
  "cost": 0.0042
}
```

## Error Handling

### Failure Scenarios

| Scenario | Behavior | Recovery |
|----------|----------|----------|
| LLM timeout | Fails after 60s | Retry (up to 3x) |
| LLM error | Logged, run fails | Retry or DLQ |
| HTTP timeout | Fails after 30s | Exponential backoff retry |
| HTTP 4xx | Non-retryable | DLQ immediately |
| HTTP 5xx | Retryable | Exponential backoff retry |
| Deserialization | Non-retryable | DLQ immediately |
| Budget exceeded | Run fails | Logged, no retry |

### Dead Letter Queue

Messages are sent to DLQ when:
- Delivery count exceeds 3
- Non-retryable errors occur
- Deserialization fails

**Accessing DLQ:**

```bash
az servicebus queue message list \
  --namespace-name your-namespace \
  --queue-name invoices/$deadletterqueue
```

**Replaying DLQ messages:**

```bash
# Requires custom tooling or Azure Function
# See: https://docs.microsoft.com/azure/service-bus-messaging/service-bus-dead-letter-queues
```

## Performance Characteristics

### Expected Performance (MVP)

| Metric | Target | Notes |
|--------|--------|-------|
| Throughput | ≥50 runs/min | Single node |
| Latency (p95) | <2s | Excluding API latency |
| LLM Call (p95) | <1s | GPT-4 Turbo |
| HTTP Post (p95) | <500ms | Depends on API |
| Success Rate | >99% | Excluding DLQ |

### Resource Utilization

- **CPU**: ~0.5 cores per concurrent run
- **Memory**: ~512MB per agent process
- **Token Usage**: ~150-200 tokens per invoice (estimated)
- **Cost**: ~$0.003 - $0.005 per invoice (estimated)

## Security Considerations

1. **Secrets Management**
   - Connection strings via environment variables
   - Use Azure Key Vault or Kubernetes Secrets in production
   - Never commit secrets to source control

2. **API Authentication**
   - API key authentication for HTTP output
   - Consider mTLS for enhanced security
   - Rotate keys regularly

3. **Network Security**
   - Service Bus over TLS 1.2+
   - HTTP calls over HTTPS only
   - Consider private endpoints in production

4. **Data Privacy**
   - Invoice data may contain PII
   - Ensure compliance with data protection regulations
   - Consider data encryption at rest

## Future Enhancements

Potential improvements for future iterations:

1. **Advanced Classification**
   - Machine learning model for vendor matching
   - Historical classification learning
   - Multi-label classification

2. **Enhanced Routing**
   - Rule engine for complex routing logic
   - Priority-based routing
   - Approval workflows

3. **Validation**
   - Schema validation for invoice data
   - Business rule validation
   - Duplicate detection

4. **Monitoring**
   - Anomaly detection
   - Cost alerting
   - SLA monitoring

## References

- [System Architecture Document](../sad.md) - Section 6: Example Agent
- [Tasks YAML](../tasks.yaml) - Epic 3, Task 6
- [Agent Definition](../agents/definitions/invoice-classifier.json)
- [Service Bus Connector](../src/Node.Runtime/Connectors/ServiceBusInputConnector.cs)
- [Azure AI Foundry Integration](./AZURE_AI_FOUNDRY_INTEGRATION.md)

## Support

For issues or questions:
- **Team**: Platform Engineering
- **Epic Owner**: E3 - Agent Definition & Deployment Flow
- **Task**: E3-T6 - Invoice Classifier agent

---

**Document Version**: 1.0.0  
**Last Updated**: 2024-10-31  
**Status**: Implementation Complete
