#!/bin/bash
# Test Bicep Template Validation
# This script validates the Bicep template without deploying it

set -e

# Colors
GREEN='\033[0;32m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
NC='\033[0m'

print_test() {
    echo -e "${YELLOW}[TEST]${NC} $1"
}

print_pass() {
    echo -e "${GREEN}[PASS]${NC} $1"
}

print_fail() {
    echo -e "${RED}[FAIL]${NC} $1"
}

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
BICEP_DIR="$SCRIPT_DIR/../bicep"
TEST_FAILURES=0

echo "======================================="
echo "Bicep Template Validation Tests"
echo "======================================="
echo ""

# Test 1: Check if Bicep CLI is available
print_test "Checking for Bicep CLI..."
if command -v az bicep &> /dev/null; then
    print_pass "Bicep CLI is available"
else
    print_fail "Bicep CLI not found"
    ((TEST_FAILURES++))
fi
echo ""

# Test 2: Build Bicep template
print_test "Building main.bicep template..."
if az bicep build --file "$BICEP_DIR/main.bicep" &> /dev/null; then
    print_pass "Bicep template builds successfully"
    # Clean up generated ARM template
    rm -f "$BICEP_DIR/main.json"
else
    print_fail "Bicep template build failed"
    ((TEST_FAILURES++))
fi
echo ""

# Test 3: Validate parameter files
for env in dev prod; do
    print_test "Validating $env parameter file..."
    PARAM_FILE="$BICEP_DIR/main.parameters.$env.json"
    
    if [ ! -f "$PARAM_FILE" ]; then
        print_fail "Parameter file not found: $PARAM_FILE"
        ((TEST_FAILURES++))
        continue
    fi
    
    # Check JSON syntax
    if jq empty "$PARAM_FILE" 2>/dev/null; then
        print_pass "Parameter file $env is valid JSON"
    else
        print_fail "Parameter file $env has invalid JSON syntax"
        ((TEST_FAILURES++))
    fi
done
echo ""

# Test 4: Check required parameters
print_test "Checking required parameters in dev config..."
REQUIRED_PARAMS=(
    "location"
    "environment"
    "baseName"
    "aksConfig"
    "postgresConfig"
    "redisConfig"
    "openAiConfig"
)

for param in "${REQUIRED_PARAMS[@]}"; do
    if jq -e ".parameters.$param" "$BICEP_DIR/main.parameters.dev.json" &> /dev/null; then
        print_pass "Parameter '$param' is defined"
    else
        print_fail "Required parameter '$param' is missing"
        ((TEST_FAILURES++))
    fi
done
echo ""

# Test 5: Check Bicep syntax and linting
print_test "Running Bicep linter..."
LINT_OUTPUT=$(az bicep build --file "$BICEP_DIR/main.bicep" 2>&1)
if echo "$LINT_OUTPUT" | grep -i "warning" > /dev/null; then
    print_fail "Bicep template has linting warnings:"
    echo "$LINT_OUTPUT" | grep -i "warning"
    ((TEST_FAILURES++))
else
    print_pass "No linting warnings found"
fi
echo ""

# Test 6: Verify script permissions
print_test "Checking deployment script permissions..."
DEPLOY_SCRIPT="$SCRIPT_DIR/deploy-azure.sh"
if [ -x "$DEPLOY_SCRIPT" ]; then
    print_pass "Deployment script is executable"
else
    print_fail "Deployment script is not executable"
    ((TEST_FAILURES++))
fi
echo ""

# Test 7: Check if deployment script has required structure
print_test "Validating deployment script structure..."
REQUIRED_FUNCTIONS=(
    "print_info"
    "print_error"
    "usage"
)

for func in "${REQUIRED_FUNCTIONS[@]}"; do
    if grep -q "^$func()" "$DEPLOY_SCRIPT" || grep -q "^function $func" "$DEPLOY_SCRIPT"; then
        print_pass "Function '$func' exists in deployment script"
    else
        print_fail "Function '$func' not found in deployment script"
        ((TEST_FAILURES++))
    fi
done
echo ""

# Test 8: Validate Helm values files
for env in dev prod; do
    print_test "Validating Helm values for $env environment..."
    HELM_VALUES="$SCRIPT_DIR/../helm/values-aks-$env.yaml"
    
    if [ ! -f "$HELM_VALUES" ]; then
        print_fail "Helm values file not found: $HELM_VALUES"
        ((TEST_FAILURES++))
        continue
    fi
    
    # Check YAML syntax
    if python3 -c "import yaml; yaml.safe_load(open('$HELM_VALUES'))" 2>/dev/null; then
        print_pass "Helm values file for $env is valid YAML"
    else
        print_fail "Helm values file for $env has invalid YAML syntax"
        ((TEST_FAILURES++))
    fi
done
echo ""

# Test 9: Check if README exists and has required sections
print_test "Validating infrastructure documentation..."
README="$SCRIPT_DIR/../README.md"
REQUIRED_SECTIONS=(
    "Prerequisites"
    "Quick Start"
    "Deployment"
    "Configuration"
)

for section in "${REQUIRED_SECTIONS[@]}"; do
    if grep -qi "## $section" "$README" || grep -qi "# $section" "$README"; then
        print_pass "Documentation section '$section' exists"
    else
        print_fail "Documentation section '$section' not found"
        ((TEST_FAILURES++))
    fi
done
echo ""

# Summary
echo "======================================="
echo "Test Summary"
echo "======================================="
if [ $TEST_FAILURES -eq 0 ]; then
    echo -e "${GREEN}All tests passed!${NC}"
    exit 0
else
    echo -e "${RED}$TEST_FAILURES test(s) failed${NC}"
    exit 1
fi
