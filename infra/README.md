# Azure Infrastructure for Business Process Agents

This directory contains the Infrastructure as Code (IaC) for deploying the Business Process Agents platform to Azure Kubernetes Service (AKS).

**Task:** E6-T3 - AKS environment provisioning

## Overview

The infrastructure provisions the following Azure resources:

- **Azure Kubernetes Service (AKS)** - Managed Kubernetes cluster
- **Azure Database for PostgreSQL Flexible Server** - Managed PostgreSQL database
- **Azure Cache for Redis** - Managed Redis cache
- **Azure Key Vault** - Secrets management with RBAC
- **Azure OpenAI Service** - LLM integration (Azure AI Foundry)
- **Azure Container Registry (ACR)** - Container image registry
- **Azure Monitor & Application Insights** - Observability and monitoring
- **Log Analytics Workspace** - Centralized logging

## Directory Structure

```
infra/
├── bicep/
│   ├── main.bicep                      # Main infrastructure template
│   ├── main.parameters.dev.json        # Development environment parameters
│   └── main.parameters.prod.json       # Production environment parameters
├── helm/
│   ├── values-aks-dev.yaml             # Helm values for dev AKS deployment
│   └── values-aks-prod.yaml            # Helm values for prod AKS deployment
├── scripts/
│   └── deploy-azure.sh                 # Deployment automation script
└── README.md                           # This file
```

## Prerequisites

### Required Tools

1. **Azure CLI** - version 2.50.0 or later
   ```bash
   curl -sL https://aka.ms/InstallAzureCLIDeb | sudo bash
   ```

2. **kubectl** - Kubernetes CLI
   ```bash
   az aks install-cli
   ```

3. **Helm** - version 3.8 or later
   ```bash
   curl https://raw.githubusercontent.com/helm/helm/main/scripts/get-helm-3 | bash
   ```

4. **Bicep CLI** (included with Azure CLI 2.20+)
   ```bash
   az bicep install
   ```

### Azure Subscription

- Active Azure subscription with appropriate permissions
- Contributor or Owner role on the subscription or resource group
- Ability to create service principals (for CI/CD)

### Domain Names (for production)

- DNS records for ingress endpoints:
  - `api.bpa.example.com` - Control Plane API
  - `admin.bpa.example.com` - Admin UI

## Quick Start

### 1. Login to Azure

```bash
az login
az account set --subscription "<your-subscription-id>"
```

### 2. Set Environment Variables

```bash
export RESOURCE_GROUP="bpa-dev-rg"
export LOCATION="eastus"
export ENVIRONMENT="dev"
export SUBSCRIPTION_ID="<your-subscription-id>"
export POSTGRES_PASSWORD="<secure-password>"
```

### 3. Deploy Infrastructure

```bash
cd infra/scripts
./deploy-azure.sh \
  -e dev \
  -g "$RESOURCE_GROUP" \
  -l "$LOCATION" \
  -s "$SUBSCRIPTION_ID" \
  -p "$POSTGRES_PASSWORD"
```

### 4. Verify Deployment

```bash
# Get AKS credentials
az aks get-credentials --resource-group "$RESOURCE_GROUP" --name "bpa-dev-aks"

# Verify cluster access
kubectl cluster-info
kubectl get nodes
```

## Detailed Deployment Steps

### Development Environment

1. **Create Resource Group**
   ```bash
   az group create \
     --name bpa-dev-rg \
     --location eastus
   ```

2. **Deploy Infrastructure**
   ```bash
   cd infra/bicep
   
   az deployment group create \
     --name bpa-dev-deployment \
     --resource-group bpa-dev-rg \
     --template-file main.bicep \
     --parameters main.parameters.dev.json \
     --parameters postgresAdminPassword="<secure-password>"
   ```

3. **Get Deployment Outputs**
   ```bash
   # AKS cluster name
   az deployment group show \
     --name bpa-dev-deployment \
     --resource-group bpa-dev-rg \
     --query properties.outputs.aksClusterName.value \
     --output tsv
   
   # Key Vault name
   az deployment group show \
     --name bpa-dev-deployment \
     --resource-group bpa-dev-rg \
     --query properties.outputs.keyVaultName.value \
     --output tsv
   ```

4. **Configure kubectl**
   ```bash
   az aks get-credentials \
     --resource-group bpa-dev-rg \
     --name bpa-dev-aks \
     --overwrite-existing
   ```

5. **Create Kubernetes Secret from Key Vault**
   
   The infrastructure includes Azure Key Vault Secrets Provider add-on. Create a SecretProviderClass to sync secrets:
   
   ```bash
   cat <<EOF | kubectl apply -f -
   apiVersion: secrets-store.csi.x-k8s.io/v1
   kind: SecretProviderClass
   metadata:
     name: azure-secrets
     namespace: default
   spec:
     provider: azure
     secretObjects:
     - secretName: azure-secrets
       type: Opaque
       data:
       - objectName: postgresql-connection-string
         key: postgresql-connection-string
       - objectName: redis-connection-string
         key: redis-connection-string
       - objectName: openai-endpoint
         key: openai-endpoint
       - objectName: openai-api-key
         key: openai-api-key
       - objectName: appinsights-connection-string
         key: appinsights-connection-string
     parameters:
       usePodIdentity: "false"
       useVMManagedIdentity: "true"
       userAssignedIdentityID: "<client-id>"
       keyvaultName: "<key-vault-name>"
       tenantId: "<tenant-id>"
       objects: |
         array:
           - |
             objectName: postgresql-connection-string
             objectType: secret
           - |
             objectName: redis-connection-string
             objectType: secret
           - |
             objectName: openai-endpoint
             objectType: secret
           - |
             objectName: openai-api-key
             objectType: secret
           - |
             objectName: appinsights-connection-string
             objectType: secret
   EOF
   ```

6. **Build and Push Container Images**
   ```bash
   # Login to ACR
   az acr login --name bpadevacr
   
   # Build and push images (from repository root)
   docker build -t bpadevacr.azurecr.io/control-plane:latest \
     -f src/ControlPlane.Api/Dockerfile .
   docker push bpadevacr.azurecr.io/control-plane:latest
   
   docker build -t bpadevacr.azurecr.io/node-runtime:latest \
     -f src/Node.Runtime/Dockerfile .
   docker push bpadevacr.azurecr.io/node-runtime:latest
   
   docker build -t bpadevacr.azurecr.io/admin-ui:latest \
     -f src/admin-ui/Dockerfile ./src/admin-ui
   docker push bpadevacr.azurecr.io/admin-ui:latest
   ```

7. **Deploy Application with Helm**
   ```bash
   # Install cert-manager for TLS certificates (if using ingress)
   kubectl apply -f https://github.com/cert-manager/cert-manager/releases/download/v1.13.0/cert-manager.yaml
   
   # Deploy application
   helm install bpa ./helm/business-process-agents \
     -f infra/helm/values-aks-dev.yaml \
     --namespace default
   ```

8. **Run Database Migrations**
   ```bash
   kubectl run migration-job --rm -it \
     --image=bpadevacr.azurecr.io/control-plane:latest \
     --restart=Never \
     --env="ConnectionStrings__DefaultConnection=<connection-string>" \
     -- dotnet ef database update
   ```

### Production Environment

Follow the same steps as development but use:
- `main.parameters.prod.json` for infrastructure parameters
- `values-aks-prod.yaml` for Helm values
- Production resource group (e.g., `bpa-prod-rg`)
- Production ACR name (e.g., `bpaprodacr`)

Additional production considerations:
- Use Azure Front Door or Application Gateway for global load balancing
- Enable Azure DDoS Protection
- Configure backup and disaster recovery
- Set up Azure Monitor alerts
- Configure Network Security Groups (NSGs)
- Enable Azure Policy for governance

## Configuration

### Infrastructure Parameters

Edit `main.parameters.{env}.json` to customize:

- **aksConfig**: AKS cluster configuration (node count, VM size, auto-scaling)
- **postgresConfig**: PostgreSQL configuration (SKU, storage, version)
- **redisConfig**: Redis configuration (SKU, family, capacity)
- **openAiConfig**: Azure OpenAI configuration (deployment, model, capacity)

### Helm Values

Edit `values-aks-{env}.yaml` to customize:

- Image repositories and tags
- Resource requests and limits
- Autoscaling settings
- Ingress configuration
- Environment variables
- Feature flags

## Key Vault Integration

All sensitive configuration is stored in Azure Key Vault:

| Secret Name | Description |
|-------------|-------------|
| `postgresql-connection-string` | PostgreSQL connection string |
| `redis-connection-string` | Redis connection string |
| `openai-endpoint` | Azure OpenAI endpoint URL |
| `openai-api-key` | Azure OpenAI API key |
| `appinsights-connection-string` | Application Insights connection string |

Secrets are automatically synced to Kubernetes using the Azure Key Vault Secrets Provider add-on.

## Azure OpenAI Integration

The infrastructure provisions Azure OpenAI (Azure AI Foundry) with:

- **Model**: GPT-4o (configurable)
- **Deployment**: Dedicated capacity
- **Integration**: Endpoint and API key stored in Key Vault
- **Usage**: Node Runtime uses Azure OpenAI for LLM operations

To use the Azure OpenAI service:

```bash
# Get the endpoint
az cognitiveservices account show \
  --name bpa-dev-openai \
  --resource-group bpa-dev-rg \
  --query properties.endpoint \
  --output tsv

# Get the API key
az cognitiveservices account keys list \
  --name bpa-dev-openai \
  --resource-group bpa-dev-rg \
  --query key1 \
  --output tsv
```

## Monitoring and Observability

The infrastructure includes:

- **Application Insights**: Application performance monitoring
- **Log Analytics**: Centralized logging
- **Container Insights**: AKS monitoring
- **OpenTelemetry Collector**: Metrics, traces, and logs collection

Access monitoring:

```bash
# Get Application Insights instrumentation key
az monitor app-insights component show \
  --app bpa-dev-appins \
  --resource-group bpa-dev-rg \
  --query instrumentationKey \
  --output tsv

# View logs in Log Analytics
az monitor log-analytics workspace show \
  --workspace-name bpa-dev-logs \
  --resource-group bpa-dev-rg
```

## Troubleshooting

### Deployment Failures

1. **Check deployment status**
   ```bash
   az deployment group show \
     --name <deployment-name> \
     --resource-group <resource-group> \
     --query properties.provisioningState
   ```

2. **View deployment errors**
   ```bash
   az deployment operation group list \
     --name <deployment-name> \
     --resource-group <resource-group> \
     --query "[?properties.provisioningState=='Failed']"
   ```

### AKS Cluster Issues

1. **Check node status**
   ```bash
   kubectl get nodes
   kubectl describe node <node-name>
   ```

2. **View cluster events**
   ```bash
   kubectl get events --sort-by=.metadata.creationTimestamp
   ```

3. **Check AKS diagnostics**
   ```bash
   az aks show \
     --resource-group <resource-group> \
     --name <cluster-name>
   ```

### Key Vault Access Issues

1. **Verify addon identity has access**
   ```bash
   az role assignment list \
     --scope /subscriptions/<sub-id>/resourceGroups/<rg>/providers/Microsoft.KeyVault/vaults/<kv-name>
   ```

2. **Test secret retrieval**
   ```bash
   az keyvault secret show \
     --vault-name <key-vault-name> \
     --name postgresql-connection-string
   ```

## Security Best Practices

1. **Use managed identities** - Avoid storing credentials in code or config
2. **Enable RBAC** - Use Azure RBAC for Key Vault and AKS
3. **Network isolation** - Configure private endpoints for production
4. **TLS everywhere** - Enable TLS for all services
5. **Scan images** - Use ACR vulnerability scanning
6. **Rotate secrets** - Regularly rotate database passwords and API keys
7. **Monitor access** - Enable Azure Monitor alerts for suspicious activity
8. **Backup data** - Configure automated backups for PostgreSQL

## Cost Optimization

1. **Right-size resources** - Start with smaller SKUs and scale as needed
2. **Use auto-scaling** - Enable HPA for pods and cluster autoscaler for nodes
3. **Reserved instances** - Consider Azure Reserved Instances for long-term workloads
4. **Shutdown dev environments** - Stop non-production clusters when not in use
5. **Monitor spending** - Set up Azure Cost Management alerts

## Cleanup

To delete all resources:

```bash
# Delete the entire resource group (CAUTION: This deletes everything!)
az group delete --name <resource-group> --yes --no-wait

# Or delete specific deployment
az deployment group delete \
  --name <deployment-name> \
  --resource-group <resource-group>
```

## CI/CD Integration

For automated deployments, create a service principal:

```bash
az ad sp create-for-rbac \
  --name "bpa-cicd" \
  --role contributor \
  --scopes /subscriptions/<subscription-id>/resourceGroups/<resource-group> \
  --sdk-auth
```

Store the output in GitHub Secrets for use in GitHub Actions workflows.

## References

- [Azure Kubernetes Service Documentation](https://docs.microsoft.com/azure/aks/)
- [Azure Database for PostgreSQL](https://docs.microsoft.com/azure/postgresql/)
- [Azure Cache for Redis](https://docs.microsoft.com/azure/azure-cache-for-redis/)
- [Azure Key Vault](https://docs.microsoft.com/azure/key-vault/)
- [Azure OpenAI Service](https://docs.microsoft.com/azure/cognitive-services/openai/)
- [Bicep Documentation](https://docs.microsoft.com/azure/azure-resource-manager/bicep/)

## Support

For issues or questions:
- GitHub Issues: https://github.com/dylan-mccarthy/Scalable-Process-Agent-System/issues
- Epic/Task: E6-T3 - AKS environment provisioning
