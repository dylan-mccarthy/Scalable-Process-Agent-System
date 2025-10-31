#!/bin/bash
# Configure Azure Key Vault Secrets Provider for AKS
# This script sets up the SecretProviderClass to sync secrets from Key Vault to Kubernetes

set -e

# Colors
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m'

print_info() {
    echo -e "${GREEN}[INFO]${NC} $1"
}

print_warning() {
    echo -e "${YELLOW}[WARNING]${NC} $1"
}

# Check arguments
if [ $# -lt 2 ]; then
    echo "Usage: $0 <resource-group> <aks-name> [namespace]"
    echo "Example: $0 bpa-dev-rg bpa-dev-aks default"
    exit 1
fi

RESOURCE_GROUP=$1
AKS_NAME=$2
NAMESPACE=${3:-default}

print_info "Configuring Key Vault Secrets Provider for AKS cluster: $AKS_NAME"

# Get Key Vault name from deployment
DEPLOYMENT_NAME=$(az deployment group list \
    --resource-group $RESOURCE_GROUP \
    --query "[?starts_with(name, 'bpa-')].name | [0]" \
    --output tsv)

if [ -z "$DEPLOYMENT_NAME" ]; then
    print_warning "Could not find deployment. Please specify Key Vault name manually."
    read -p "Enter Key Vault name: " KV_NAME
else
    KV_NAME=$(az deployment group show \
        --name $DEPLOYMENT_NAME \
        --resource-group $RESOURCE_GROUP \
        --query properties.outputs.keyVaultName.value \
        --output tsv)
fi

print_info "Using Key Vault: $KV_NAME"

# Get tenant ID
TENANT_ID=$(az account show --query tenantId --output tsv)

# Get addon identity
ADDON_IDENTITY=$(az aks show \
    --resource-group $RESOURCE_GROUP \
    --name $AKS_NAME \
    --query addonProfiles.azureKeyvaultSecretsProvider.identity.clientId \
    --output tsv)

print_info "Addon Identity Client ID: $ADDON_IDENTITY"
print_info "Tenant ID: $TENANT_ID"

# Create SecretProviderClass
cat <<EOF | kubectl apply -f -
apiVersion: secrets-store.csi.x-k8s.io/v1
kind: SecretProviderClass
metadata:
  name: azure-secrets
  namespace: $NAMESPACE
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
    userAssignedIdentityID: "$ADDON_IDENTITY"
    keyvaultName: "$KV_NAME"
    tenantId: "$TENANT_ID"
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

print_info "SecretProviderClass created successfully"

# Create a test pod to trigger secret sync
print_info "Creating test pod to trigger secret synchronization..."

cat <<EOF | kubectl apply -f -
apiVersion: v1
kind: Pod
metadata:
  name: secrets-test-pod
  namespace: $NAMESPACE
spec:
  containers:
  - name: busybox
    image: busybox:latest
    command:
    - sleep
    - "3600"
    volumeMounts:
    - name: secrets-store
      mountPath: "/mnt/secrets-store"
      readOnly: true
  volumes:
  - name: secrets-store
    csi:
      driver: secrets-store.csi.k8s.io
      readOnly: true
      volumeAttributes:
        secretProviderClass: "azure-secrets"
EOF

print_info "Waiting for pod to start..."
kubectl wait --for=condition=ready pod/secrets-test-pod --namespace=$NAMESPACE --timeout=120s

print_info "Verifying secret creation..."
if kubectl get secret azure-secrets --namespace=$NAMESPACE &> /dev/null; then
    print_info "✓ Secret 'azure-secrets' created successfully"
    echo ""
    echo "Available secret keys:"
    kubectl get secret azure-secrets --namespace=$NAMESPACE -o jsonpath='{.data}' | jq -r 'keys[]' 2>/dev/null || kubectl get secret azure-secrets --namespace=$NAMESPACE -o json | grep -o '"[^"]*":' | tr -d '":' | grep -v metadata
else
    print_warning "Secret not created yet. Check pod status:"
    kubectl describe pod secrets-test-pod --namespace=$NAMESPACE
fi

print_info "Cleaning up test pod..."
kubectl delete pod secrets-test-pod --namespace=$NAMESPACE

print_info "Configuration complete!"
echo ""
echo "The SecretProviderClass 'azure-secrets' is now available in namespace '$NAMESPACE'"
echo "Pods can mount secrets by adding this volume configuration:"
echo ""
echo "  volumes:"
echo "  - name: secrets-store"
echo "    csi:"
echo "      driver: secrets-store.csi.k8s.io"
echo "      readOnly: true"
echo "      volumeAttributes:"
echo "        secretProviderClass: \"azure-secrets\""
