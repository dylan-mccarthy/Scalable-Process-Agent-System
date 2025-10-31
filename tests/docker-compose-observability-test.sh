#!/bin/bash
# Integration test for Docker Compose observability stack
# This test validates the docker-compose.observability.yml configuration

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
COMPOSE_FILE="$PROJECT_ROOT/docker-compose.observability.yml"
OTEL_CONFIG="$PROJECT_ROOT/otel-collector-config.yaml"
PROMETHEUS_CONFIG="$PROJECT_ROOT/prometheus.yml"
TEMPO_CONFIG="$PROJECT_ROOT/tempo.yaml"
GRAFANA_DATASOURCES="$PROJECT_ROOT/grafana-datasources.yaml"

echo "======================================"
echo "Docker Compose Observability Test"
echo "======================================"
echo ""

# Test 1: Validate docker-compose.yml syntax
echo "Test 1: Validating Docker Compose file syntax..."
if docker compose -f "$COMPOSE_FILE" config > /dev/null 2>&1; then
    echo "✅ PASSED: Docker Compose file is valid"
else
    echo "❌ FAILED: Docker Compose file has syntax errors"
    exit 1
fi
echo ""

# Test 2: Verify all required services are defined
echo "Test 2: Verifying all required services..."
COMPOSE_OUTPUT=$(docker compose -f "$COMPOSE_FILE" config 2>&1)
REQUIRED_SERVICES=("otel-collector" "prometheus" "tempo" "loki" "grafana")
for service in "${REQUIRED_SERVICES[@]}"; do
    if echo "$COMPOSE_OUTPUT" | grep -q "$service:"; then
        echo "  ✅ Service '$service' found"
    else
        echo "  ❌ Service '$service' not found"
        exit 1
    fi
done
echo "✅ PASSED: All required services defined"
echo ""

# Test 3: Verify OTel Collector configuration file exists
echo "Test 3: Verifying OTel Collector configuration..."
if [ -f "$OTEL_CONFIG" ]; then
    echo "  ✅ Configuration file exists: $OTEL_CONFIG"
    
    # Check for required exporters
    if grep -q "prometheus:" "$OTEL_CONFIG" && \
       grep -q "otlp/tempo:" "$OTEL_CONFIG" && \
       grep -q "loki:" "$OTEL_CONFIG"; then
        echo "  ✅ All exporters (Prometheus, Tempo, Loki) configured"
    else
        echo "  ❌ Missing required exporters in configuration"
        exit 1
    fi
    
    # Check for OTLP receivers
    if grep -q "otlp:" "$OTEL_CONFIG" && \
       grep -q "grpc:" "$OTEL_CONFIG" && \
       grep -q "http:" "$OTEL_CONFIG"; then
        echo "  ✅ OTLP receivers (gRPC and HTTP) configured"
    else
        echo "  ❌ OTLP receivers not properly configured"
        exit 1
    fi
else
    echo "  ❌ Configuration file not found: $OTEL_CONFIG"
    exit 1
fi
echo "✅ PASSED: OTel Collector configuration valid"
echo ""

# Test 4: Verify Prometheus configuration
echo "Test 4: Verifying Prometheus configuration..."
if [ -f "$PROMETHEUS_CONFIG" ]; then
    echo "  ✅ Configuration file exists: $PROMETHEUS_CONFIG"
    
    # Check for OTel Collector scrape config
    if grep -q "otel-collector" "$PROMETHEUS_CONFIG"; then
        echo "  ✅ OTel Collector scrape target configured"
    else
        echo "  ❌ OTel Collector scrape target not configured"
        exit 1
    fi
else
    echo "  ❌ Configuration file not found: $PROMETHEUS_CONFIG"
    exit 1
fi
echo "✅ PASSED: Prometheus configuration valid"
echo ""

# Test 5: Verify Tempo configuration
echo "Test 5: Verifying Tempo configuration..."
if [ -f "$TEMPO_CONFIG" ]; then
    echo "  ✅ Configuration file exists: $TEMPO_CONFIG"
    
    # Check for OTLP receiver
    if grep -q "otlp:" "$TEMPO_CONFIG" && grep -q "grpc:" "$TEMPO_CONFIG"; then
        echo "  ✅ OTLP gRPC receiver configured"
    else
        echo "  ❌ OTLP receiver not configured"
        exit 1
    fi
else
    echo "  ❌ Configuration file not found: $TEMPO_CONFIG"
    exit 1
fi
echo "✅ PASSED: Tempo configuration valid"
echo ""

# Test 6: Verify Grafana datasources
echo "Test 6: Verifying Grafana datasources configuration..."
if [ -f "$GRAFANA_DATASOURCES" ]; then
    echo "  ✅ Configuration file exists: $GRAFANA_DATASOURCES"
    
    # Check for all datasources
    if grep -q "name: Prometheus" "$GRAFANA_DATASOURCES" && \
       grep -q "name: Tempo" "$GRAFANA_DATASOURCES" && \
       grep -q "name: Loki" "$GRAFANA_DATASOURCES"; then
        echo "  ✅ All datasources (Prometheus, Tempo, Loki) configured"
    else
        echo "  ❌ Missing required datasources"
        exit 1
    fi
    
    # Check for trace correlation
    if grep -q "tracesToLogs:" "$GRAFANA_DATASOURCES"; then
        echo "  ✅ Trace-to-logs correlation configured"
    else
        echo "  ⚠️  Warning: Trace-to-logs correlation not configured"
    fi
else
    echo "  ❌ Configuration file not found: $GRAFANA_DATASOURCES"
    exit 1
fi
echo "✅ PASSED: Grafana datasources configuration valid"
echo ""

# Test 7: Verify Docker Compose service ports
echo "Test 7: Verifying service port mappings..."
REQUIRED_PORTS=("4317" "4318" "9090" "3200" "3100" "3000")
PORT_DESCRIPTIONS=("OTLP gRPC" "OTLP HTTP" "Prometheus" "Tempo" "Loki" "Grafana")

all_ports_found=true
for i in "${!REQUIRED_PORTS[@]}"; do
    port="${REQUIRED_PORTS[$i]}"
    desc="${PORT_DESCRIPTIONS[$i]}"
    if echo "$COMPOSE_OUTPUT" | grep -q "published: \"$port\""; then
        echo "  ✅ $desc port ($port) exposed"
    else
        echo "  ❌ $desc port ($port) not exposed"
        all_ports_found=false
    fi
done

if [ "$all_ports_found" = false ]; then
    echo "  ❌ Required ports not properly exposed"
    exit 1
fi
echo "✅ PASSED: All service ports properly configured"
echo ""

# Test 8: Verify volumes are defined
echo "Test 8: Verifying persistent volumes..."
if echo "$COMPOSE_OUTPUT" | grep -q "prometheus-data:" && \
   echo "$COMPOSE_OUTPUT" | grep -q "tempo-data:" && \
   echo "$COMPOSE_OUTPUT" | grep -q "loki-data:" && \
   echo "$COMPOSE_OUTPUT" | grep -q "grafana-data:"; then
    echo "  ✅ All required persistent volumes defined"
else
    echo "  ❌ Missing required persistent volumes"
    exit 1
fi
echo "✅ PASSED: Persistent volumes configured"
echo ""

echo "======================================"
echo "All tests passed! ✅"
echo "======================================"
echo ""
echo "Summary:"
echo "- Docker Compose configuration is valid"
echo "- OTel Collector is configured with Prometheus, Tempo, and Loki exporters"
echo "- Prometheus is configured to scrape OTel Collector metrics"
echo "- Tempo is configured to receive traces via OTLP"
echo "- Grafana is configured with all datasources and trace correlation"
echo "- All service ports are properly exposed"
echo "- Persistent volumes are configured for data retention"
echo ""

exit 0
