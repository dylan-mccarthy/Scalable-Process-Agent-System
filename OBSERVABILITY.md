# OpenTelemetry Observability Quick Start Guide

This guide helps you get started with the OpenTelemetry observability stack for the Business Process Agents Control Plane API.

## Prerequisites

- Docker and Docker Compose installed
- .NET 9.0 SDK
- Control Plane API running

## Starting the Observability Stack

1. **Start the observability services:**

```bash
docker-compose -f docker-compose.observability.yml up -d
```

This starts:
- OpenTelemetry Collector (ports 4317, 4318, 8889)
- Prometheus (port 9090)
- Tempo (port 3200)
- Loki (port 3100)
- Grafana (port 3000)

2. **Verify all services are running:**

```bash
docker-compose -f docker-compose.observability.yml ps
```

## Running the Control Plane API

1. **Configure the API to send telemetry:**

The API is already configured to send telemetry to `http://localhost:4317` (OTLP gRPC endpoint).

2. **Start the API:**

```bash
cd src/ControlPlane.Api
dotnet run
```

## Accessing the Observability Tools

### Grafana (Visualization)

URL: http://localhost:3000

- Pre-configured with Prometheus, Tempo, and Loki datasources
- Anonymous access enabled (no login required for local dev)

**Creating a Dashboard:**

1. Navigate to Dashboards → New Dashboard
2. Add panels for:
   - **Run Metrics**: Query `runs_started_total`, `runs_completed_total`, `runs_failed_total`
   - **Node Metrics**: Query `nodes_registered_total`, `nodes_disconnected_total`
   - **Performance**: Query `run_duration_ms`, `scheduling_duration_ms`
   - **Costs**: Query `run_tokens`, `run_cost_usd`

### Prometheus (Metrics)

URL: http://localhost:9090

**Sample Queries:**

```promql
# Total runs started
rate(runs_started_total[5m])

# Failed run rate
rate(runs_failed_total[5m])

# p95 run duration
histogram_quantile(0.95, rate(run_duration_ms_bucket[5m]))

# Node utilization
nodes_registered_total - nodes_disconnected_total

# Average scheduling time
avg(rate(scheduling_duration_ms_sum[5m]) / rate(scheduling_duration_ms_count[5m]))
```

### Tempo (Distributed Tracing)

URL: http://localhost:3200

Access via Grafana → Explore → Select "Tempo" datasource

**Trace Search:**
- Search by service name: `ControlPlane.Api`
- Search by operation: `RunStore.CompleteRun`, `Scheduler.ScheduleRun`, etc.
- Filter by duration or status

**Trace Flow:**
1. HTTP Request → API Endpoint
2. RunStore.CreateRun → Database Insert
3. Scheduler.ScheduleRun → Node Selection
4. LeaseService.Pull → gRPC Stream

### Loki (Logs)

URL: http://localhost:3100

Access via Grafana → Explore → Select "Loki" datasource

**Sample LogQL Queries:**

```logql
# All logs from Control Plane
{service_name="ControlPlane.Api"}

# Error logs only
{service_name="ControlPlane.Api"} |= "error"

# Logs for a specific run
{service_name="ControlPlane.Api"} | json | run_id="your-run-id"

# Logs correlated with a trace
{service_name="ControlPlane.Api"} | json | trace_id="your-trace-id"
```

## Verifying Telemetry

### 1. Test Metrics Collection

```bash
# Send a request to create a run (via your API tests)
# Then query Prometheus

curl 'http://localhost:9090/api/v1/query?query=runs_started_total'
```

### 2. Test Trace Collection

1. Make API requests to create/complete runs
2. Go to Grafana → Explore → Tempo
3. Search for recent traces
4. Click on a trace to see the full span timeline

### 3. Test Log Collection

1. Make API requests
2. Go to Grafana → Explore → Loki
3. Query: `{service_name="ControlPlane.Api"} | json`
4. See structured logs with trace correlation

## Trace Correlation Example

When you complete a run:

1. **Metrics** show: `runs_completed_total` incremented
2. **Trace** shows: Full span from HTTP request → RunStore.CompleteRun → Database update
3. **Logs** show: All log entries with the same `trace_id`

Navigate between them in Grafana:
- From a trace → Click "Logs for this span" → See correlated logs
- From logs → Click trace_id → See full distributed trace

## Stopping the Stack

```bash
docker-compose -f docker-compose.observability.yml down
```

To also remove volumes (data will be lost):

```bash
docker-compose -f docker-compose.observability.yml down -v
```

## Customization

### Enable Console Exporter (Development)

In `appsettings.Development.json`:

```json
{
  "OpenTelemetry": {
    "ConsoleExporter": {
      "Enabled": true
    }
  }
}
```

This prints telemetry directly to the console for debugging.

### Change OTLP Endpoint

In `appsettings.json`:

```json
{
  "OpenTelemetry": {
    "OtlpExporter": {
      "Endpoint": "http://your-collector:4317"
    }
  }
}
```

### Adjust Sampling Ratio

To reduce trace volume in production:

```json
{
  "OpenTelemetry": {
    "Traces": {
      "SamplingRatio": 0.1
    }
  }
}
```

This samples 10% of traces.

## Troubleshooting

### No metrics appearing

1. Check OTel Collector logs: `docker logs otel-collector`
2. Verify API is sending to correct endpoint: `http://localhost:4317`
3. Check Prometheus targets: http://localhost:9090/targets

### No traces appearing

1. Check Tempo is receiving: `docker logs tempo`
2. Verify traces are enabled in appsettings.json
3. Check activity listener is registered (automatic with OpenTelemetry)

### No logs appearing

1. Check Loki is receiving: `docker logs loki`
2. Verify logs are enabled in appsettings.json
3. Check OTel Collector logs pipeline configuration

## Production Considerations

1. **Use managed services**: Azure Monitor, Datadog, New Relic, etc.
2. **Adjust retention**: Configure appropriate data retention policies
3. **Tune sampling**: Use adaptive sampling based on traffic patterns
4. **Secure endpoints**: Use TLS and authentication for OTLP endpoints
5. **Resource limits**: Set memory and CPU limits for collectors
6. **High availability**: Deploy multiple collector instances with load balancing

## Testing the Observability Stack

### Integration Tests

The project includes automated tests to validate the observability stack configuration:

#### Helm Chart Test

Validates the Kubernetes/Helm deployment configuration:

```bash
./tests/helm-observability-test.sh
```

This test verifies:
- Helm chart templates render correctly
- OTel Collector deployment and service are configured
- Prometheus, Tempo, and Loki exporters are enabled
- All backend services (Prometheus, Tempo, Loki, Grafana) are deployed
- Control Plane is configured to send telemetry to OTel Collector

#### Docker Compose Test

Validates the Docker Compose observability stack:

```bash
./tests/docker-compose-observability-test.sh
```

This test verifies:
- Docker Compose configuration is syntactically valid
- All required services are defined
- OTel Collector configuration includes all exporters
- Service ports are properly exposed
- Persistent volumes are configured

### Running Tests in CI/CD

Add these tests to your CI/CD pipeline:

```yaml
# Example GitHub Actions step
- name: Test Observability Stack
  run: |
    ./tests/helm-observability-test.sh
    ./tests/docker-compose-observability-test.sh
```

## Additional Resources

- [OpenTelemetry Documentation](https://opentelemetry.io/docs/)
- [Grafana Documentation](https://grafana.com/docs/)
- [Prometheus Documentation](https://prometheus.io/docs/)
- [System Architecture Document](./sad.md) - See section 4.5 for observability requirements
