# Business Process Agents Helm Chart

This Helm chart deploys the Business Process Agents MVP platform on Kubernetes.

## Prerequisites

- Kubernetes 1.24+
- Helm 3.8+
- PV provisioner support in the underlying infrastructure (for persistence)

## Components

This chart deploys the following components:

- **Control Plane API**: Central orchestration and management service
- **Node Runtime**: Worker nodes for agent execution
- **Admin UI**: Web-based administration interface
- **PostgreSQL**: Database for persistent storage
- **Redis**: Distributed leases and locks
- **NATS**: Event streaming with JetStream
- **OpenTelemetry Collector** (optional): Telemetry collection and export

## Installing the Chart

To install the chart with the release name `bpa`:

```bash
helm install bpa ./helm/business-process-agents
```

To install with custom values:

```bash
helm install bpa ./helm/business-process-agents -f my-values.yaml
```

## Uninstalling the Chart

To uninstall/delete the `bpa` deployment:

```bash
helm uninstall bpa
```

## Configuration

The following table lists the configurable parameters and their default values.

### Global Configuration

| Parameter | Description | Default |
|-----------|-------------|---------|
| `global.imagePullSecrets` | Global Docker registry secret names | `[]` |
| `global.storageClass` | Global storage class for PVCs | `""` |

### Control Plane Configuration

| Parameter | Description | Default |
|-----------|-------------|---------|
| `controlPlane.enabled` | Enable Control Plane deployment | `true` |
| `controlPlane.replicaCount` | Number of Control Plane replicas | `2` |
| `controlPlane.image.repository` | Control Plane image repository | `business-process-agents/control-plane` |
| `controlPlane.image.tag` | Control Plane image tag | `latest` |
| `controlPlane.image.pullPolicy` | Image pull policy | `IfNotPresent` |
| `controlPlane.service.type` | Service type | `ClusterIP` |
| `controlPlane.service.port` | HTTP port | `8080` |
| `controlPlane.service.grpcPort` | gRPC port | `8081` |
| `controlPlane.ingress.enabled` | Enable ingress | `false` |
| `controlPlane.autoscaling.enabled` | Enable HPA | `false` |
| `controlPlane.migration.enabled` | Run database migrations | `true` |

### Node Runtime Configuration

| Parameter | Description | Default |
|-----------|-------------|---------|
| `nodeRuntime.enabled` | Enable Node Runtime deployment | `true` |
| `nodeRuntime.replicaCount` | Number of Node Runtime replicas | `2` |
| `nodeRuntime.image.repository` | Node Runtime image repository | `business-process-agents/node-runtime` |
| `nodeRuntime.image.tag` | Node Runtime image tag | `latest` |
| `nodeRuntime.autoscaling.enabled` | Enable HPA | `true` |
| `nodeRuntime.autoscaling.minReplicas` | Minimum replicas | `2` |
| `nodeRuntime.autoscaling.maxReplicas` | Maximum replicas | `20` |
| `nodeRuntime.capacity.slots` | Node capacity slots | `8` |
| `nodeRuntime.metadata.region` | Node region | `default` |
| `nodeRuntime.metadata.environment` | Node environment | `production` |

### Admin UI Configuration

| Parameter | Description | Default |
|-----------|-------------|---------|
| `adminUI.enabled` | Enable Admin UI deployment | `true` |
| `adminUI.replicaCount` | Number of Admin UI replicas | `1` |
| `adminUI.image.repository` | Admin UI image repository | `business-process-agents/admin-ui` |
| `adminUI.image.tag` | Admin UI image tag | `latest` |
| `adminUI.service.port` | Service port | `3000` |
| `adminUI.ingress.enabled` | Enable ingress | `false` |

### PostgreSQL Configuration

| Parameter | Description | Default |
|-----------|-------------|---------|
| `postgresql.enabled` | Enable PostgreSQL | `true` |
| `postgresql.image.repository` | PostgreSQL image repository | `postgres` |
| `postgresql.image.tag` | PostgreSQL image tag | `16-alpine` |
| `postgresql.persistence.enabled` | Enable persistence | `true` |
| `postgresql.persistence.size` | PVC size | `10Gi` |
| `postgresql.auth.database` | Database name | `bpa` |
| `postgresql.auth.username` | Database username | `postgres` |
| `postgresql.auth.password` | Database password | `postgres` |

### Redis Configuration

| Parameter | Description | Default |
|-----------|-------------|---------|
| `redis.enabled` | Enable Redis | `true` |
| `redis.image.repository` | Redis image repository | `redis` |
| `redis.image.tag` | Redis image tag | `7-alpine` |
| `redis.persistence.enabled` | Enable persistence | `true` |
| `redis.persistence.size` | PVC size | `5Gi` |

### NATS Configuration

| Parameter | Description | Default |
|-----------|-------------|---------|
| `nats.enabled` | Enable NATS | `true` |
| `nats.image.repository` | NATS image repository | `nats` |
| `nats.image.tag` | NATS image tag | `2.10-alpine` |
| `nats.jetstream.enabled` | Enable JetStream | `true` |
| `nats.persistence.enabled` | Enable persistence | `true` |
| `nats.persistence.size` | PVC size | `5Gi` |

## Examples

### Minimal Installation (In-Memory)

For testing without persistent storage:

```yaml
# values-minimal.yaml
postgresql:
  persistence:
    enabled: false

redis:
  persistence:
    enabled: false

nats:
  persistence:
    enabled: false

controlPlane:
  replicaCount: 1

nodeRuntime:
  replicaCount: 1
  autoscaling:
    enabled: false
```

```bash
helm install bpa ./helm/business-process-agents -f values-minimal.yaml
```

### Production Installation with Ingress

```yaml
# values-production.yaml
controlPlane:
  replicaCount: 3
  autoscaling:
    enabled: true
    minReplicas: 3
    maxReplicas: 10
  ingress:
    enabled: true
    className: nginx
    annotations:
      cert-manager.io/cluster-issuer: letsencrypt-prod
    hosts:
      - host: api.bpa.example.com
        paths:
          - path: /
            pathType: Prefix
    tls:
      - secretName: control-plane-tls
        hosts:
          - api.bpa.example.com

adminUI:
  ingress:
    enabled: true
    className: nginx
    annotations:
      cert-manager.io/cluster-issuer: letsencrypt-prod
    hosts:
      - host: admin.bpa.example.com
        paths:
          - path: /
            pathType: Prefix
    tls:
      - secretName: admin-ui-tls
        hosts:
          - admin.bpa.example.com

postgresql:
  persistence:
    storageClass: fast-ssd
    size: 50Gi
  auth:
    password: <secure-password>

nodeRuntime:
  autoscaling:
    enabled: true
    minReplicas: 5
    maxReplicas: 50

otelCollector:
  enabled: true
```

```bash
helm install bpa ./helm/business-process-agents -f values-production.yaml
```

### Local Development (k3d)

```bash
# Create k3d cluster
k3d cluster create bpa-dev --servers 1 --agents 2

# Install chart
helm install bpa ./helm/business-process-agents

# Port forward to access services
kubectl port-forward svc/bpa-business-process-agents-control-plane 8080:8080
kubectl port-forward svc/bpa-business-process-agents-admin-ui 3000:3000
```

## Upgrading

To upgrade the release:

```bash
helm upgrade bpa ./helm/business-process-agents -f my-values.yaml
```

## Database Migrations

Database migrations are automatically run as an init container in the Control Plane deployment when `controlPlane.migration.enabled` is `true` (default).

To disable automatic migrations:

```yaml
controlPlane:
  migration:
    enabled: false
```

Then run migrations manually:

```bash
kubectl exec -it <control-plane-pod> -- dotnet ef database update
```

## Monitoring and Observability

Enable the full observability stack:

```yaml
otelCollector:
  enabled: true

observability:
  prometheus:
    enabled: true
  grafana:
    enabled: true
  tempo:
    enabled: true
  loki:
    enabled: true
```

Access Grafana:

```bash
kubectl port-forward svc/bpa-business-process-agents-grafana 3000:3000
```

Default credentials: admin/admin

## Security Considerations

### Production Deployments

1. **Use External Secrets**: Never store passwords in values.yaml

   ```yaml
   postgresql:
     auth:
       existingSecret: postgres-credentials
   ```

2. **Enable Network Policies**: Restrict pod-to-pod communication

3. **Use TLS**: Enable TLS for all external endpoints

4. **RBAC**: Configure appropriate service account permissions

5. **Image Security**: Use signed images and vulnerability scanning

### Resource Limits

Always set resource limits in production:

```yaml
controlPlane:
  resources:
    limits:
      cpu: 2000m
      memory: 2Gi
    requests:
      cpu: 1000m
      memory: 1Gi
```

## Troubleshooting

### Check pod status

```bash
kubectl get pods -l app.kubernetes.io/instance=bpa
```

### View logs

```bash
# Control Plane
kubectl logs -l app.kubernetes.io/component=control-plane -f

# Node Runtime
kubectl logs -l app.kubernetes.io/component=node-runtime -f

# Admin UI
kubectl logs -l app.kubernetes.io/component=admin-ui -f
```

### Database connection issues

```bash
# Check PostgreSQL
kubectl exec -it <postgres-pod> -- psql -U postgres -d bpa -c "SELECT 1"

# Check connection from Control Plane
kubectl exec -it <control-plane-pod> -- curl postgres:5432
```

### Redis connection issues

```bash
kubectl exec -it <redis-pod> -- redis-cli ping
```

### NATS connection issues

```bash
kubectl exec -it <nats-pod> -- wget -qO- http://localhost:8222/healthz
```

## Support

For issues and questions, please open an issue on the [GitHub repository](https://github.com/dylan-mccarthy/Scalable-Process-Agent-System).
