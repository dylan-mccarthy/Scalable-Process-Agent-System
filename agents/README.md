# Business Process Agents - Agent Definitions

This directory contains agent definitions and seed scripts for the Business Process Agents platform.

## Directory Structure

```
agents/
├── definitions/               # Agent definition files (JSON)
│   └── invoice-classifier.json
├── seed-invoice-classifier.sh # Seed script for Invoice Classifier
└── README.md                  # This file
```

## Invoice Classifier Agent

The Invoice Classifier is the MVP agent that demonstrates the platform's capabilities for business process automation.

### Purpose

The Invoice Classifier agent:
- Receives invoice data from Azure Service Bus
- Classifies invoices by vendor category
- Routes invoices to appropriate departments
- Sends classified data to a downstream API

### Configuration

The agent is configured with:

**Input Connector (Service Bus)**
- Queue: `invoices`
- Connection: Configured via `SERVICE_BUS_CONNECTION_STRING` environment variable
- Prefetch: 16 messages
- Max Delivery Count: 3 (then moves to DLQ)
- Receive Mode: PeekLock (for reliability)

**Output Connector (HTTP)**
- Endpoint: Configured via `INVOICE_API_ENDPOINT` environment variable
- Method: POST
- Authentication: API Key via `INVOICE_API_KEY` environment variable
- Retry Policy: Exponential backoff, max 3 retries
- Idempotency: Uses `{RunId}-{MessageId}` format for idempotency keys

**Budget Constraints**
- Max Tokens: 4000
- Max Duration: 60 seconds

**Model Configuration**
- Model: GPT-4
- Temperature: 0.3 (lower temperature for consistent classification)
- Max Tokens: 4000

### System Prompt

The agent uses a detailed system prompt that:
1. Instructs the LLM to analyze invoice data
2. Extract key information (vendor, amounts, dates)
3. Classify vendors into predefined categories:
   - Office Supplies
   - Technology/Hardware
   - Professional Services
   - Utilities
   - Travel & Expenses
   - Other
4. Route to appropriate departments based on category
5. Output structured JSON with classification results

### Vendor Categories and Routing

| Category | Routing Destination |
|----------|---------------------|
| Office Supplies | Procurement Department |
| Technology/Hardware | IT Department |
| Professional Services | Finance Department |
| Utilities | Facilities Management |
| Travel & Expenses | HR Department |
| Other | General Accounts Payable |

### Output Format

The agent outputs a JSON object with the following structure:

```json
{
  "vendorName": "ABC Corporation",
  "vendorCategory": "Office Supplies",
  "invoiceNumber": "INV-2024-001",
  "invoiceDate": "2024-10-30",
  "totalAmount": 1250.00,
  "currency": "USD",
  "routingDestination": "Procurement Department",
  "confidence": 0.95
}
```

### Seeding the Agent

To register the Invoice Classifier agent with the Control Plane API:

```bash
# Ensure the Control Plane API is running
cd src/ControlPlane.Api
dotnet run

# In a separate terminal, run the seed script
cd agents
./seed-invoice-classifier.sh
```

You can override the Control Plane URL:

```bash
CONTROL_PLANE_URL=http://localhost:5109 ./seed-invoice-classifier.sh
```

### Creating a Deployment

After seeding the agent, create a deployment:

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
      "region": ["aus-east"]
    }
  }'
```

### Environment Variables

Configure these environment variables for the Node Runtime:

```bash
# Azure Service Bus connection string
export SERVICE_BUS_CONNECTION_STRING="Endpoint=sb://your-namespace.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=your-key"

# Invoice API endpoint
export INVOICE_API_ENDPOINT="https://api.example.com/invoices"

# Invoice API authentication
export INVOICE_API_KEY="your-api-key"
```

### Testing the Agent

1. **Send a test invoice to Service Bus:**

```bash
# Using Azure CLI
az servicebus queue message send \
  --namespace-name your-namespace \
  --queue-name invoices \
  --body '{
    "vendorName": "Office Depot",
    "invoiceNumber": "INV-2024-001",
    "invoiceDate": "2024-10-30",
    "totalAmount": 1250.00,
    "currency": "USD",
    "lineItems": [
      {
        "description": "Paper (A4, 5 reams)",
        "quantity": 5,
        "unitPrice": 25.00,
        "total": 125.00
      },
      {
        "description": "Pens (Box of 100)",
        "quantity": 10,
        "unitPrice": 12.50,
        "total": 125.00
      }
    ]
  }'
```

2. **Monitor the agent execution:**

```bash
# View runs
curl http://localhost:5109/v1/runs

# View specific run details
curl http://localhost:5109/v1/runs/{runId}
```

3. **Check the output API endpoint** to verify the classified invoice was sent

### Observability

The agent execution produces:

**Metrics:**
- `runs_started_total`: Counter of runs started
- `runs_completed_total`: Counter of successful runs
- `runs_failed_total`: Counter of failed runs
- `run_latency_ms`: Histogram of run latency
- `tokens_in`: Input token usage
- `tokens_out`: Output token usage
- `usd_cost`: Estimated cost per run

**Traces:**
Distributed traces with spans for:
- Service Bus message receipt
- Agent execution
- LLM classification
- HTTP output delivery
- Message completion

**Logs:**
Structured JSON logs with:
- Run ID
- Agent ID
- Trace ID
- Execution status
- Error messages (if any)

### Dead Letter Queue (DLQ)

Failed messages are sent to the DLQ after:
- 3 delivery attempts (configured via `maxDeliveryCount`)
- Deserialization errors
- Non-retryable errors

Access the DLQ:

```bash
# List DLQ messages
az servicebus queue message list \
  --namespace-name your-namespace \
  --queue-name invoices/$deadletterqueue
```

### Related Documentation

- [System Architecture Document (SAD)](../sad.md)
- [Azure AI Foundry Integration](../docs/AZURE_AI_FOUNDRY_INTEGRATION.md)
- [Service Bus Connector](../src/Node.Runtime/Connectors/ServiceBusInputConnector.cs)
- [Tasks YAML](../tasks.yaml) - Epic 3, Task 6

## Adding New Agents

To add a new agent:

1. Create a JSON definition file in `definitions/`:

```json
{
  "agentId": "my-agent",
  "name": "My Agent",
  "description": "Agent description",
  "instructions": "System prompt...",
  "modelProfile": { ... },
  "budget": { ... },
  "input": { ... },
  "output": { ... }
}
```

2. Create a seed script following the pattern of `seed-invoice-classifier.sh`

3. Update this README with agent documentation

4. Add tests to validate the agent definition

## License

See the main repository README for license information.
