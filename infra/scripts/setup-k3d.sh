#!/usr/bin/env bash

###############################################################################
# k3d Local Development Environment Setup Script
# Task: E6-T1 - Local environment
# 
# This script creates a local k3d cluster and deploys all core services
# for the Business Process Agents platform.
#
# Core Services:
# - PostgreSQL (database)
# - Redis (leases & locks)
# - NATS (event streaming)
# - Control Plane API
# - Node Runtime (worker nodes)
# - Admin UI
# - OpenTelemetry Collector (optional)
# - Prometheus, Tempo, Loki, Grafana (observability stack - optional)
#
# Usage:
#   ./setup-k3d.sh [options]
#
# Options:
#   -c, --cluster-name NAME    Name of the k3d cluster (default: bpa-dev)
#   -n, --namespace NAMESPACE  Kubernetes namespace (default: default)
#   -o, --observability        Enable observability stack
#   -h, --help                 Show this help message
#
###############################################################################

set -euo pipefail

# Default configuration
CLUSTER_NAME="${CLUSTER_NAME:-bpa-dev}"
NAMESPACE="${NAMESPACE:-default}"
ENABLE_OBSERVABILITY=false
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "${SCRIPT_DIR}/../.." && pwd)"
HELM_CHART_DIR="${PROJECT_ROOT}/helm/business-process-agents"

# Color codes for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Function to print colored output
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

# Function to print section headers
print_header() {
    echo ""
    echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
    echo -e "${BLUE}  $1${NC}"
    echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
}

# Function to show help
show_help() {
    cat << EOF
k3d Local Development Environment Setup Script

This script creates a local k3d cluster and deploys all core services
for the Business Process Agents platform.

Usage:
    ./setup-k3d.sh [options]

Options:
    -c, --cluster-name NAME     Name of the k3d cluster (default: bpa-dev)
    -n, --namespace NAMESPACE   Kubernetes namespace (default: default)
    -o, --observability         Enable observability stack (Prometheus, Grafana, etc.)
    -h, --help                  Show this help message

Examples:
    # Basic setup
    ./setup-k3d.sh

    # Setup with custom cluster name
    ./setup-k3d.sh --cluster-name my-cluster

    # Setup with observability stack
    ./setup-k3d.sh --observability

Environment Variables:
    CLUSTER_NAME      Override cluster name
    NAMESPACE         Override namespace

Core Services Deployed:
    - PostgreSQL (database)
    - Redis (leases & locks)
    - NATS (event streaming)
    - Control Plane API
    - Node Runtime (worker nodes)
    - Admin UI

Optional Services (--observability):
    - OpenTelemetry Collector
    - Prometheus
    - Tempo
    - Loki
    - Grafana

EOF
}

# Parse command line arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        -c|--cluster-name)
            CLUSTER_NAME="$2"
            shift 2
            ;;
        -n|--namespace)
            NAMESPACE="$2"
            shift 2
            ;;
        -o|--observability)
            ENABLE_OBSERVABILITY=true
            shift
            ;;
        -h|--help)
            show_help
            exit 0
            ;;
        *)
            log_error "Unknown option: $1"
            show_help
            exit 1
            ;;
    esac
done

# Function to check if a command exists
command_exists() {
    command -v "$1" >/dev/null 2>&1
}

# Function to check prerequisites
check_prerequisites() {
    print_header "Checking Prerequisites"
    
    local missing_deps=()
    
    # Check for required tools
    if ! command_exists docker; then
        missing_deps+=("docker")
    else
        log_success "Docker found: $(docker --version | head -n1)"
    fi
    
    if ! command_exists k3d; then
        missing_deps+=("k3d")
    else
        log_success "k3d found: $(k3d version | grep k3d | awk '{print $3}')"
    fi
    
    if ! command_exists kubectl; then
        missing_deps+=("kubectl")
    else
        log_success "kubectl found: $(kubectl version --client --short 2>/dev/null | head -n1)"
    fi
    
    if ! command_exists helm; then
        missing_deps+=("helm")
    else
        log_success "Helm found: $(helm version --short)"
    fi
    
    # Report missing dependencies
    if [ ${#missing_deps[@]} -gt 0 ]; then
        log_error "Missing required tools: ${missing_deps[*]}"
        echo ""
        echo "Installation instructions:"
        echo "  - Docker: https://docs.docker.com/get-docker/"
        echo "  - k3d: curl -s https://raw.githubusercontent.com/k3d-io/k3d/main/install.sh | bash"
        echo "  - kubectl: https://kubernetes.io/docs/tasks/tools/"
        echo "  - Helm: curl https://raw.githubusercontent.com/helm/helm/main/scripts/get-helm-3 | bash"
        exit 1
    fi
    
    # Check if Docker is running
    if ! docker ps >/dev/null 2>&1; then
        log_error "Docker daemon is not running. Please start Docker."
        exit 1
    fi
    log_success "Docker daemon is running"
}

# Function to check if cluster exists
cluster_exists() {
    k3d cluster list | grep -q "^${CLUSTER_NAME} "
}

# Function to create k3d cluster
create_cluster() {
    print_header "Creating k3d Cluster: ${CLUSTER_NAME}"
    
    if cluster_exists; then
        log_warning "Cluster '${CLUSTER_NAME}' already exists. Skipping creation."
        return 0
    fi
    
    log_info "Creating k3d cluster with the following configuration:"
    echo "  - Cluster name: ${CLUSTER_NAME}"
    echo "  - Servers: 1"
    echo "  - Agents: 2"
    echo "  - Port mappings:"
    echo "    - 8080:80@loadbalancer (HTTP)"
    echo "    - 8443:443@loadbalancer (HTTPS)"
    echo "    - 9090:9090@loadbalancer (Prometheus)"
    echo "    - 3000:3000@loadbalancer (Grafana)"
    
    k3d cluster create "${CLUSTER_NAME}" \
        --servers 1 \
        --agents 2 \
        --port "8080:80@loadbalancer" \
        --port "8443:443@loadbalancer" \
        --port "9090:9090@loadbalancer" \
        --port "3000:3000@loadbalancer" \
        --wait
    
    log_success "Cluster created successfully"
    
    # Verify cluster is ready
    log_info "Waiting for cluster to be ready..."
    kubectl wait --for=condition=Ready nodes --all --timeout=120s
    log_success "All nodes are ready"
}

# Function to create namespace if needed
create_namespace() {
    if [ "${NAMESPACE}" != "default" ]; then
        print_header "Creating Namespace: ${NAMESPACE}"
        
        if kubectl get namespace "${NAMESPACE}" >/dev/null 2>&1; then
            log_warning "Namespace '${NAMESPACE}' already exists. Skipping creation."
        else
            kubectl create namespace "${NAMESPACE}"
            log_success "Namespace created successfully"
        fi
    fi
}

# Function to deploy the application using Helm
deploy_application() {
    print_header "Deploying Business Process Agents Platform"
    
    if [ ! -d "${HELM_CHART_DIR}" ]; then
        log_error "Helm chart not found at: ${HELM_CHART_DIR}"
        exit 1
    fi
    
    log_info "Installing Helm chart from: ${HELM_CHART_DIR}"
    
    # Prepare Helm values
    local helm_values=""
    
    # Use k3d-specific values file if it exists
    local k3d_values_file="${PROJECT_ROOT}/infra/helm/values-k3d.yaml"
    if [ -f "${k3d_values_file}" ]; then
        helm_values="-f ${k3d_values_file}"
        log_info "Using k3d values file: ${k3d_values_file}"
    fi
    
    # Set appropriate values for k3d environment
    local set_values=(
        "--set controlPlane.replicaCount=1"
        "--set nodeRuntime.replicaCount=2"
        "--set controlPlane.image.pullPolicy=IfNotPresent"
        "--set nodeRuntime.image.pullPolicy=IfNotPresent"
        "--set adminUI.image.pullPolicy=IfNotPresent"
        "--set postgresql.persistence.size=5Gi"
        "--set redis.persistence.size=1Gi"
        "--set nats.persistence.size=1Gi"
    )
    
    # Enable observability if requested
    if [ "${ENABLE_OBSERVABILITY}" = true ]; then
        log_info "Enabling observability stack"
        set_values+=(
            "--set otelCollector.enabled=true"
            "--set prometheus.enabled=true"
            "--set tempo.enabled=true"
            "--set loki.enabled=true"
            "--set grafana.enabled=true"
        )
    fi
    
    # Install or upgrade the Helm release
    log_info "Installing/upgrading Helm release 'bpa' in namespace '${NAMESPACE}'..."
    
    # shellcheck disable=SC2086
    helm upgrade --install bpa "${HELM_CHART_DIR}" \
        --namespace "${NAMESPACE}" \
        --create-namespace \
        ${helm_values} \
        "${set_values[@]}" \
        --wait \
        --timeout 10m
    
    log_success "Application deployed successfully"
}

# Function to wait for pods to be ready
wait_for_pods() {
    print_header "Waiting for Pods to be Ready"
    
    log_info "Waiting for all pods to be ready (timeout: 5 minutes)..."
    
    if kubectl wait --for=condition=ready pod \
        --selector app.kubernetes.io/instance=bpa \
        --namespace "${NAMESPACE}" \
        --timeout=300s; then
        log_success "All pods are ready"
    else
        log_warning "Some pods may not be ready yet. Checking status..."
        kubectl get pods -n "${NAMESPACE}" -l app.kubernetes.io/instance=bpa
    fi
}

# Function to display access information
display_access_info() {
    print_header "Access Information"
    
    echo ""
    echo "Cluster Information:"
    echo "  - Cluster name: ${CLUSTER_NAME}"
    echo "  - Namespace: ${NAMESPACE}"
    echo ""
    
    echo "Service Endpoints:"
    echo "  - Control Plane API: http://localhost:8080"
    echo "  - Admin UI: http://localhost:3000"
    
    if [ "${ENABLE_OBSERVABILITY}" = true ]; then
        echo "  - Grafana: http://localhost:3000 (observability)"
        echo "  - Prometheus: http://localhost:9090"
    fi
    
    echo ""
    echo "Useful Commands:"
    echo "  - View pods: kubectl get pods -n ${NAMESPACE}"
    echo "  - View logs (Control Plane): kubectl logs -n ${NAMESPACE} -l app.kubernetes.io/component=control-plane -f"
    echo "  - View logs (Node Runtime): kubectl logs -n ${NAMESPACE} -l app.kubernetes.io/component=node-runtime -f"
    echo "  - Port forward (if needed): kubectl port-forward -n ${NAMESPACE} svc/bpa-business-process-agents-control-plane 8080:8080"
    echo ""
    
    if [ "${ENABLE_OBSERVABILITY}" = true ]; then
        echo "Observability:"
        echo "  - Grafana credentials: admin / admin (change on first login)"
        echo "  - Prometheus UI: http://localhost:9090"
        echo ""
    fi
    
    echo "To access the cluster in the future:"
    echo "  kubectl config use-context k3d-${CLUSTER_NAME}"
    echo ""
    
    echo "To destroy the cluster:"
    echo "  ./cleanup-k3d.sh --cluster-name ${CLUSTER_NAME}"
    echo "  or: k3d cluster delete ${CLUSTER_NAME}"
    echo ""
}

# Function to perform health check
health_check() {
    print_header "Health Check"
    
    log_info "Checking service health..."
    
    # Check if Control Plane API is responding
    local max_retries=30
    local retry_count=0
    
    while [ $retry_count -lt $max_retries ]; do
        if curl -s -o /dev/null -w "%{http_code}" http://localhost:8080/health 2>/dev/null | grep -q "200"; then
            log_success "Control Plane API is healthy"
            break
        fi
        
        retry_count=$((retry_count + 1))
        if [ $retry_count -eq $max_retries ]; then
            log_warning "Control Plane API health check timed out. It may still be starting up."
            log_info "Check logs with: kubectl logs -n ${NAMESPACE} -l app.kubernetes.io/component=control-plane"
        else
            sleep 2
        fi
    done
    
    # Display pod status
    echo ""
    log_info "Current pod status:"
    kubectl get pods -n "${NAMESPACE}" -l app.kubernetes.io/instance=bpa
}

# Main execution
main() {
    print_header "Business Process Agents - k3d Local Environment Setup"
    
    echo "Configuration:"
    echo "  - Cluster: ${CLUSTER_NAME}"
    echo "  - Namespace: ${NAMESPACE}"
    echo "  - Observability: ${ENABLE_OBSERVABILITY}"
    
    # Run setup steps
    check_prerequisites
    create_cluster
    create_namespace
    deploy_application
    wait_for_pods
    health_check
    display_access_info
    
    log_success "Setup complete! Your local k3d environment is ready."
}

# Run main function
main
