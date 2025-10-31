# Quick Start Guide for AKS Deployment

This is a condensed guide for deploying the Business Process Agents platform to Azure AKS.

## Prerequisites

- Azure subscription
- Azure CLI 2.50+
- kubectl
- Helm 3.8+

## 1. Set Variables

```bash
export SUBSCRIPTION_ID="your-subscription-id"
export RESOURCE_GROUP="bpa-dev-rg"
export LOCATION="eastus"
export POSTGRES_PASSWORD="YourSecurePassword123!"
```

## 2. Deploy Infrastructure

```bash
# Login to Azure
az login
az account set --subscription $SUBSCRIPTION_ID

# Run deployment script
cd infra/scripts
./deploy-azure.sh \
  -e dev \
  -g $RESOURCE_GROUP \
  -l $LOCATION \
  -s $SUBSCRIPTION_ID \
  -p $POSTGRES_PASSWORD
```

This script will create:
- ✅ AKS cluster
- ✅ Azure Database for PostgreSQL
- ✅ Azure Cache for Redis
- ✅ Azure Key Vault
- ✅ Azure OpenAI Service
- ✅ Azure Container Registry
- ✅ Application Insights

**Expected time:** 15-20 minutes

## 3. Build and Push Images

```bash
# Get deployment outputs
AKS_NAME=$(az deployment group show \
  --name bpa-dev-deployment \
  --resource-group $RESOURCE_GROUP \
  --query properties.outputs.aksClusterName.value \
  --output tsv)

ACR_NAME=$(az deployment group show \
  --name bpa-dev-deployment \
  --resource-group $RESOURCE_GROUP \
  --query properties.outputs.acrName.value \
  --output tsv)

# Login to ACR
az acr login --name $ACR_NAME

# Build and push (from repository root)
cd ../..

docker build -t ${ACR_NAME}.azurecr.io/control-plane:latest \
  -f src/ControlPlane.Api/Dockerfile .
docker push ${ACR_NAME}.azurecr.io/control-plane:latest

docker build -t ${ACR_NAME}.azurecr.io/node-runtime:latest \
  -f src/Node.Runtime/Dockerfile .
docker push ${ACR_NAME}.azurecr.io/node-runtime:latest

docker build -t ${ACR_NAME}.azurecr.io/admin-ui:latest \
  -f src/admin-ui/Dockerfile ./src/admin-ui
docker push ${ACR_NAME}.azurecr.io/admin-ui:latest
```

## 4. Configure Kubernetes Secrets

```bash
# Get Key Vault name
KV_NAME=$(az deployment group show \
  --name bpa-dev-deployment \
  --resource-group $RESOURCE_GROUP \
  --query properties.outputs.keyVaultName.value \
  --output tsv)

# Get secrets from Key Vault
POSTGRES_CONN=$(az keyvault secret show \
  --vault-name $KV_NAME \
  --name postgresql-connection-string \
  --query value -o tsv)

REDIS_CONN=$(az keyvault secret show \
  --vault-name $KV_NAME \
  --name redis-connection-string \
  --query value -o tsv)

OPENAI_ENDPOINT=$(az keyvault secret show \
  --vault-name $KV_NAME \
  --name openai-endpoint \
  --query value -o tsv)

OPENAI_KEY=$(az keyvault secret show \
  --vault-name $KV_NAME \
  --name openai-api-key \
  --query value -o tsv)

APPINS_CONN=$(az keyvault secret show \
  --vault-name $KV_NAME \
  --name appinsights-connection-string \
  --query value -o tsv)

# Create Kubernetes secret
kubectl create secret generic azure-secrets \
  --from-literal=postgresql-connection-string="$POSTGRES_CONN" \
  --from-literal=redis-connection-string="$REDIS_CONN" \
  --from-literal=openai-endpoint="$OPENAI_ENDPOINT" \
  --from-literal=openai-api-key="$OPENAI_KEY" \
  --from-literal=appinsights-connection-string="$APPINS_CONN" \
  --dry-run=client -o yaml | kubectl apply -f -
```

## 5. Deploy Application

```bash
# Update Helm values with ACR name
sed -i "s/bpadevacr/${ACR_NAME}/g" infra/helm/values-aks-dev.yaml

# Deploy with Helm
helm install bpa ./helm/business-process-agents \
  -f infra/helm/values-aks-dev.yaml \
  --namespace default \
  --wait
```

## 6. Verify Deployment

```bash
# Check pods
kubectl get pods

# Check services
kubectl get svc

# View logs
kubectl logs -l app.kubernetes.io/component=control-plane --tail=50
```

## 7. Access Services

```bash
# Port forward Control Plane API
kubectl port-forward svc/bpa-business-process-agents-control-plane 8080:8080 &

# Port forward Admin UI
kubectl port-forward svc/bpa-business-process-agents-admin-ui 3000:3000 &

# Test API
curl http://localhost:8080/health
```

## Troubleshooting

### Pod not starting?
```bash
kubectl describe pod <pod-name>
kubectl logs <pod-name>
```

### Database connection issues?
```bash
# Test connection from a debug pod
kubectl run -it --rm debug --image=postgres:16 --restart=Never -- psql "$POSTGRES_CONN"
```

### Check infrastructure deployment
```bash
az deployment group show \
  --name bpa-dev-deployment \
  --resource-group $RESOURCE_GROUP
```

## Cleanup

```bash
# Delete everything
az group delete --name $RESOURCE_GROUP --yes --no-wait
```

## Next Steps

1. Configure ingress with cert-manager for HTTPS
2. Set up monitoring dashboards in Azure Monitor
3. Configure autoscaling policies
4. Deploy production environment

For detailed documentation, see [infra/README.md](infra/README.md).
