# Deployment Guide

This guide covers deploying the Business Process Agents platform using Docker, Docker Compose, and Kubernetes with Helm.

## Table of Contents

- [Docker Deployment](#docker-deployment)
- [Docker Compose Deployment](#docker-compose-deployment)
- [Kubernetes Deployment with Helm](#kubernetes-deployment-with-helm)
- [Local Development with k3d](#local-development-with-k3d)
- [Production Deployment on AKS](#production-deployment-on-aks)
- [Troubleshooting](#troubleshooting)

## Docker Deployment

### Building Images

Build all service images individually:

```bash
# Build Control Plane API
docker build -t business-process-agents/control-plane:latest \
  -f src/ControlPlane.Api/Dockerfile .

# Build Node Runtime
docker build -t business-process-agents/node-runtime:latest \
  -f src/Node.Runtime/Dockerfile .

# Build Admin UI
docker build -t business-process-agents/admin-ui:latest \
  -f src/admin-ui/Dockerfile ./src/admin-ui
```

### Running Individual Containers

```bash
# Start PostgreSQL
docker run -d --name bpa-postgres \
  -e POSTGRES_DB=bpa \
  -e POSTGRES_USER=postgres \
  -e POSTGRES_PASSWORD=<SECURE_PASSWORD> \
  -p 5432:5432 \
  postgres:16-alpine

# Start Redis
docker run -d --name bpa-redis \
  -p 6379:6379 \
  redis:7-alpine

# Start NATS
docker run -d --name bpa-nats \
  -p 4222:4222 -p 8222:8222 \
  nats:2.10-alpine --jetstream

# Start Control Plane
docker run -d --name bpa-control-plane \
  -e ConnectionStrings__DefaultConnection="Host=host.docker.internal:5432;Database=bpa;Username=postgres;Password=<SECURE_PASSWORD>" \
  -e ConnectionStrings__Redis="host.docker.internal:6379" \
  -e ConnectionStrings__Nats="nats://host.docker.internal:4222" \
  -p 8080:8080 -p 8081:8081 \
  business-process-agents/control-plane:latest

# Start Node Runtime
docker run -d --name bpa-node-runtime \
  -e ControlPlane__BaseUrl="http://host.docker.internal:8080" \
  -e ControlPlane__GrpcUrl="http://host.docker.internal:8081" \
  business-process-agents/node-runtime:latest

# Start Admin UI
docker run -d --name bpa-admin-ui \
  -e NEXT_PUBLIC_API_URL="http://localhost:8080" \
  -p 3000:3000 \
  business-process-agents/admin-ui:latest
```

## Docker Compose Deployment

### Full Stack Deployment

Start all services with a single command:

```bash
# Start all services
docker-compose up -d

# View logs
docker-compose logs -f

# Stop all services
docker-compose down

# Clean up including volumes
docker-compose down -v
```

### With Observability Stack

Include Prometheus, Grafana, and OpenTelemetry Collector:

```bash
docker-compose --profile observability up -d
```

Access points:
- **Control Plane API**: http://localhost:8080
- **Admin UI**: http://localhost:3000
- **Grafana**: http://localhost:3001 (with observability profile)
- **Prometheus**: http://localhost:9090 (with observability profile)

### Scaling Node Runtime

```bash
# Scale to 5 node runtime instances
docker-compose up -d --scale node-runtime=5
```

## Kubernetes Deployment with Helm

### Prerequisites

- Kubernetes cluster (v1.24+)
- Helm 3.8+
- kubectl configured

### Quick Start

```bash
# Install the chart
helm install bpa ./helm/business-process-agents

# Check deployment status
kubectl get pods -l app.kubernetes.io/instance=bpa

# Port forward to access services
kubectl port-forward svc/bpa-business-process-agents-control-plane 8080:8080
kubectl port-forward svc/bpa-business-process-agents-admin-ui 3000:3000
```

### Custom Configuration

Create a custom values file:

```yaml
# custom-values.yaml
controlPlane:
  replicaCount: 2
  resources:
    requests:
      cpu: 500m
      memory: 512Mi
    limits:
      cpu: 1000m
      memory: 1Gi

nodeRuntime:
  replicaCount: 3
  autoscaling:
    enabled: true
    minReplicas: 3
    maxReplicas: 20

postgresql:
  persistence:
    size: 20Gi
```

Install with custom values:

```bash
helm install bpa ./helm/business-process-agents -f custom-values.yaml
```

### Enabling Ingress

```yaml
# values-ingress.yaml
controlPlane:
  ingress:
    enabled: true
    className: nginx
    annotations:
      cert-manager.io/cluster-issuer: letsencrypt-prod
    hosts:
      - host: api.example.com
        paths:
          - path: /
            pathType: Prefix
    tls:
      - secretName: control-plane-tls
        hosts:
          - api.example.com

adminUI:
  ingress:
    enabled: true
    className: nginx
    annotations:
      cert-manager.io/cluster-issuer: letsencrypt-prod
    hosts:
      - host: admin.example.com
        paths:
          - path: /
            pathType: Prefix
    tls:
      - secretName: admin-ui-tls
        hosts:
          - admin.example.com
```

```bash
helm install bpa ./helm/business-process-agents -f values-ingress.yaml
```

### Upgrading

```bash
# Upgrade existing release
helm upgrade bpa ./helm/business-process-agents -f custom-values.yaml

# Check upgrade status
helm status bpa

# Rollback if needed
helm rollback bpa
```

### Uninstalling

```bash
# Uninstall the release
helm uninstall bpa

# Delete PVCs (optional - deletes all data)
kubectl delete pvc -l app.kubernetes.io/instance=bpa
```

## Local Development with k3d

### Setup k3d Cluster

```bash
# Install k3d (if not already installed)
curl -s https://raw.githubusercontent.com/k3d-io/k3d/main/install.sh | bash

# Create cluster
k3d cluster create bpa-dev \
  --servers 1 \
  --agents 2 \
  --port "8080:80@loadbalancer" \
  --port "8443:443@loadbalancer"

# Verify cluster
kubectl cluster-info
kubectl get nodes
```

### Deploy to k3d

```bash
# Install Helm chart
helm install bpa ./helm/business-process-agents \
  --set controlPlane.replicaCount=1 \
  --set nodeRuntime.replicaCount=2

# Wait for pods to be ready
kubectl wait --for=condition=ready pod -l app.kubernetes.io/instance=bpa --timeout=300s

# Port forward services
kubectl port-forward svc/bpa-business-process-agents-control-plane 8080:8080 &
kubectl port-forward svc/bpa-business-process-agents-admin-ui 3000:3000 &
```

### Load Local Images into k3d

```bash
# Build images
docker build -t business-process-agents/control-plane:dev -f src/ControlPlane.Api/Dockerfile .
docker build -t business-process-agents/node-runtime:dev -f src/Node.Runtime/Dockerfile .
docker build -t business-process-agents/admin-ui:dev -f src/admin-ui/Dockerfile ./src/admin-ui

# Import into k3d
k3d image import business-process-agents/control-plane:dev -c bpa-dev
k3d image import business-process-agents/node-runtime:dev -c bpa-dev
k3d image import business-process-agents/admin-ui:dev -c bpa-dev

# Deploy with local images
helm install bpa ./helm/business-process-agents \
  --set controlPlane.image.tag=dev \
  --set nodeRuntime.image.tag=dev \
  --set adminUI.image.tag=dev \
  --set controlPlane.image.pullPolicy=Never \
  --set nodeRuntime.image.pullPolicy=Never \
  --set adminUI.image.pullPolicy=Never
```

### Clean Up

```bash
# Delete cluster
k3d cluster delete bpa-dev
```

## Production Deployment on AKS

### Prerequisites

- Azure CLI installed
- Azure subscription
- Helm 3.8+
- kubectl

### Create AKS Cluster

```bash
# Login to Azure
az login

# Create resource group
az group create --name bpa-rg --location eastus

# Create AKS cluster
az aks create \
  --resource-group bpa-rg \
  --name bpa-cluster \
  --node-count 3 \
  --node-vm-size Standard_D4s_v3 \
  --enable-managed-identity \
  --generate-ssh-keys

# Get credentials
az aks get-credentials --resource-group bpa-rg --name bpa-cluster

# Verify connection
kubectl cluster-info
```

### Create Azure Container Registry

```bash
# Create ACR
az acr create \
  --resource-group bpa-rg \
  --name bparegistry \
  --sku Standard

# Attach ACR to AKS
az aks update \
  --resource-group bpa-rg \
  --name bpa-cluster \
  --attach-acr bparegistry

# Login to ACR
az acr login --name bparegistry
```

### Build and Push Images

```bash
# Tag and push images
docker tag business-process-agents/control-plane:latest bparegistry.azurecr.io/control-plane:1.0.0
docker tag business-process-agents/node-runtime:latest bparegistry.azurecr.io/node-runtime:1.0.0
docker tag business-process-agents/admin-ui:latest bparegistry.azurecr.io/admin-ui:1.0.0

docker push bparegistry.azurecr.io/control-plane:1.0.0
docker push bparegistry.azurecr.io/node-runtime:1.0.0
docker push bparegistry.azurecr.io/admin-ui:1.0.0
```

### Install NGINX Ingress Controller

```bash
# Add ingress-nginx repository
helm repo add ingress-nginx https://kubernetes.github.io/ingress-nginx
helm repo update

# Install ingress controller
helm install nginx-ingress ingress-nginx/ingress-nginx \
  --set controller.service.annotations."service\.beta\.kubernetes\.io/azure-load-balancer-health-probe-request-path"=/healthz
```

### Install cert-manager (for TLS)

```bash
# Add cert-manager repository
helm repo add jetstack https://charts.jetstack.io
helm repo update

# Install cert-manager
kubectl apply -f https://github.com/cert-manager/cert-manager/releases/download/v1.13.0/cert-manager.crds.yaml

helm install cert-manager jetstack/cert-manager \
  --namespace cert-manager \
  --create-namespace \
  --version v1.13.0

# Create Let's Encrypt ClusterIssuer
cat <<EOF | kubectl apply -f -
apiVersion: cert-manager.io/v1
kind: ClusterIssuer
metadata:
  name: letsencrypt-prod
spec:
  acme:
    server: https://acme-v02.api.letsencrypt.org/directory
    email: admin@example.com
    privateKeySecretRef:
      name: letsencrypt-prod
    solvers:
    - http01:
        ingress:
          class: nginx
EOF
```

### Deploy Application

Create production values:

```yaml
# values-production.yaml
controlPlane:
  replicaCount: 3
  image:
    repository: bparegistry.azurecr.io/control-plane
    tag: "1.0.0"
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
      - host: api.example.com
        paths:
          - path: /
            pathType: Prefix
    tls:
      - secretName: control-plane-tls
        hosts:
          - api.example.com

nodeRuntime:
  replicaCount: 5
  image:
    repository: bparegistry.azurecr.io/node-runtime
    tag: "1.0.0"
  autoscaling:
    enabled: true
    minReplicas: 5
    maxReplicas: 50

adminUI:
  image:
    repository: bparegistry.azurecr.io/admin-ui
    tag: "1.0.0"
  ingress:
    enabled: true
    className: nginx
    annotations:
      cert-manager.io/cluster-issuer: letsencrypt-prod
    hosts:
      - host: admin.example.com
        paths:
          - path: /
            pathType: Prefix
    tls:
      - secretName: admin-ui-tls
        hosts:
          - admin.example.com

postgresql:
  persistence:
    size: 100Gi
    storageClass: managed-premium
  auth:
    password: <SECURE_PASSWORD>

redis:
  persistence:
    size: 20Gi
    storageClass: managed-premium

nats:
  persistence:
    size: 20Gi
    storageClass: managed-premium

otelCollector:
  enabled: true
```

Deploy:

```bash
helm install bpa ./helm/business-process-agents -f values-production.yaml
```

### Monitor Deployment

```bash
# Watch pods
kubectl get pods -l app.kubernetes.io/instance=bpa -w

# Check service endpoints
kubectl get svc -l app.kubernetes.io/instance=bpa

# View logs
kubectl logs -l app.kubernetes.io/component=control-plane -f

# Get ingress IP
kubectl get ingress
```

## Troubleshooting

### Pods Not Starting

```bash
# Describe pod
kubectl describe pod <pod-name>

# View events
kubectl get events --sort-by=.metadata.creationTimestamp

# Check logs
kubectl logs <pod-name>
```

### Database Connection Issues

```bash
# Test PostgreSQL connection
kubectl exec -it <postgres-pod> -- psql -U postgres -d bpa -c "SELECT 1"

# Check connection string
kubectl get configmap bpa-business-process-agents-control-plane-config -o yaml
```

### Image Pull Errors

```bash
# Check image pull secrets
kubectl get secrets

# Describe pod for detailed error
kubectl describe pod <pod-name>

# For ACR, ensure cluster has pull permissions
az aks update --resource-group bpa-rg --name bpa-cluster --attach-acr bparegistry
```

### Performance Issues

```bash
# Check resource usage
kubectl top nodes
kubectl top pods

# Scale services
helm upgrade bpa ./helm/business-process-agents \
  --set controlPlane.replicaCount=5 \
  --set nodeRuntime.replicaCount=10
```

### Helm Issues

```bash
# List releases
helm list

# Get release status
helm status bpa

# Get release values
helm get values bpa

# Dry run to test
helm install bpa ./helm/business-process-agents --dry-run --debug
```

## Security Best Practices

### Production Checklist

- [ ] Use strong passwords for PostgreSQL
- [ ] Store secrets in Azure Key Vault or Kubernetes Secrets
- [ ] Enable TLS for all external endpoints
- [ ] Configure network policies to restrict pod communication
- [ ] Use signed container images
- [ ] Enable Pod Security Standards (PSS) or Pod Security Admission
- [ ] Configure RBAC appropriately
- [ ] Enable audit logging
- [ ] Regularly update base images
- [ ] Scan images for vulnerabilities

### External Secrets Operator (Recommended for Production)

Install External Secrets Operator:

```bash
helm repo add external-secrets https://charts.external-secrets.io
helm install external-secrets \
  external-secrets/external-secrets \
  -n external-secrets-system \
  --create-namespace
```

Configure Azure Key Vault integration (see AKS documentation).

## Backup and Disaster Recovery

### PostgreSQL Backup

```bash
# Manual backup
kubectl exec <postgres-pod> -- pg_dump -U postgres bpa > backup.sql

# Restore
kubectl exec -i <postgres-pod> -- psql -U postgres bpa < backup.sql
```

### Automated Backups

Consider using:
- Velero for cluster backups
- Azure Backup for AKS
- PostgreSQL WAL archiving

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
```

Access Grafana:

```bash
kubectl port-forward svc/bpa-business-process-agents-grafana 3000:3000
```

Default credentials: admin / admin (change in production)

## Support

For issues and questions:
- GitHub Issues: https://github.com/dylan-mccarthy/Scalable-Process-Agent-System/issues
- Documentation: See README.md and helm/business-process-agents/README.md
