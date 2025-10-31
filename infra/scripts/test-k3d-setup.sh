#!/usr/bin/env bash

###############################################################################
# k3d Setup Integration Test
# Task: E6-T1 - Local environment
# 
# This script tests the k3d setup by creating a cluster, deploying services,
# and verifying they are working correctly.
#
# Usage:
#   ./test-k3d-setup.sh
#
###############################################################################

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
TEST_CLUSTER_NAME="bpa-test-$(date +%s)"
NAMESPACE="default"

# Color codes for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Test results
TESTS_PASSED=0
TESTS_FAILED=0

log_info() {
    echo -e "${BLUE}ℹ${NC} $1"
}

log_success() {
    echo -e "${GREEN}✓${NC} $1"
    TESTS_PASSED=$((TESTS_PASSED + 1))
}

log_error() {
    echo -e "${RED}✗${NC} $1"
    TESTS_FAILED=$((TESTS_FAILED + 1))
}

print_header() {
    echo ""
    echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
    echo -e "${BLUE}  $1${NC}"
    echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
}

# Cleanup function
cleanup() {
    print_header "Cleanup"
    log_info "Cleaning up test cluster: ${TEST_CLUSTER_NAME}"
    
    if k3d cluster list | grep -q "^${TEST_CLUSTER_NAME} "; then
        k3d cluster delete "${TEST_CLUSTER_NAME}" >/dev/null 2>&1 || true
        log_success "Test cluster deleted"
    fi
}

# Trap cleanup on exit
trap cleanup EXIT

# Test 1: Setup script exists and is executable
test_setup_script_exists() {
    print_header "Test 1: Setup Script Exists"
    
    if [ -f "${SCRIPT_DIR}/setup-k3d.sh" ]; then
        log_success "setup-k3d.sh exists"
    else
        log_error "setup-k3d.sh not found"
        return 1
    fi
    
    if [ -x "${SCRIPT_DIR}/setup-k3d.sh" ]; then
        log_success "setup-k3d.sh is executable"
    else
        log_error "setup-k3d.sh is not executable"
        return 1
    fi
}

# Test 2: Cleanup script exists and is executable
test_cleanup_script_exists() {
    print_header "Test 2: Cleanup Script Exists"
    
    if [ -f "${SCRIPT_DIR}/cleanup-k3d.sh" ]; then
        log_success "cleanup-k3d.sh exists"
    else
        log_error "cleanup-k3d.sh not found"
        return 1
    fi
    
    if [ -x "${SCRIPT_DIR}/cleanup-k3d.sh" ]; then
        log_success "cleanup-k3d.sh is executable"
    else
        log_error "cleanup-k3d.sh is not executable"
        return 1
    fi
}

# Test 3: k3d values file exists
test_k3d_values_exists() {
    print_header "Test 3: k3d Values File Exists"
    
    if [ -f "${SCRIPT_DIR}/../helm/values-k3d.yaml" ]; then
        log_success "values-k3d.yaml exists"
    else
        log_error "values-k3d.yaml not found"
        return 1
    fi
}

# Test 4: Helm chart exists
test_helm_chart_exists() {
    print_header "Test 4: Helm Chart Exists"
    
    if [ -f "${SCRIPT_DIR}/../../helm/business-process-agents/Chart.yaml" ]; then
        log_success "Helm chart exists"
    else
        log_error "Helm chart not found"
        return 1
    fi
}

# Test 5: Setup script can be invoked with --help
test_setup_help() {
    print_header "Test 5: Setup Script Help"
    
    if "${SCRIPT_DIR}/setup-k3d.sh" --help >/dev/null 2>&1; then
        log_success "Setup script --help works"
    else
        log_error "Setup script --help failed"
        return 1
    fi
}

# Test 6: Cleanup script can be invoked with --help
test_cleanup_help() {
    print_header "Test 6: Cleanup Script Help"
    
    if "${SCRIPT_DIR}/cleanup-k3d.sh" --help >/dev/null 2>&1; then
        log_success "Cleanup script --help works"
    else
        log_error "Cleanup script --help failed"
        return 1
    fi
}

# Test 7: k3d values file is valid YAML
test_k3d_values_valid() {
    print_header "Test 7: k3d Values File Valid YAML"
    
    # Check if yq is available
    if command -v yq >/dev/null 2>&1; then
        if yq eval '.' "${SCRIPT_DIR}/../helm/values-k3d.yaml" >/dev/null 2>&1; then
            log_success "values-k3d.yaml is valid YAML"
        else
            log_error "values-k3d.yaml is invalid YAML"
            return 1
        fi
    else
        # Fallback to basic YAML check with Python
        if python3 -c "import yaml; yaml.safe_load(open('${SCRIPT_DIR}/../helm/values-k3d.yaml'))" 2>/dev/null; then
            log_success "values-k3d.yaml is valid YAML"
        else
            log_error "values-k3d.yaml is invalid YAML (install yq or python3-yaml for better validation)"
            return 1
        fi
    fi
}

# Test 8: README exists
test_readme_exists() {
    print_header "Test 8: README Documentation Exists"
    
    if [ -f "${SCRIPT_DIR}/README.md" ]; then
        log_success "README.md exists"
    else
        log_error "README.md not found"
        return 1
    fi
}

# Test 9: Full setup (optional - requires Docker/k3d)
test_full_setup() {
    print_header "Test 9: Full Setup Test (Optional)"
    
    # Skip if prerequisites are not available
    if ! command -v docker >/dev/null 2>&1; then
        log_info "Docker not available, skipping full setup test"
        return 0
    fi
    
    if ! command -v k3d >/dev/null 2>&1; then
        log_info "k3d not available, skipping full setup test"
        return 0
    fi
    
    if ! command -v kubectl >/dev/null 2>&1; then
        log_info "kubectl not available, skipping full setup test"
        return 0
    fi
    
    if ! command -v helm >/dev/null 2>&1; then
        log_info "helm not available, skipping full setup test"
        return 0
    fi
    
    if ! docker ps >/dev/null 2>&1; then
        log_info "Docker daemon not running, skipping full setup test"
        return 0
    fi
    
    log_info "All prerequisites available, running full setup test..."
    log_info "This may take several minutes..."
    
    # Run setup script
    export CLUSTER_NAME="${TEST_CLUSTER_NAME}"
    if "${SCRIPT_DIR}/setup-k3d.sh" >/dev/null 2>&1; then
        log_success "Setup script completed successfully"
    else
        log_error "Setup script failed"
        return 1
    fi
    
    # Verify cluster exists
    if k3d cluster list | grep -q "^${TEST_CLUSTER_NAME} "; then
        log_success "Cluster created successfully"
    else
        log_error "Cluster not found"
        return 1
    fi
    
    # Verify pods are running
    if kubectl get pods -n "${NAMESPACE}" -l app.kubernetes.io/instance=bpa >/dev/null 2>&1; then
        log_success "Pods are deployed"
        
        # Count running pods
        local running_pods=$(kubectl get pods -n "${NAMESPACE}" -l app.kubernetes.io/instance=bpa --field-selector=status.phase=Running --no-headers 2>/dev/null | wc -l)
        log_info "Running pods: ${running_pods}"
        
        if [ "${running_pods}" -gt 0 ]; then
            log_success "At least one pod is running"
        else
            log_error "No pods are running"
            return 1
        fi
    else
        log_error "Failed to get pods"
        return 1
    fi
}

# Main test execution
main() {
    print_header "k3d Setup Integration Tests"
    
    echo "Test Cluster: ${TEST_CLUSTER_NAME}"
    echo ""
    
    # Run tests
    test_setup_script_exists || true
    test_cleanup_script_exists || true
    test_k3d_values_exists || true
    test_helm_chart_exists || true
    test_setup_help || true
    test_cleanup_help || true
    test_k3d_values_valid || true
    test_readme_exists || true
    
    # Full setup test (optional)
    if [ "${SKIP_FULL_TEST:-false}" != "true" ]; then
        test_full_setup || true
    else
        log_info "Skipping full setup test (SKIP_FULL_TEST=true)"
    fi
    
    # Print results
    print_header "Test Results"
    
    local total_tests=$((TESTS_PASSED + TESTS_FAILED))
    echo ""
    echo "Tests Passed: ${TESTS_PASSED}/${total_tests}"
    echo "Tests Failed: ${TESTS_FAILED}/${total_tests}"
    echo ""
    
    if [ ${TESTS_FAILED} -eq 0 ]; then
        log_success "All tests passed!"
        return 0
    else
        log_error "Some tests failed!"
        return 1
    fi
}

# Run main function
main
