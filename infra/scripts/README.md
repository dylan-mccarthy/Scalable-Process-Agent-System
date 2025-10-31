# k3d Local Development Environment

This directory contains scripts and configuration for setting up a local k3d Kubernetes cluster for the Business Process Agents platform.

**Task:** E6-T1 - Local environment

## Overview

The k3d setup provides a fully-functional local Kubernetes environment that closely mirrors production deployments. It's ideal for:

- Local development and testing
- Integration testing
- Feature validation
- Learning the platform architecture
- CI/CD pipeline testing

## Quick Start

### Prerequisites

Install the required tools:

1. **Docker Desktop** (or Docker Engine)
   - Mac/Windows: https://docs.docker.com/get-docker/
   - Linux: https://docs.docker.com/engine/install/

2. **k3d** (Lightweight Kubernetes in Docker)
   ```bash
   curl -s https://raw.githubusercontent.com/k3d-io/k3d/main/install.sh | bash
   ```

3. **kubectl** (Kubernetes CLI)
   ```bash
   # Mac
   brew install kubectl
   
   # Linux
   curl -LO "https://dl.k8s.io/release/$(curl -L -s https://dl.k8s.io/release/stable.txt)/bin/linux/amd64/kubectl"
   sudo install -o root -g root -m 0755 kubectl /usr/local/bin/kubectl
   ```

4. **Helm** (Kubernetes package manager)
   ```bash
   curl https://raw.githubusercontent.com/helm/helm/main/scripts/get-helm-3 | bash
   ```

### Basic Setup

Run the setup script from the project root:

```bash
./infra/scripts/setup-k3d.sh
```

This will:
1. ✓ Check prerequisites
2. ✓ Create a k3d cluster named `bpa-dev`
3. ✓ Deploy all core services via Helm
4. ✓ Wait for pods to be ready
5. ✓ Perform health checks
6. ✓ Display access information

### With Observability Stack

To include Prometheus, Grafana, and the full observability stack:

```bash
./infra/scripts/setup-k3d.sh --observability
```

### Custom Configuration

```bash
# Custom cluster name
./infra/scripts/setup-k3d.sh --cluster-name my-cluster

# Custom namespace
./infra/scripts/setup-k3d.sh --namespace bpa-dev

# All together
./infra/scripts/setup-k3d.sh --cluster-name my-cluster --namespace bpa-dev --observability
```

## Core Services Deployed

The k3d environment deploys the following services:

### Required Services (Always Deployed)

1. **PostgreSQL** - Primary database
   - Port: 5432 (internal)
   - Database: `bpa`
   - Credentials: `postgres` / `postgres`

2. **Redis** - Lease and lock management
   - Port: 6379 (internal)
   - No authentication (local dev only)

3. **NATS** - Event streaming with JetStream
   - Port: 4222 (internal)
   - Management: 8222 (internal)

4. **Control Plane API** - Core orchestration service
   - HTTP: http://localhost:8080
   - gRPC: http://localhost:8081
   - Endpoints: `/v1/agents`, `/v1/nodes`, `/v1/runs`, etc.

5. **Node Runtime** - Worker nodes (2 replicas)
   - Connects to Control Plane via gRPC
   - Executes agent runs

6. **Admin UI** - Management interface
   - URL: http://localhost:3000
   - Built with Next.js

### Optional Services (--observability flag)

7. **OpenTelemetry Collector** - Telemetry collection
   - OTLP gRPC: 4317
   - OTLP HTTP: 4318

8. **Prometheus** - Metrics storage and querying
   - URL: http://localhost:9090

9. **Tempo** - Distributed tracing backend
   - Internal only

10. **Loki** - Log aggregation
    - Internal only

11. **Grafana** - Observability dashboards
    - URL: http://localhost:3000 (port-forward required)
    - Credentials: `admin` / `admin`

## Accessing Services

### Direct Access (Port Mapping)

The following services are accessible directly via localhost:

- **Control Plane API**: http://localhost:8080
- **Admin UI**: http://localhost:3000
- **Prometheus** (if enabled): http://localhost:9090

### Port Forwarding

For other services, use kubectl port-forward:

```bash
# Grafana
kubectl port-forward -n default svc/bpa-business-process-agents-grafana 3001:3000

# PostgreSQL
kubectl port-forward -n default svc/bpa-business-process-agents-postgresql 5432:5432

# Redis
kubectl port-forward -n default svc/bpa-business-process-agents-redis 6379:6379

# NATS
kubectl port-forward -n default svc/bpa-business-process-agents-nats 4222:4222
```

## Common Tasks

### View Logs

```bash
# Control Plane logs
kubectl logs -l app.kubernetes.io/component=control-plane -f

# Node Runtime logs
kubectl logs -l app.kubernetes.io/component=node-runtime -f

# All application logs
kubectl logs -l app.kubernetes.io/instance=bpa -f --all-containers
```

### Check Pod Status

```bash
# List all pods
kubectl get pods

# Detailed pod information
kubectl describe pod <pod-name>

# Watch pods in real-time
kubectl get pods -w
```

### Execute Commands in Pods

```bash
# Access PostgreSQL
kubectl exec -it <postgres-pod-name> -- psql -U postgres -d bpa

# Access Redis CLI
kubectl exec -it <redis-pod-name> -- redis-cli

# Shell into Control Plane
kubectl exec -it <control-plane-pod-name> -- /bin/bash
```

### Restart Services

```bash
# Restart Control Plane
kubectl rollout restart deployment bpa-business-process-agents-control-plane

# Restart Node Runtime
kubectl rollout restart deployment bpa-business-process-agents-node-runtime

# Restart Admin UI
kubectl rollout restart deployment bpa-business-process-agents-admin-ui
```

### Scale Services

```bash
# Scale Node Runtime to 5 replicas
kubectl scale deployment bpa-business-process-agents-node-runtime --replicas=5

# Verify scaling
kubectl get deployment bpa-business-process-agents-node-runtime
```

## Testing the Setup

### API Health Check

```bash
# Check Control Plane health
curl http://localhost:8080/health

# List agents
curl http://localhost:8080/v1/agents

# List nodes
curl http://localhost:8080/v1/nodes
```

### Create a Test Agent

```bash
# Create an agent
curl -X POST http://localhost:8080/v1/agents \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Test Agent",
    "instructions": "A simple test agent",
    "modelProfile": {
      "model": "gpt-4",
      "temperature": 0.7
    }
  }'
```

### Access Admin UI

Open http://localhost:3000 in your browser to access the web interface.

## Troubleshooting

### Cluster Creation Issues

**Problem**: k3d cluster creation fails

```bash
# Check Docker is running
docker ps

# Check existing clusters
k3d cluster list

# Delete conflicting cluster
k3d cluster delete bpa-dev

# Try again
./infra/scripts/setup-k3d.sh
```

### Pods Not Starting

**Problem**: Pods stuck in `Pending` or `CrashLoopBackOff`

```bash
# Check pod events
kubectl describe pod <pod-name>

# Check logs
kubectl logs <pod-name>

# Check resource availability
kubectl top nodes
kubectl top pods
```

### Image Pull Errors

**Problem**: `ImagePullBackOff` or `ErrImagePull`

The k3d setup uses local images with `pullPolicy: IfNotPresent`. If images don't exist:

```bash
# Build images from project root
cd ../..

# Build Control Plane
docker build -t business-process-agents/control-plane:latest \
  -f src/ControlPlane.Api/Dockerfile .

# Build Node Runtime
docker build -t business-process-agents/node-runtime:latest \
  -f src/Node.Runtime/Dockerfile .

# Build Admin UI
docker build -t business-process-agents/admin-ui:latest \
  -f src/admin-ui/Dockerfile ./src/admin-ui

# Import into k3d
k3d image import business-process-agents/control-plane:latest -c bpa-dev
k3d image import business-process-agents/node-runtime:latest -c bpa-dev
k3d image import business-process-agents/admin-ui:latest -c bpa-dev
```

### Database Connection Issues

**Problem**: Control Plane can't connect to PostgreSQL

```bash
# Check PostgreSQL pod
kubectl get pod -l app.kubernetes.io/name=postgresql

# Test connection
kubectl exec -it <postgres-pod> -- psql -U postgres -d bpa -c "SELECT 1"

# Check connection string in Control Plane
kubectl describe pod <control-plane-pod> | grep ConnectionStrings
```

### Port Already in Use

**Problem**: Port 8080, 3000, or 9090 already in use

```bash
# Find process using port
lsof -i :8080
lsof -i :3000

# Kill process or use different ports
k3d cluster delete bpa-dev
k3d cluster create bpa-dev --port "8081:80@loadbalancer"
```

### Reset Everything

If you need to start completely fresh:

```bash
# Delete cluster
./infra/scripts/cleanup-k3d.sh --force

# Clean Docker volumes
docker volume prune -f

# Recreate cluster
./infra/scripts/setup-k3d.sh
```

## Cleanup

### Remove Cluster

```bash
# Interactive cleanup (with confirmation)
./infra/scripts/cleanup-k3d.sh

# Force cleanup (no confirmation)
./infra/scripts/cleanup-k3d.sh --force

# Cleanup custom cluster
./infra/scripts/cleanup-k3d.sh --cluster-name my-cluster
```

### Manual Cleanup

```bash
# Delete cluster manually
k3d cluster delete bpa-dev

# Remove leftover volumes
docker volume ls | grep k3d-bpa-dev | awk '{print $2}' | xargs docker volume rm
```

## Configuration Files

- **setup-k3d.sh** - Main setup script
- **cleanup-k3d.sh** - Cleanup script
- **values-k3d.yaml** - Helm values optimized for k3d
- **../../helm/business-process-agents/** - Helm chart

## Customizing the Setup

### Modify Helm Values

Create your own values file:

```yaml
# my-values.yaml
controlPlane:
  replicaCount: 2
  
nodeRuntime:
  replicaCount: 5
  capacity:
    slots: 16
```

Deploy with custom values:

```bash
helm upgrade --install bpa ../../helm/business-process-agents \
  -f infra/helm/values-k3d.yaml \
  -f my-values.yaml
```

### Change Resource Limits

Edit `values-k3d.yaml` and update the `resources` sections:

```yaml
controlPlane:
  resources:
    requests:
      cpu: 200m
      memory: 512Mi
    limits:
      cpu: 1000m
      memory: 1Gi
```

Then upgrade the deployment:

```bash
helm upgrade bpa ../../helm/business-process-agents \
  -f infra/helm/values-k3d.yaml
```

## CI/CD Integration

The k3d setup can be used in CI/CD pipelines:

```yaml
# .github/workflows/integration-test.yml
jobs:
  test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      
      - name: Setup k3d cluster
        run: ./infra/scripts/setup-k3d.sh
      
      - name: Run integration tests
        run: npm test
      
      - name: Cleanup
        if: always()
        run: ./infra/scripts/cleanup-k3d.sh --force
```

## Performance Tuning

For better performance on resource-constrained systems:

1. **Reduce replicas**:
   ```bash
   helm upgrade bpa ../../helm/business-process-agents \
     -f infra/helm/values-k3d.yaml \
     --set nodeRuntime.replicaCount=1
   ```

2. **Disable observability**:
   ```bash
   # Don't use --observability flag
   ./infra/scripts/setup-k3d.sh
   ```

3. **Reduce resource limits** in `values-k3d.yaml`

4. **Use fewer k3d agents**:
   ```bash
   k3d cluster create bpa-dev --agents 1
   ```

## Differences from Production

The k3d environment differs from production in several ways:

| Aspect | k3d (Local) | Production (AKS) |
|--------|-------------|------------------|
| Database | PostgreSQL in cluster | Azure Database for PostgreSQL |
| Redis | Redis in cluster | Azure Cache for Redis |
| Secrets | ConfigMaps/Secrets | Azure Key Vault |
| Storage | local-path | Azure Disks |
| Networking | ClusterIP + port-forward | Ingress + Load Balancer |
| TLS | Disabled | Enabled with cert-manager |
| Scaling | Manual | Auto-scaling (HPA/CA) |
| Monitoring | Optional | Always enabled |
| High Availability | Single replica | Multiple replicas + zones |

## Next Steps

- **Development**: Start coding against the local API at http://localhost:8080
- **Testing**: Use the k3d environment for integration tests
- **Observability**: Enable with `--observability` to explore metrics and traces
- **Cloud Deployment**: See `infra/README.md` for deploying to Azure AKS

## Support

For issues or questions:
- GitHub Issues: https://github.com/dylan-mccarthy/Scalable-Process-Agent-System/issues
- Documentation: See project README.md
- Epic: E6 - Infrastructure & CI/CD
- Task: E6-T1 - Local environment
