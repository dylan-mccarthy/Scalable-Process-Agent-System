#!/usr/bin/env bash

###############################################################################
# k3d Local Development Environment Cleanup Script
# Task: E6-T1 - Local environment
# 
# This script destroys a k3d cluster and cleans up all resources.
#
# Usage:
#   ./cleanup-k3d.sh [options]
#
# Options:
#   -c, --cluster-name NAME    Name of the k3d cluster (default: bpa-dev)
#   -f, --force                Skip confirmation prompt
#   -h, --help                 Show this help message
#
###############################################################################

set -euo pipefail

# Default configuration
CLUSTER_NAME="${CLUSTER_NAME:-bpa-dev}"
FORCE=false

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
k3d Local Development Environment Cleanup Script

This script destroys a k3d cluster and cleans up all resources.

Usage:
    ./cleanup-k3d.sh [options]

Options:
    -c, --cluster-name NAME    Name of the k3d cluster (default: bpa-dev)
    -f, --force                Skip confirmation prompt
    -h, --help                 Show this help message

Examples:
    # Basic cleanup (with confirmation)
    ./cleanup-k3d.sh

    # Cleanup with custom cluster name
    ./cleanup-k3d.sh --cluster-name my-cluster

    # Force cleanup without confirmation
    ./cleanup-k3d.sh --force

Environment Variables:
    CLUSTER_NAME      Override cluster name

EOF
}

# Parse command line arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        -c|--cluster-name)
            CLUSTER_NAME="$2"
            shift 2
            ;;
        -f|--force)
            FORCE=true
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

# Function to check if cluster exists
cluster_exists() {
    k3d cluster list | grep -q "^${CLUSTER_NAME} "
}

# Main cleanup function
cleanup() {
    print_header "k3d Cluster Cleanup: ${CLUSTER_NAME}"
    
    # Check if k3d is installed
    if ! command_exists k3d; then
        log_error "k3d is not installed. Cannot proceed with cleanup."
        exit 1
    fi
    
    # Check if cluster exists
    if ! cluster_exists; then
        log_warning "Cluster '${CLUSTER_NAME}' does not exist. Nothing to clean up."
        exit 0
    fi
    
    # Show cluster information
    log_info "Cluster information:"
    k3d cluster list | grep "^${CLUSTER_NAME} " || true
    
    # Confirmation prompt
    if [ "${FORCE}" != true ]; then
        echo ""
        log_warning "This will DELETE the k3d cluster '${CLUSTER_NAME}' and all its resources."
        echo -n "Are you sure you want to continue? (yes/no): "
        read -r response
        
        if [[ ! "${response}" =~ ^[Yy][Ee][Ss]$ ]]; then
            log_info "Cleanup cancelled."
            exit 0
        fi
    fi
    
    # Delete the cluster
    log_info "Deleting k3d cluster '${CLUSTER_NAME}'..."
    
    if k3d cluster delete "${CLUSTER_NAME}"; then
        log_success "Cluster deleted successfully"
    else
        log_error "Failed to delete cluster"
        exit 1
    fi
    
    # Clean up any leftover Docker volumes
    log_info "Cleaning up Docker volumes..."
    # Use while read loop for safe processing
    docker volume ls -q | while IFS= read -r volume; do
        if [[ "${volume}" == k3d-"${CLUSTER_NAME}"* ]]; then
            docker volume rm "${volume}" 2>/dev/null || true
        fi
    done
    
    log_success "Cleanup complete!"
}

# Main execution
main() {
    cleanup
}

# Run main function
main
