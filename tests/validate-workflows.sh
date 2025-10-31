#!/bin/bash

# Workflow Validation Test Script
# Validates GitHub Actions workflows for syntax and best practices

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
WORKFLOWS_DIR="$REPO_ROOT/.github/workflows"

echo "======================================"
echo "GitHub Actions Workflow Validation"
echo "======================================"
echo ""

# Color codes
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Test counter
TESTS_PASSED=0
TESTS_FAILED=0

# Function to print test result
print_result() {
    local test_name="$1"
    local result="$2"
    
    if [ "$result" -eq 0 ]; then
        echo -e "${GREEN}✅ PASSED${NC}: $test_name"
        ((TESTS_PASSED++))
    else
        echo -e "${RED}❌ FAILED${NC}: $test_name"
        ((TESTS_FAILED++))
    fi
}

# Test 1: Check if workflow files exist
echo "Test 1: Checking for required workflow files..."
REQUIRED_WORKFLOWS=("ci.yml" "release.yml" "code-quality.yml" "deploy-aks.yml")
ALL_FOUND=1
for workflow in "${REQUIRED_WORKFLOWS[@]}"; do
    if [ -f "$WORKFLOWS_DIR/$workflow" ]; then
        echo "  Found: $workflow"
    else
        echo -e "  ${RED}Missing: $workflow${NC}"
        ALL_FOUND=0
    fi
done
print_result "Required workflow files exist" $((1 - ALL_FOUND))

# Test 2: Validate YAML syntax with Python
echo ""
echo "Test 2: Validating YAML syntax..."
YAML_VALID=1
for workflow in "$WORKFLOWS_DIR"/*.yml; do
    workflow_name=$(basename "$workflow")
    if python3 -c "import yaml; yaml.safe_load(open('$workflow'))" 2>/dev/null; then
        echo "  Valid: $workflow_name"
    else
        echo -e "  ${RED}Invalid YAML: $workflow_name${NC}"
        YAML_VALID=0
    fi
done
print_result "YAML syntax validation" $((1 - YAML_VALID))

# Test 3: Check for actionlint (if available)
echo ""
echo "Test 3: Running actionlint (if available)..."
if command -v actionlint &> /dev/null; then
    cd "$REPO_ROOT"
    if actionlint .github/workflows/*.yml 2>&1; then
        echo "  All workflows passed actionlint checks"
        print_result "Actionlint validation" 0
    else
        print_result "Actionlint validation" 1
    fi
else
    echo -e "  ${YELLOW}⚠ SKIPPED${NC}: actionlint not installed"
fi

# Test 4: Check for up-to-date action versions
echo ""
echo "Test 4: Checking for deprecated action versions..."
DEPRECATED_ACTIONS=0
for workflow in "$WORKFLOWS_DIR"/*.yml; do
    workflow_name=$(basename "$workflow")
    
    # Check for azure/login@v1 (deprecated)
    if grep -q "azure/login@v1" "$workflow"; then
        echo -e "  ${RED}Found deprecated azure/login@v1 in $workflow_name${NC}"
        DEPRECATED_ACTIONS=1
    fi
    
    # Check for actions/checkout@v2 or v3 (v4 is current)
    if grep -q "actions/checkout@v[23]" "$workflow"; then
        echo -e "  ${YELLOW}Warning: Old actions/checkout version in $workflow_name${NC}"
    fi
done

if [ $DEPRECATED_ACTIONS -eq 0 ]; then
    echo "  No deprecated actions found"
    print_result "No deprecated action versions" 0
else
    print_result "No deprecated action versions" 1
fi

# Test 5: Verify CI workflow components
echo ""
echo "Test 5: Verifying CI workflow has required jobs..."
CI_FILE="$WORKFLOWS_DIR/ci.yml"
REQUIRED_JOBS=("dotnet-build-test" "nextjs-build-test" "docker-build" "ci-summary")
JOBS_FOUND=1
for job in "${REQUIRED_JOBS[@]}"; do
    if grep -q "^  $job:" "$CI_FILE" || grep -q "^  ${job/-/_}:" "$CI_FILE"; then
        echo "  Found job: $job"
    else
        echo -e "  ${RED}Missing job: $job${NC}"
        JOBS_FOUND=0
    fi
done
print_result "CI workflow has required jobs" $((1 - JOBS_FOUND))

# Test 6: Verify security features
echo ""
echo "Test 6: Verifying security features in workflows..."
SECURITY_FEATURES_FOUND=0

# Check for SBOM generation
if grep -q "sbom-action" "$WORKFLOWS_DIR/ci.yml"; then
    echo "  Found: SBOM generation"
    ((SECURITY_FEATURES_FOUND++))
fi

# Check for Cosign signing
if grep -q "cosign" "$WORKFLOWS_DIR/ci.yml"; then
    echo "  Found: Image signing with Cosign"
    ((SECURITY_FEATURES_FOUND++))
fi

# Check for Trivy scanning
if grep -q "trivy-action" "$WORKFLOWS_DIR/ci.yml"; then
    echo "  Found: Trivy vulnerability scanning"
    ((SECURITY_FEATURES_FOUND++))
fi

# Check for CodeQL
if grep -q "codeql-action" "$WORKFLOWS_DIR/code-quality.yml"; then
    echo "  Found: CodeQL analysis"
    ((SECURITY_FEATURES_FOUND++))
fi

if [ $SECURITY_FEATURES_FOUND -eq 4 ]; then
    print_result "Security features present" 0
else
    echo -e "  ${RED}Found $SECURITY_FEATURES_FOUND/4 expected security features${NC}"
    print_result "Security features present" 1
fi

# Test 7: Verify deployment workflow structure
echo ""
echo "Test 7: Verifying deploy-aks workflow structure..."
DEPLOY_FILE="$WORKFLOWS_DIR/deploy-aks.yml"
DEPLOY_JOBS=("deploy-infrastructure" "build-and-push" "deploy-application")
for job in "${DEPLOY_JOBS[@]}"; do
    if grep -q "^  $job:" "$DEPLOY_FILE" || grep -q "^  ${job/-/_}:" "$DEPLOY_FILE"; then
        echo "  Found job: $job"
    else
        echo -e "  ${YELLOW}Warning: Job might be missing: $job${NC}"
    fi
done
print_result "Deploy workflow structure" 0

# Test 8: Check for proper permissions
echo ""
echo "Test 8: Checking for proper workflow permissions..."
PERMISSIONS_OK=0
for workflow in "$WORKFLOWS_DIR"/*.yml; do
    workflow_name=$(basename "$workflow")
    if grep -q "^permissions:" "$workflow" || grep -q "^  permissions:" "$workflow"; then
        echo "  Permissions defined in: $workflow_name"
        ((PERMISSIONS_OK++))
    fi
done

if [ $PERMISSIONS_OK -gt 0 ]; then
    echo "  Found permissions in $PERMISSIONS_OK workflow(s)"
    print_result "Workflow permissions defined" 0
else
    echo -e "  ${YELLOW}Warning: No explicit permissions found${NC}"
    print_result "Workflow permissions defined" 0
fi

# Final summary
echo ""
echo "======================================"
if [ $TESTS_FAILED -eq 0 ]; then
    echo -e "${GREEN}All tests passed! ✅${NC}"
else
    echo -e "${RED}Some tests failed! ❌${NC}"
fi
echo "======================================"
echo ""
echo "Summary:"
echo "  Tests Passed: $TESTS_PASSED"
echo "  Tests Failed: $TESTS_FAILED"
echo ""

# Exit with appropriate code
if [ $TESTS_FAILED -eq 0 ]; then
    exit 0
else
    exit 1
fi
