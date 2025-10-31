#!/bin/bash

################################################################################
# Invoice Classifier Demo Script (E8-T3)
#
# This script demonstrates the complete invoice classification workflow using
# the Business Process Agents platform. It showcases:
# - Agent deployment and configuration
# - End-to-end message processing
# - LLM-based classification
# - HTTP output delivery
# - Observability and monitoring
#
# Prerequisites:
# - Docker and Docker Compose installed
# - jq for JSON parsing
# - curl for API calls
# - Minimum 4GB RAM available
#
# Usage:
#   ./demo-invoice-classifier.sh
#
################################################################################

set -e

# Colors for output
GREEN='\033[0;32m'
BLUE='\033[0;34m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
CYAN='\033[0;36m'
BOLD='\033[1m'
NC='\033[0m' # No Color

# Configuration
CONTROL_PLANE_URL="${CONTROL_PLANE_URL:-http://localhost:8080}"  # Docker Compose port (local dev uses 5109)
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

# Helper functions
log_section() {
    echo ""
    echo -e "${BOLD}${CYAN}╔════════════════════════════════════════════════════════════════╗${NC}"
    echo -e "${BOLD}${CYAN}║ $1${NC}"
    echo -e "${BOLD}${CYAN}╚════════════════════════════════════════════════════════════════╝${NC}"
    echo ""
}

log_info() {
    echo -e "${BLUE}ℹ${NC} $1"
}

log_success() {
    echo -e "${GREEN}✓${NC} $1"
}

log_warning() {
    echo -e "${YELLOW}⚠${NC} $1"
}

log_error() {
    echo -e "${RED}✗${NC} $1"
}

log_step() {
    echo -e "${BOLD}$1${NC}"
}

wait_for_service() {
    local url=$1
    local service_name=$2
    local max_attempts=30
    local attempt=1

    log_info "Waiting for $service_name to be ready..."
    
    while [ $attempt -le $max_attempts ]; do
        if curl -s -f "$url" > /dev/null 2>&1; then
            log_success "$service_name is ready!"
            return 0
        fi
        echo -n "."
        sleep 2
        attempt=$((attempt + 1))
    done
    
    log_error "$service_name failed to start after $max_attempts attempts"
    return 1
}

# Check prerequisites
check_prerequisites() {
    log_section "Checking Prerequisites"
    
    local missing_deps=()
    
    if ! command -v docker &> /dev/null; then
        missing_deps+=("docker")
    fi
    
    if ! command -v docker-compose &> /dev/null && ! docker compose version &> /dev/null; then
        missing_deps+=("docker-compose")
    fi
    
    if ! command -v jq &> /dev/null; then
        missing_deps+=("jq")
    fi
    
    if ! command -v curl &> /dev/null; then
        missing_deps+=("curl")
    fi
    
    if [ ${#missing_deps[@]} -gt 0 ]; then
        log_error "Missing required dependencies: ${missing_deps[*]}"
        log_info "Please install the missing dependencies and try again."
        exit 1
    fi
    
    log_success "All prerequisites are installed"
}

# Start services
start_services() {
    log_section "Starting Services"
    
    log_step "Starting infrastructure services (PostgreSQL, Redis, NATS)..."
    docker compose up -d postgres redis nats
    
    log_info "Waiting for infrastructure services to be healthy..."
    sleep 5
    
    log_step "Starting Control Plane API..."
    docker compose up -d control-plane
    
    # Wait for Control Plane to be ready
    wait_for_service "${CONTROL_PLANE_URL}/health" "Control Plane API"
    
    log_step "Starting Node Runtime workers..."
    docker compose up -d node-runtime
    
    log_success "All services are running!"
    echo ""
    log_info "Service URLs:"
    echo "  • Control Plane API: ${CONTROL_PLANE_URL}"
    echo "  • PostgreSQL: localhost:5432"
    echo "  • Redis: localhost:6379"
    echo "  • NATS: localhost:4222"
}

# Seed the Invoice Classifier agent
seed_agent() {
    log_section "Deploying Invoice Classifier Agent"
    
    if [ ! -f "${SCRIPT_DIR}/agents/seed-invoice-classifier.sh" ]; then
        log_error "Agent seed script not found at ${SCRIPT_DIR}/agents/seed-invoice-classifier.sh"
        exit 1
    fi
    
    log_step "Running agent seed script..."
    (
        cd "${SCRIPT_DIR}/agents" && \
        CONTROL_PLANE_URL="${CONTROL_PLANE_URL}" ./seed-invoice-classifier.sh
    )
    
    log_success "Invoice Classifier agent deployed successfully"
}

# Create deployment
create_deployment() {
    log_section "Creating Agent Deployment"
    
    log_step "Creating deployment for invoice-classifier agent..."
    
    local deployment_response
    deployment_response=$(curl -s -X POST "${CONTROL_PLANE_URL}/v1/deployments" \
      -H "Content-Type: application/json" \
      -d '{
        "agentId": "invoice-classifier",
        "version": "1.0.0",
        "env": "demo",
        "target": {
          "replicas": 2,
          "placement": {}
        }
      }')
    
    local dep_id
    dep_id=$(echo "$deployment_response" | jq -r '.depId')
    
    if [ "$dep_id" == "null" ] || [ -z "$dep_id" ]; then
        log_error "Failed to create deployment"
        echo "Response: $deployment_response"
        exit 1
    fi
    
    log_success "Deployment created: $dep_id"
    echo "$dep_id" > /tmp/demo-deployment-id.txt
}

# Show fleet status
show_fleet_status() {
    log_section "Fleet Status"
    
    log_step "Checking registered nodes..."
    
    local nodes_response
    nodes_response=$(curl -s "${CONTROL_PLANE_URL}/v1/nodes")
    
    local node_count
    node_count=$(echo "$nodes_response" | jq '. | length')
    
    log_info "Registered nodes: $node_count"
    
    if [ "$node_count" -gt 0 ]; then
        echo "$nodes_response" | jq -r '.[] | "  • \(.nodeId) - \(.status.state) - \(.status.activeRuns)/\(.capacity.slots) runs"'
    fi
}

# Send demo invoices
send_demo_invoices() {
    log_section "Sending Demo Invoices"
    
    log_info "This demo uses simulated invoice processing (no actual Azure Service Bus required)"
    log_info "In production, invoices would be sent to Azure Service Bus queue"
    echo ""
    
    # Sample invoice data
    local invoices=(
        '{
            "vendorName": "Office Depot",
            "invoiceNumber": "DEMO-2024-001",
            "invoiceDate": "2024-10-30",
            "totalAmount": 542.75,
            "currency": "USD",
            "lineItems": [
                {"description": "Paper (A4, White, 500 sheets)", "quantity": 10, "unitPrice": 25.00, "total": 250.00},
                {"description": "Ballpoint Pens (Blue, Box of 50)", "quantity": 5, "unitPrice": 15.00, "total": 75.00},
                {"description": "Stapler (Heavy Duty)", "quantity": 3, "unitPrice": 18.50, "total": 55.50}
            ]
        }'
        '{
            "vendorName": "Dell Technologies",
            "invoiceNumber": "DEMO-2024-002",
            "invoiceDate": "2024-10-29",
            "totalAmount": 2899.99,
            "currency": "USD",
            "lineItems": [
                {"description": "Latitude 5540 Laptop", "quantity": 2, "unitPrice": 1199.99, "total": 2399.98},
                {"description": "USB-C Dock", "quantity": 2, "unitPrice": 250.00, "total": 500.00}
            ]
        }'
        '{
            "vendorName": "Accenture Consulting",
            "invoiceNumber": "DEMO-2024-003",
            "invoiceDate": "2024-10-28",
            "totalAmount": 15000.00,
            "currency": "USD",
            "lineItems": [
                {"description": "Strategy Consulting Services - October", "quantity": 1, "unitPrice": 15000.00, "total": 15000.00}
            ]
        }'
    )
    
    log_step "Sample invoices to process:"
    echo ""
    
    for i in "${!invoices[@]}"; do
        local invoice="${invoices[$i]}"
        local vendor_name
        local invoice_number
        local amount
        vendor_name=$(echo "$invoice" | jq -r '.vendorName')
        invoice_number=$(echo "$invoice" | jq -r '.invoiceNumber')
        amount=$(echo "$invoice" | jq -r '.totalAmount')
        
        echo -e "  ${BOLD}$((i + 1)). $vendor_name${NC}"
        echo "     Invoice: $invoice_number"
        echo "     Amount: \$$amount USD"
        echo ""
    done
    
    log_info "Expected Classifications:"
    echo "  1. Office Depot → Office Supplies → Procurement Department"
    echo "  2. Dell Technologies → Technology/Hardware → IT Department"
    echo "  3. Accenture Consulting → Professional Services → Finance Department"
}

# Show expected flow
show_expected_flow() {
    log_section "Invoice Processing Flow"
    
    cat << 'EOF'
    
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

    Process Steps:
    1. Invoice arrives in Service Bus queue
    2. Node Runtime receives message via Service Bus Input Connector
    3. Control Plane schedules run to least-loaded node
    4. Node spawns Agent.Host process with budget constraints
    5. Agent (GPT-4) classifies invoice by vendor category
    6. Agent determines routing destination (department)
    7. HTTP Output Connector sends classification to Invoice API
    8. Service Bus message is completed (or sent to DLQ on failure)
    9. Metrics and traces are exported to observability stack

EOF
}

# Show observability features
show_observability() {
    log_section "Observability Features"
    
    log_info "The system includes comprehensive observability via OpenTelemetry:"
    echo ""
    echo "  ${BOLD}Metrics:${NC}"
    echo "    • runs_started_total - Total runs initiated"
    echo "    • runs_completed_total - Successfully completed runs"
    echo "    • runs_failed_total - Failed runs"
    echo "    • run_duration_ms - End-to-end latency distribution"
    echo "    • run_tokens - Token usage per run"
    echo "    • run_cost_usd - Estimated LLM cost per run"
    echo ""
    echo "  ${BOLD}Distributed Tracing:${NC}"
    echo "    • ServiceBus.Receive - Message retrieval"
    echo "    • Scheduler.Plan - Lease assignment"
    echo "    • Agent.Execute - LLM classification"
    echo "    • Http.Post - API delivery"
    echo "    • ServiceBus.Complete - Message acknowledgment"
    echo ""
    echo "  ${BOLD}Structured Logging:${NC}"
    echo "    • JSON-formatted logs with trace correlation"
    echo "    • Run IDs, Agent IDs, and Trace IDs for correlation"
    echo "    • Performance metrics (duration, tokens, cost)"
    echo ""
    
    log_info "View logs with:"
    echo "  docker compose logs -f control-plane"
    echo "  docker compose logs -f node-runtime"
    echo ""
    
    log_info "In production, metrics are exported to:"
    echo "  • Prometheus (metrics)"
    echo "  • Tempo/Jaeger (traces)"
    echo "  • Loki (logs)"
    echo "  • Grafana (unified dashboard)"
}

# Show architecture
show_architecture() {
    log_section "System Architecture"
    
    log_info "The Business Process Agents platform consists of:"
    echo ""
    echo "  ${BOLD}Control Plane:${NC}"
    echo "    • REST API for agent/node/run management"
    echo "    • gRPC LeaseService for node communication"
    echo "    • Scheduler with least-loaded strategy"
    echo "    • PostgreSQL for persistent storage"
    echo "    • Redis for distributed leases/locks"
    echo "    • NATS JetStream for event streaming"
    echo ""
    echo "  ${BOLD}Node Runtime:${NC}"
    echo "    • Worker service that executes agents"
    echo "    • gRPC client for lease pull loop"
    echo "    • Service Bus input connector"
    echo "    • HTTP output connector"
    echo "    • Agent.Host process isolation"
    echo ""
    echo "  ${BOLD}Microsoft Agent Framework:${NC}"
    echo "    • LLM orchestration (GPT-4, Azure AI Foundry)"
    echo "    • Tool registry and execution"
    echo "    • Budget enforcement (tokens, time)"
    echo ""
}

# Show API examples
show_api_examples() {
    log_section "API Examples"
    
    log_step "1. List all agents:"
    echo "   curl ${CONTROL_PLANE_URL}/v1/agents | jq"
    echo ""
    
    log_step "2. Get agent details:"
    echo "   curl ${CONTROL_PLANE_URL}/v1/agents/invoice-classifier | jq"
    echo ""
    
    log_step "3. List deployments:"
    echo "   curl ${CONTROL_PLANE_URL}/v1/deployments | jq"
    echo ""
    
    log_step "4. List registered nodes:"
    echo "   curl ${CONTROL_PLANE_URL}/v1/nodes | jq"
    echo ""
    
    log_step "5. List recent runs:"
    echo "   curl ${CONTROL_PLANE_URL}/v1/runs | jq"
    echo ""
    
    log_info "Try these commands to explore the API!"
}

# Show next steps
show_next_steps() {
    log_section "Next Steps"
    
    log_info "To continue exploring:"
    echo ""
    echo "  ${BOLD}1. View service logs:${NC}"
    echo "     docker compose logs -f control-plane"
    echo "     docker compose logs -f node-runtime"
    echo ""
    echo "  ${BOLD}2. Access services:${NC}"
    echo "     • Control Plane API: ${CONTROL_PLANE_URL}"
    echo "     • PostgreSQL: localhost:5432 (user: postgres, db: bpa)"
    echo "     • Redis: localhost:6379"
    echo "     • NATS: localhost:4222"
    echo ""
    echo "  ${BOLD}3. Explore the Admin UI (if enabled):${NC}"
    echo "     docker compose up -d admin-ui"
    echo "     # Visit http://localhost:3000"
    echo ""
    echo "  ${BOLD}4. Run E2E tests:${NC}"
    echo "     dotnet test tests/E2E.Tests/E2E.Tests.csproj"
    echo ""
    echo "  ${BOLD}5. Deploy to Kubernetes:${NC}"
    echo "     ./infra/scripts/setup-k3d.sh"
    echo "     # or"
    echo "     helm install bpa ./helm/business-process-agents"
    echo ""
    echo "  ${BOLD}6. Configure Azure AI Foundry integration:${NC}"
    echo "     # See: docs/AZURE_AI_FOUNDRY_INTEGRATION.md"
    echo ""
}

# Cleanup function
cleanup() {
    log_section "Cleanup"
    
    log_step "Stopping all services..."
    docker compose down
    
    log_step "Removing temporary files..."
    rm -f /tmp/demo-deployment-id.txt
    
    log_success "Cleanup complete!"
    echo ""
    log_info "To remove all data (including volumes):"
    echo "  docker compose down -v"
}

# Main demo flow
main() {
    clear
    
    cat << 'EOF'
╔══════════════════════════════════════════════════════════════════════════╗
║                                                                          ║
║         Business Process Agents - Invoice Classifier Demo               ║
║                                                                          ║
║         Epic 8, Task 3: Demo Script                                     ║
║         Walkthrough invoice classification end-to-end                   ║
║                                                                          ║
╚══════════════════════════════════════════════════════════════════════════╝

EOF
    
    # Parse command line arguments
    case "${1:-}" in
        cleanup|clean|stop)
            cleanup
            exit 0
            ;;
        --help|-h)
            echo "Usage: $0 [cleanup]"
            echo ""
            echo "Options:"
            echo "  cleanup    Stop services and clean up"
            echo "  --help     Show this help message"
            exit 0
            ;;
    esac
    
    # Check prerequisites
    check_prerequisites
    
    # Show architecture overview
    show_architecture
    
    log_info "Press Enter to continue..."
    read -r
    
    # Start services
    start_services
    
    # Show expected processing flow
    show_expected_flow
    
    log_info "Press Enter to continue..."
    read -r
    
    # Seed the agent
    seed_agent
    
    # Create deployment
    create_deployment
    
    # Show fleet status
    show_fleet_status
    
    # Show demo invoices
    send_demo_invoices
    
    # Show observability features
    show_observability
    
    # Show API examples
    show_api_examples
    
    # Show next steps
    show_next_steps
    
    log_section "Demo Complete!"
    
    log_success "The Invoice Classifier agent is now deployed and ready to process invoices!"
    echo ""
    log_info "The demo environment is still running. You can:"
    echo "  • Explore the APIs using the examples above"
    echo "  • View service logs: docker compose logs -f [service-name]"
    echo "  • Run E2E tests: dotnet test tests/E2E.Tests/"
    echo ""
    log_warning "To stop and clean up the demo environment, run:"
    echo "  $0 cleanup"
    echo ""
}

# Trap errors and cleanup on exit
trap 'log_error "Demo failed! Run \"$0 cleanup\" to clean up."; exit 1' ERR

# Run main function
main "$@"
