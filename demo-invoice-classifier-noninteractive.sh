#!/bin/bash

################################################################################
# Invoice Classifier Demo - Non-Interactive Version
#
# This script runs the demo in non-interactive mode, suitable for:
# - CI/CD pipelines
# - Automated testing
# - Quick validation
#
# Usage:
#   ./demo-invoice-classifier-noninteractive.sh
#
################################################################################

set -e

# Colors for output
GREEN='\033[0;32m'
BLUE='\033[0;34m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
NC='\033[0m'

# Configuration
CONTROL_PLANE_URL="${CONTROL_PLANE_URL:-http://localhost:8080}"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
TIMEOUT=120

log_info() {
    echo -e "${BLUE}[INFO]${NC} $1"
}

log_success() {
    echo -e "${GREEN}[SUCCESS]${NC} $1"
}

log_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

wait_for_service() {
    local url=$1
    local service_name=$2
    local max_attempts=60
    local attempt=1

    log_info "Waiting for $service_name to be ready (timeout: ${max_attempts}s)..."
    
    while [ $attempt -le $max_attempts ]; do
        if curl -s -f "$url" > /dev/null 2>&1; then
            log_success "$service_name is ready!"
            return 0
        fi
        sleep 1
        attempt=$((attempt + 1))
    done
    
    log_error "$service_name failed to start after $max_attempts attempts"
    return 1
}

# Check prerequisites
log_info "Checking prerequisites..."
for cmd in docker jq curl; do
    if ! command -v $cmd &> /dev/null; then
        log_error "Required command not found: $cmd"
        exit 1
    fi
done
log_success "Prerequisites OK"

# Start services
log_info "Starting infrastructure services..."
docker compose up -d postgres redis nats

log_info "Waiting for infrastructure to be healthy..."
sleep 10

log_info "Starting Control Plane..."
docker compose up -d control-plane

# Wait for Control Plane
if ! wait_for_service "${CONTROL_PLANE_URL}/health" "Control Plane API"; then
    log_error "Control Plane failed to start"
    docker compose logs control-plane
    exit 1
fi

log_info "Starting Node Runtime..."
docker compose up -d node-runtime

sleep 5

# Seed agent
log_info "Seeding Invoice Classifier agent..."
if [ -f "${SCRIPT_DIR}/agents/seed-invoice-classifier.sh" ]; then
    cd "${SCRIPT_DIR}/agents"
    CONTROL_PLANE_URL="${CONTROL_PLANE_URL}" ./seed-invoice-classifier.sh > /dev/null 2>&1
    cd "${SCRIPT_DIR}"
    log_success "Agent seeded"
else
    log_error "Seed script not found"
    exit 1
fi

# Create deployment
log_info "Creating deployment..."
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

dep_id=$(echo "$deployment_response" | jq -r '.depId')
if [ "$dep_id" == "null" ] || [ -z "$dep_id" ]; then
    log_error "Failed to create deployment"
    echo "$deployment_response"
    exit 1
fi
log_success "Deployment created: $dep_id"

# Verify nodes
log_info "Checking node registration..."
nodes=$(curl -s "${CONTROL_PLANE_URL}/v1/nodes")
node_count=$(echo "$nodes" | jq '. | length')
log_info "Registered nodes: $node_count"

if [ "$node_count" -eq 0 ]; then
    log_error "No nodes registered!"
    exit 1
fi

# Verify agent
log_info "Verifying agent deployment..."
agent=$(curl -s "${CONTROL_PLANE_URL}/v1/agents/invoice-classifier")
agent_name=$(echo "$agent" | jq -r '.name')

if [ "$agent_name" != "Invoice Classifier" ]; then
    log_error "Agent verification failed"
    exit 1
fi
log_success "Agent deployed: $agent_name"

# Success
log_success "Demo environment is ready!"
echo ""
echo "Access points:"
echo "  • Control Plane API: ${CONTROL_PLANE_URL}"
echo "  • Health check: ${CONTROL_PLANE_URL}/health"
echo ""
echo "Test commands:"
echo "  curl ${CONTROL_PLANE_URL}/v1/agents | jq"
echo "  curl ${CONTROL_PLANE_URL}/v1/nodes | jq"
echo "  curl ${CONTROL_PLANE_URL}/v1/deployments | jq"
echo ""
echo "View logs:"
echo "  docker compose logs -f control-plane"
echo "  docker compose logs -f node-runtime"
echo ""
echo "Cleanup:"
echo "  docker compose down"
