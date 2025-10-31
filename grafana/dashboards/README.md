# Grafana Dashboards

This directory contains pre-configured Grafana dashboards for the Business Process Agents observability stack.

## Available Dashboards

### 1. Control Plane - Runs & Scheduling (`control-plane.json`)

**UID:** `control-plane`  
**Refresh:** 5 seconds  
**Time Range:** Last 15 minutes

Provides comprehensive visibility into the control plane operations, including:

- **Run Metrics:**
  - Active runs (real-time gauge)
  - Runs started, completed, failed, and cancelled (5-minute rates)
  - Run rate over time (per second)
  - Run success rate percentage

- **Performance:**
  - Run duration percentiles (p50, p95, p99)
  - Scheduling duration percentiles
  - Active runs over time

- **Resource Usage:**
  - Token usage distribution per run
  - Cost per run in USD

- **Scheduling:**
  - Scheduling attempts vs failures
  - Scheduling duration trends

### 2. Node Fleet - Health & Capacity (`node-fleet.json`)

**UID:** `node-fleet`  
**Refresh:** 5 seconds  
**Time Range:** Last 15 minutes

Provides real-time visibility into the node fleet health and capacity:

- **Fleet Overview:**
  - Active nodes count
  - Total, used, and available slots
  - Cluster utilization percentage gauge

- **Capacity Tracking:**
  - Active nodes over time
  - Slot utilization over time (stacked area)
  - Utilization percentage trends
  - Cluster capacity breakdown

- **Node Lifecycle:**
  - Node registration/disconnection rates
  - Lease grant/release metrics

## Usage

### Docker Compose

Dashboards are automatically provisioned when you start the observability stack:

```bash
docker compose -f docker-compose.observability.yml up -d
```

Access at:
- Control Plane: http://localhost:3000/d/control-plane
- Node Fleet: http://localhost:3000/d/node-fleet

### Helm/Kubernetes

Dashboards are automatically provisioned when Grafana is enabled in the Helm chart:

```bash
helm install bpa ./helm/business-process-agents \
  --set observability.grafana.enabled=true
```

Port-forward to access:
```bash
kubectl port-forward svc/bpa-grafana 3000:3000
```

Then access at:
- Control Plane: http://localhost:3000/d/control-plane
- Node Fleet: http://localhost:3000/d/node-fleet

## Customization

### Editing Dashboards

You can customize dashboards in two ways:

1. **In Grafana UI:**
   - Open the dashboard
   - Click the gear icon (⚙️) → Settings
   - Click "Make editable" to create a copy
   - Make your changes and save

2. **Edit JSON Files:**
   - Edit the `.json` files in this directory
   - Restart Grafana to load changes
   - For Kubernetes, update the ConfigMap and restart the pod

### Adding New Dashboards

1. Create or export a dashboard as JSON
2. Save to `grafana/dashboards/your-dashboard.json`
3. For Docker Compose: Dashboards are auto-loaded from the mounted directory
4. For Helm:
   - Copy the dashboard to `helm/business-process-agents/dashboards/`
   - Update `templates/grafana-dashboards-configmap.yaml` to include the new file

## Dashboard Provisioning

Dashboards are provisioned using Grafana's provisioning system:

- **Provisioning Config:** `grafana/provisioning/dashboards/dashboards.yaml`
- **Dashboard Files:** This directory
- **Update Interval:** 10 seconds
- **Allow UI Updates:** Yes (changes can be made in the UI)

## Metrics Reference

All metrics are collected via OpenTelemetry and exported to Prometheus.

### Counters
- `runs_started_total`, `runs_completed_total`, `runs_failed_total`, `runs_cancelled_total`
- `nodes_registered_total`, `nodes_disconnected_total`
- `leases_granted_total`, `leases_released_total`
- `scheduling_attempts_total`, `scheduling_failures_total`

### Histograms
- `run_duration_ms` - Run execution latency
- `scheduling_duration_ms` - Scheduling operation latency
- `run_tokens` - Token usage per run
- `run_cost_usd` - Cost per run in USD

### Observable Gauges
- `active_runs` - Current number of active runs
- `active_nodes` - Current number of active nodes
- `total_slots` - Total execution slots across all active nodes
- `used_slots` - Number of slots currently executing runs
- `available_slots` - Number of slots available for new runs

## Troubleshooting

### Dashboards Not Appearing

1. Check Grafana logs:
   ```bash
   docker logs grafana
   # or
   kubectl logs -l app.kubernetes.io/component=grafana
   ```

2. Verify provisioning configuration is mounted:
   ```bash
   docker exec grafana ls -la /etc/grafana/provisioning/dashboards/
   # or
   kubectl exec -it <grafana-pod> -- ls -la /etc/grafana/provisioning/dashboards/
   ```

3. Verify dashboard files are mounted:
   ```bash
   docker exec grafana ls -la /var/lib/grafana/dashboards/
   # or
   kubectl exec -it <grafana-pod> -- ls -la /var/lib/grafana/dashboards/
   ```

### Metrics Not Showing

1. Verify Prometheus is scraping metrics:
   - Go to http://localhost:9090/targets
   - Check that the Control Plane API target is UP

2. Query metrics directly in Prometheus:
   ```
   http://localhost:9090/graph?g0.expr=active_runs
   ```

3. Check OpenTelemetry Collector logs:
   ```bash
   docker logs otel-collector
   ```

## Related Documentation

- [OBSERVABILITY.md](../../OBSERVABILITY.md) - Full observability stack guide
- [System Architecture Document](../../sad.md) - Section 4.5 for observability requirements
