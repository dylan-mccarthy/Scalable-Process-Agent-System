# Architecture Diagrams

This document contains C4 architecture diagrams for the Business Process Agents MVP system.

## Overview

The C4 model provides a hierarchical view of the system architecture:
- **Context**: System context and external dependencies
- **Container**: High-level technology choices and container interactions

---

## C4 Context Diagram

The context diagram shows how the Business Process Agents platform fits into the broader enterprise ecosystem, including external actors and systems.

```mermaid
C4Context
title Business Process Agents MVP - System Context

Person(admin, "Platform Admin", "Manages agents, monitors system health, configures deployments")
Person(developer, "Agent Developer", "Creates and deploys business process agents")

System(bpa, "Business Process Agents Platform", "Orchestrates AI agents for business process automation using Microsoft Agent Framework")

System_Ext(azureai, "Azure AI Foundry", "Provides LLM models (GPT-4, etc.) for agent reasoning")
System_Ext(servicebus, "Azure Service Bus", "Message queue for input events and DLQ")
System_Ext(keyvault, "Azure Key Vault", "Stores secrets and connection strings")
System_Ext(targetapi, "Target Business APIs", "Downstream systems that agents interact with (e.g., Invoice API)")
System_Ext(identity, "Identity Provider", "OIDC authentication (Keycloak/Entra)")

Rel(admin, bpa, "Monitors and manages", "HTTPS/UI")
Rel(developer, bpa, "Deploys agents", "API/UI")

Rel(bpa, azureai, "Calls LLM", "HTTPS/OpenAI SDK")
Rel(bpa, servicebus, "Consumes messages, publishes to DLQ", "AMQP")
Rel(bpa, keyvault, "Retrieves secrets", "HTTPS")
Rel(bpa, targetapi, "Invokes business logic", "HTTPS")
Rel(bpa, identity, "Authenticates users", "OIDC")

Rel(servicebus, bpa, "Triggers agent runs", "Event notification")
```

**Key External Dependencies:**

- **Azure AI Foundry**: Hosts LLM models (e.g., GPT-4) used by agents for reasoning and decision-making via Microsoft Agent Framework
- **Azure Service Bus**: Input queue for business events (invoices, orders, etc.) and dead-letter queue for failed messages
- **Azure Key Vault**: Secure storage for connection strings, API keys, and certificates
- **Target Business APIs**: External REST APIs that agents call to perform business actions (e.g., creating invoices, updating records)
- **Identity Provider**: OIDC provider for admin/developer authentication (Keycloak for dev, Entra for production)

---

## C4 Container Diagram

The container diagram shows the internal structure of the Business Process Agents platform, including key components and their interactions.

```mermaid
C4Container
title Business Process Agents MVP - Container View

Person(admin, "Platform Admin", "Manages agents and monitors system")

Container_Boundary(control, "Control Plane (Kubernetes)") {
  Container(api, "Control API", "ASP.NET Core + gRPC", "Manages agents, nodes, runs, and deployments")
  Container(scheduler, "Scheduler", "Hosted Service", "Least-loaded scheduling with placement constraints")
  Container(database, "PostgreSQL", "Relational Database", "Stores agents, versions, deployments, nodes, runs")
  Container(cache, "Redis", "In-Memory Store", "Manages leases, locks, and rate limits")
  Container(otel, "OTel Collector", "Telemetry Hub", "Collects and exports metrics, traces, logs")
  Container(ui, "Admin UI", "Next.js SPA", "Fleet dashboard, runs viewer, agent editor")
}

Container_Boundary(worker, "Worker Node") {
  Container(runtime, "Node Runtime", ".NET Worker Service", "Pulls leases, executes agents in sandboxes, reports results")
  Container(connectors, "Connectors SDK", ".NET Libraries", "Service Bus input, HTTP output, DLQ handling")
}

Container_Boundary(observability, "Observability Stack") {
  Container(prometheus, "Prometheus", "Metrics Store", "Stores time-series metrics")
  Container(tempo, "Tempo", "Trace Store", "Stores distributed traces")
  Container(loki, "Loki", "Log Aggregation", "Stores structured logs")
  Container(grafana, "Grafana", "Visualization", "Dashboards for metrics, traces, logs")
}

System_Ext(servicebus, "Azure Service Bus", "Message queue and DLQ")
System_Ext(azureai, "Azure AI Foundry", "LLM inference")
System_Ext(targetapi, "Target Business API", "Downstream systems")
System_Ext(keycloak, "Keycloak/Entra", "Identity provider")

Rel(admin, ui, "Uses", "HTTPS")
Rel(ui, api, "Calls", "REST/gRPC")
Rel(api, scheduler, "Invokes", "In-process")
Rel(api, database, "Reads/Writes", "SQL")
Rel(scheduler, cache, "Manages leases", "Redis protocol")
Rel(scheduler, database, "Queries nodes/runs", "SQL")

Rel(runtime, api, "Registers, heartbeats", "gRPC")
Rel(api, runtime, "Streams leases", "gRPC")
Rel(runtime, connectors, "Orchestrates", "In-process")
Rel(connectors, servicebus, "Receives/Acks/Nacks", "AMQP")
Rel(connectors, azureai, "Calls LLM via MAF", "HTTPS")
Rel(connectors, targetapi, "Posts results", "HTTPS")

Rel(runtime, otel, "Sends telemetry", "OTLP")
Rel(api, otel, "Sends telemetry", "OTLP")
Rel(otel, prometheus, "Exports metrics", "Prometheus Remote Write")
Rel(otel, tempo, "Exports traces", "OTLP")
Rel(otel, loki, "Exports logs", "Loki API")
Rel(grafana, prometheus, "Queries", "PromQL")
Rel(grafana, tempo, "Queries", "TraceQL")
Rel(grafana, loki, "Queries", "LogQL")

Rel(ui, keycloak, "Authenticates", "OIDC")
Rel(api, keycloak, "Validates tokens", "JWT")
```

**Key Containers:**

### Control Plane
- **Control API**: REST and gRPC endpoints for managing agents, nodes, and runs; integrates with Microsoft Agent Framework SDK
- **Scheduler**: Selects optimal node for each run based on capacity and placement constraints
- **PostgreSQL**: Persistent storage for all system state
- **Redis**: Distributed locks and lease management with TTL
- **OTel Collector**: Central telemetry aggregation point
- **Admin UI**: Web interface for operators and developers

### Worker Node
- **Node Runtime**: Long-running worker service that pulls leases, executes agents via MAF, and reports status
- **Connectors SDK**: Pluggable input/output adapters (Service Bus, HTTP, DLQ)

### Observability Stack
- **Prometheus**: Metrics storage and querying (runs, latency, tokens, cost)
- **Tempo**: Distributed tracing backend
- **Loki**: Log aggregation with trace correlation
- **Grafana**: Unified dashboards for all telemetry

---

## Additional Diagrams

### Sequence Diagram: Agent Run Flow

Shows the end-to-end flow of processing a message through an agent run.

```mermaid
sequenceDiagram
    participant SB as Azure Service Bus
    participant API as Control API
    participant Sched as Scheduler
    participant Node as Node Runtime
    participant Agent as Agent (MAF)
    participant LLM as Azure AI Foundry
    participant TargetAPI as Target Business API

    SB->>API: Queue depth notification
    API->>Sched: Create run request
    Sched->>Sched: Select node (least-loaded)
    Sched->>API: Return lease assignment
    API->>Node: Stream lease (gRPC)
    Node->>Node: Start sandbox process
    Node->>SB: Receive message
    Node->>Agent: Execute with message payload
    Agent->>LLM: LLM reasoning call
    LLM-->>Agent: Response with tool calls
    Agent->>TargetAPI: POST with idempotency key
    TargetAPI-->>Agent: 200 OK
    Agent-->>Node: Execution complete
    Node->>SB: Complete message (ack)
    Node->>API: Report run complete
    API->>Sched: Release lease
```

### Sequence Diagram: Failure and DLQ Flow

Shows how failures are handled and messages are routed to the dead-letter queue.

```mermaid
sequenceDiagram
    participant SB as Azure Service Bus
    participant Node as Node Runtime
    participant Agent as Agent (MAF)
    participant TargetAPI as Target Business API
    participant DLQ as Dead Letter Queue

    SB->>Node: Receive message
    Node->>Agent: Execute agent run
    Agent->>TargetAPI: POST /api/endpoint
    TargetAPI-->>Agent: 500 Internal Server Error
    Agent-->>Node: Retry 1/3
    Node->>Agent: Execute agent run
    Agent->>TargetAPI: POST /api/endpoint (retry)
    TargetAPI-->>Agent: 500 Internal Server Error
    Agent-->>Node: Retry 2/3
    Node->>Agent: Execute agent run
    Agent->>TargetAPI: POST /api/endpoint (retry)
    TargetAPI-->>Agent: 500 Internal Server Error
    Agent-->>Node: Retry 3/3 (failed)
    Node->>SB: Abandon message
    SB->>DLQ: Move to dead-letter queue
    Node->>API: Report run failed
```

---

## Deployment View

### Local Development (k3d)

```mermaid
graph TB
    subgraph "k3d Cluster"
        subgraph "Control Plane Namespace"
            API[Control API]
            Sched[Scheduler]
            UI[Admin UI]
            PG[PostgreSQL]
            Redis[Redis Cache]
            OTel[OTel Collector]
        end
        
        subgraph "Worker Namespace"
            Node1[Node Runtime 1]
            Node2[Node Runtime 2]
        end
        
        subgraph "Observability Namespace"
            Prom[Prometheus]
            Tempo[Tempo]
            Loki[Loki]
            Grafana[Grafana]
        end
    end
    
    subgraph "External Services"
        SB[Azure Service Bus]
        AzureAI[Azure AI Foundry]
        KC[Keycloak]
    end
    
    API --> PG
    API --> Redis
    Sched --> Redis
    Node1 --> API
    Node2 --> API
    Node1 --> SB
    Node2 --> SB
    Node1 --> AzureAI
    Node2 --> AzureAI
    API --> OTel
    Node1 --> OTel
    OTel --> Prom
    OTel --> Tempo
    OTel --> Loki
    UI --> KC
```

### Production (AKS)

```mermaid
graph TB
    subgraph "Azure"
        subgraph "AKS Cluster"
            subgraph "Control Plane"
                API[Control API<br/>2 replicas]
                Sched[Scheduler]
                UI[Admin UI]
            end
            
            subgraph "Worker Nodes"
                Node1[Node 1]
                Node2[Node 2]
                NodeN[Node N]
            end
            
            subgraph "Observability"
                OTel[OTel Collector]
                Grafana[Grafana]
            end
        end
        
        PG[Azure Database<br/>for PostgreSQL]
        Redis[Azure Cache<br/>for Redis]
        SB[Azure Service Bus]
        AzureAI[Azure AI Foundry]
        KV[Azure Key Vault]
        Monitor[Azure Monitor]
        Entra[Entra ID]
    end
    
    API --> PG
    API --> Redis
    API --> KV
    Sched --> Redis
    Node1 --> API
    Node2 --> API
    NodeN --> API
    Node1 --> SB
    Node1 --> AzureAI
    API --> OTel
    Node1 --> OTel
    OTel --> Monitor
    UI --> Entra
```

---

## Technology Stack Summary

| Layer | Technologies |
|-------|-------------|
| **Control Plane** | ASP.NET Core, gRPC, Microsoft Agent Framework SDK |
| **Worker Runtime** | .NET Worker Service, Microsoft Agent Framework |
| **Storage** | PostgreSQL, Redis |
| **Messaging** | Azure Service Bus, NATS JetStream |
| **AI/LLM** | Azure AI Foundry (GPT-4, etc.) |
| **Observability** | OpenTelemetry, Prometheus, Tempo, Loki, Grafana |
| **UI** | Next.js, React, Tailwind CSS, shadcn/ui |
| **Auth** | Keycloak (dev), Entra ID (prod), OIDC |
| **Infrastructure** | Kubernetes (k3d/AKS), Helm, Docker |
| **Secrets** | Azure Key Vault, External Secrets Operator |

---

## References

- [System Architecture Document (SAD)](../sad.md)
- [Microsoft Agent Framework Documentation](https://learn.microsoft.com/en-us/agent-framework/)
- [C4 Model](https://c4model.com/)
- [Azure AI Foundry Integration](./AZURE_AI_FOUNDRY_INTEGRATION.md)
