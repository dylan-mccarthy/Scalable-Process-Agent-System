#!/bin/bash
# Verify AKS Infrastructure Deployment
# This script checks all resources are properly provisioned

set -e

# Colors
GREEN='\033[0;32m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
NC='\033[0m'

print_success() {
    echo -e "${GREEN}✓${NC} $1"
}

print_error() {
    echo -e "${RED}✗${NC} $1"
}

print_warning() {
    echo -e "${YELLOW}⚠${NC} $1"
}

print_info() {
    echo -e "${GREEN}[INFO]${NC} $1"
}

# Check arguments
if [ $# -lt 1 ]; then
    echo "Usage: $0 <resource-group> [deployment-name]"
    echo "Example: $0 bpa-dev-rg"
    exit 1
fi

RESOURCE_GROUP=$1
DEPLOYMENT_NAME=$2

if [ -z "$DEPLOYMENT_NAME" ]; then
    DEPLOYMENT_NAME=$(az deployment group list \
        --resource-group $RESOURCE_GROUP \
        --query "[?starts_with(name, 'bpa-')].name | [0]" \
        --output tsv)
fi

if [ -z "$DEPLOYMENT_NAME" ]; then
    print_error "Could not find deployment in resource group $RESOURCE_GROUP"
    exit 1
fi

print_info "Verifying deployment: $DEPLOYMENT_NAME"
echo ""

# Get deployment outputs
AKS_NAME=$(az deployment group show \
    --name $DEPLOYMENT_NAME \
    --resource-group $RESOURCE_GROUP \
    --query properties.outputs.aksClusterName.value \
    --output tsv 2>/dev/null)

ACR_NAME=$(az deployment group show \
    --name $DEPLOYMENT_NAME \
    --resource-group $RESOURCE_GROUP \
    --query properties.outputs.acrName.value \
    --output tsv 2>/dev/null)

KV_NAME=$(az deployment group show \
    --name $DEPLOYMENT_NAME \
    --resource-group $RESOURCE_GROUP \
    --query properties.outputs.keyVaultName.value \
    --output tsv 2>/dev/null)

POSTGRES_FQDN=$(az deployment group show \
    --name $DEPLOYMENT_NAME \
    --resource-group $RESOURCE_GROUP \
    --query properties.outputs.postgresServerFqdn.value \
    --output tsv 2>/dev/null)

REDIS_HOST=$(az deployment group show \
    --name $DEPLOYMENT_NAME \
    --resource-group $RESOURCE_GROUP \
    --query properties.outputs.redisHostName.value \
    --output tsv 2>/dev/null)

OPENAI_ENDPOINT=$(az deployment group show \
    --name $DEPLOYMENT_NAME \
    --resource-group $RESOURCE_GROUP \
    --query properties.outputs.openAiEndpoint.value \
    --output tsv 2>/dev/null)

# Check deployment status
DEPLOY_STATE=$(az deployment group show \
    --name $DEPLOYMENT_NAME \
    --resource-group $RESOURCE_GROUP \
    --query properties.provisioningState \
    --output tsv)

if [ "$DEPLOY_STATE" = "Succeeded" ]; then
    print_success "Deployment status: $DEPLOY_STATE"
else
    print_error "Deployment status: $DEPLOY_STATE"
fi

echo ""
print_info "Infrastructure Components:"
echo ""

# Check AKS
if [ -n "$AKS_NAME" ]; then
    AKS_STATE=$(az aks show --resource-group $RESOURCE_GROUP --name $AKS_NAME --query provisioningState --output tsv 2>/dev/null)
    if [ "$AKS_STATE" = "Succeeded" ]; then
        NODE_COUNT=$(az aks show --resource-group $RESOURCE_GROUP --name $AKS_NAME --query agentPoolProfiles[0].count --output tsv)
        print_success "AKS Cluster: $AKS_NAME ($NODE_COUNT nodes)"
    else
        print_error "AKS Cluster: $AKS_NAME (Status: $AKS_STATE)"
    fi
else
    print_error "AKS Cluster: Not found"
fi

# Check ACR
if [ -n "$ACR_NAME" ]; then
    ACR_STATE=$(az acr show --resource-group $RESOURCE_GROUP --name $ACR_NAME --query provisioningState --output tsv 2>/dev/null)
    if [ "$ACR_STATE" = "Succeeded" ]; then
        print_success "Container Registry: $ACR_NAME"
    else
        print_error "Container Registry: $ACR_NAME (Status: $ACR_STATE)"
    fi
else
    print_error "Container Registry: Not found"
fi

# Check Key Vault
if [ -n "$KV_NAME" ]; then
    KV_STATE=$(az keyvault show --resource-group $RESOURCE_GROUP --name $KV_NAME --query properties.provisioningState --output tsv 2>/dev/null)
    if [ "$KV_STATE" = "Succeeded" ]; then
        SECRET_COUNT=$(az keyvault secret list --vault-name $KV_NAME --query "length(@)" --output tsv 2>/dev/null)
        print_success "Key Vault: $KV_NAME ($SECRET_COUNT secrets)"
    else
        print_error "Key Vault: $KV_NAME (Status: $KV_STATE)"
    fi
else
    print_error "Key Vault: Not found"
fi

# Check PostgreSQL
if [ -n "$POSTGRES_FQDN" ]; then
    POSTGRES_NAME=$(echo $POSTGRES_FQDN | cut -d. -f1)
    PG_STATE=$(az postgres flexible-server show --resource-group $RESOURCE_GROUP --name $POSTGRES_NAME --query state --output tsv 2>/dev/null)
    if [ "$PG_STATE" = "Ready" ]; then
        print_success "PostgreSQL: $POSTGRES_NAME (Ready)"
    else
        print_warning "PostgreSQL: $POSTGRES_NAME (Status: $PG_STATE)"
    fi
else
    print_error "PostgreSQL: Not found"
fi

# Check Redis
if [ -n "$REDIS_HOST" ]; then
    REDIS_NAME=$(echo $REDIS_HOST | cut -d. -f1)
    REDIS_STATE=$(az redis show --resource-group $RESOURCE_GROUP --name $REDIS_NAME --query provisioningState --output tsv 2>/dev/null)
    if [ "$REDIS_STATE" = "Succeeded" ]; then
        print_success "Redis Cache: $REDIS_NAME"
    else
        print_warning "Redis Cache: $REDIS_NAME (Status: $REDIS_STATE)"
    fi
else
    print_error "Redis Cache: Not found"
fi

# Check Azure OpenAI
if [ -n "$OPENAI_ENDPOINT" ]; then
    OPENAI_NAME=$(echo $OPENAI_ENDPOINT | sed 's|https://||' | cut -d. -f1)
    OPENAI_STATE=$(az cognitiveservices account show --resource-group $RESOURCE_GROUP --name $OPENAI_NAME --query properties.provisioningState --output tsv 2>/dev/null)
    if [ "$OPENAI_STATE" = "Succeeded" ]; then
        print_success "Azure OpenAI: $OPENAI_NAME"
    else
        print_warning "Azure OpenAI: $OPENAI_NAME (Status: $OPENAI_STATE)"
    fi
else
    print_error "Azure OpenAI: Not found"
fi

echo ""
print_info "Key Vault Secrets:"
echo ""

if [ -n "$KV_NAME" ]; then
    SECRETS=$(az keyvault secret list --vault-name $KV_NAME --query "[].name" --output tsv 2>/dev/null)
    if [ -n "$SECRETS" ]; then
        while IFS= read -r secret; do
            print_success "$secret"
        done <<< "$SECRETS"
    else
        print_warning "No secrets found in Key Vault"
    fi
fi

echo ""
print_info "Next Steps:"
echo ""
echo "1. Get AKS credentials:"
echo "   az aks get-credentials --resource-group $RESOURCE_GROUP --name $AKS_NAME"
echo ""
echo "2. Login to ACR:"
echo "   az acr login --name $ACR_NAME"
echo ""
echo "3. Configure Key Vault secrets for Kubernetes:"
echo "   ./configure-keyvault-secrets.sh $RESOURCE_GROUP $AKS_NAME"
echo ""
echo "4. Deploy application:"
echo "   helm install bpa ./helm/business-process-agents -f infra/helm/values-aks-dev.yaml"
echo ""
