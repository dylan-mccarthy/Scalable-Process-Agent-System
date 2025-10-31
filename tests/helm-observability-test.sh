#!/bin/bash
# Integration test for OTel Collector deployment with Prometheus, Tempo, and Loki exporters
# This test validates the Helm chart generates all required observability components

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
HELM_CHART_PATH="$PROJECT_ROOT/helm/business-process-agents"
VALUES_FILE="$HELM_CHART_PATH/examples/values-observability.yaml"

echo "======================================"
echo "OTel Collector Deployment Test"
echo "======================================"
echo ""

# Test 1: Validate Helm chart can be templated
echo "Test 1: Validating Helm chart template rendering..."
TEMPLATE_OUTPUT=$(helm template test-release "$HELM_CHART_PATH" --values "$VALUES_FILE" 2>&1)
if [ $? -ne 0 ]; then
    echo "❌ FAILED: Helm template rendering failed"
    echo "$TEMPLATE_OUTPUT"
    exit 1
fi
echo "✅ PASSED: Helm chart templates successfully"
echo ""

# Test 2: Verify OTel Collector Deployment exists
echo "Test 2: Verifying OTel Collector Deployment..."
if echo "$TEMPLATE_OUTPUT" | grep -q "kind: Deployment" && echo "$TEMPLATE_OUTPUT" | grep -q "name: test-release-business-process-agents-otel-collector"; then
    echo "✅ PASSED: OTel Collector Deployment found"
else
    echo "❌ FAILED: OTel Collector Deployment not found"
    exit 1
fi
echo ""

# Test 3: Verify OTel Collector Service exists
echo "Test 3: Verifying OTel Collector Service..."
if echo "$TEMPLATE_OUTPUT" | grep -q "kind: Service" && echo "$TEMPLATE_OUTPUT" | grep -q "name: test-release-business-process-agents-otel-collector"; then
    echo "✅ PASSED: OTel Collector Service found"
else
    echo "❌ FAILED: OTel Collector Service not found"
    exit 1
fi
echo ""

# Test 4: Verify OTel Collector ConfigMap with exporters
echo "Test 4: Verifying OTel Collector ConfigMap with exporters..."
if echo "$TEMPLATE_OUTPUT" | grep -q "otel-collector-config" && \
   echo "$TEMPLATE_OUTPUT" | grep -q "prometheus:" && \
   echo "$TEMPLATE_OUTPUT" | grep -q "otlp/tempo:" && \
   echo "$TEMPLATE_OUTPUT" | grep -q "loki:"; then
    echo "✅ PASSED: OTel Collector ConfigMap with Prometheus, Tempo, and Loki exporters found"
else
    echo "❌ FAILED: OTel Collector ConfigMap missing required exporters"
    exit 1
fi
echo ""

# Test 5: Verify Prometheus StatefulSet exists
echo "Test 5: Verifying Prometheus StatefulSet..."
if echo "$TEMPLATE_OUTPUT" | grep -q "kind: StatefulSet" && echo "$TEMPLATE_OUTPUT" | grep -q "name: test-release-business-process-agents-prometheus"; then
    echo "✅ PASSED: Prometheus StatefulSet found"
else
    echo "❌ FAILED: Prometheus StatefulSet not found"
    exit 1
fi
echo ""

# Test 6: Verify Tempo StatefulSet exists
echo "Test 6: Verifying Tempo StatefulSet..."
if echo "$TEMPLATE_OUTPUT" | grep -q "kind: StatefulSet" && echo "$TEMPLATE_OUTPUT" | grep -q "name: test-release-business-process-agents-tempo"; then
    echo "✅ PASSED: Tempo StatefulSet found"
else
    echo "❌ FAILED: Tempo StatefulSet not found"
    exit 1
fi
echo ""

# Test 7: Verify Loki StatefulSet exists
echo "Test 7: Verifying Loki StatefulSet..."
if echo "$TEMPLATE_OUTPUT" | grep -q "kind: StatefulSet" && echo "$TEMPLATE_OUTPUT" | grep -q "name: test-release-business-process-agents-loki"; then
    echo "✅ PASSED: Loki StatefulSet found"
else
    echo "❌ FAILED: Loki StatefulSet not found"
    exit 1
fi
echo ""

# Test 8: Verify Grafana Deployment exists
echo "Test 8: Verifying Grafana Deployment..."
if echo "$TEMPLATE_OUTPUT" | grep -q "kind: Deployment" && echo "$TEMPLATE_OUTPUT" | grep -q "name: test-release-business-process-agents-grafana"; then
    echo "✅ PASSED: Grafana Deployment found"
else
    echo "❌ FAILED: Grafana Deployment not found"
    exit 1
fi
echo ""

# Test 9: Verify OTel Collector ports configuration
echo "Test 9: Verifying OTel Collector port configuration..."
if echo "$TEMPLATE_OUTPUT" | grep -q "otlp-grpc" && \
   echo "$TEMPLATE_OUTPUT" | grep -q "otlp-http" && \
   echo "$TEMPLATE_OUTPUT" | grep -q "containerPort: 4317" && \
   echo "$TEMPLATE_OUTPUT" | grep -q "containerPort: 4318"; then
    echo "✅ PASSED: OTel Collector ports (4317, 4318) configured correctly"
else
    echo "❌ FAILED: OTel Collector ports not configured correctly"
    exit 1
fi
echo ""

# Test 10: Verify exporters in OTel pipeline configuration
echo "Test 10: Verifying OTel pipeline exporters configuration..."
if echo "$TEMPLATE_OUTPUT" | grep -q "exporters:" && \
   echo "$TEMPLATE_OUTPUT" | grep -q "prometheus" && \
   echo "$TEMPLATE_OUTPUT" | grep -q "otlp/tempo" && \
   echo "$TEMPLATE_OUTPUT" | grep -q "loki"; then
    echo "✅ PASSED: OTel pipelines configured with all exporters"
else
    echo "❌ FAILED: OTel pipelines missing required exporters"
    exit 1
fi
echo ""

# Test 11: Verify Grafana datasources configuration
echo "Test 11: Verifying Grafana datasources configuration..."
if echo "$TEMPLATE_OUTPUT" | grep -q "grafana-datasources" && \
   echo "$TEMPLATE_OUTPUT" | grep -q "type: prometheus" && \
   echo "$TEMPLATE_OUTPUT" | grep -q "type: tempo" && \
   echo "$TEMPLATE_OUTPUT" | grep -q "type: loki"; then
    echo "✅ PASSED: Grafana datasources configured for Prometheus, Tempo, and Loki"
else
    echo "❌ FAILED: Grafana datasources not configured correctly"
    exit 1
fi
echo ""

# Test 12: Verify Control Plane OpenTelemetry endpoint configuration
echo "Test 12: Verifying Control Plane OTel endpoint configuration..."
if echo "$TEMPLATE_OUTPUT" | grep -q "test-release-business-process-agents-otel-collector:4317"; then
    echo "✅ PASSED: Control Plane configured to send telemetry to OTel Collector"
else
    echo "❌ FAILED: Control Plane OTel endpoint not configured correctly"
    exit 1
fi
echo ""

echo "======================================"
echo "All tests passed! ✅"
echo "======================================"
echo ""
echo "Summary:"
echo "- OTel Collector deployment is properly configured"
echo "- Prometheus exporter is enabled"
echo "- Tempo (traces) exporter is enabled"
echo "- Loki (logs) exporter is enabled"
echo "- Grafana is configured with all datasources"
echo "- Control Plane is configured to send telemetry"
echo ""

exit 0
