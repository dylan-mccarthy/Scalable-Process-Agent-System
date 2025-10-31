#!/bin/bash
# Deploy Business Process Agents Infrastructure to Azure
# Task: E6-T3 - AKS environment provisioning

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Default values
ENVIRONMENT="dev"
LOCATION="eastus"
RESOURCE_GROUP=""
SUBSCRIPTION_ID=""
POSTGRES_PASSWORD=""

# Function to print colored output
print_info() {
    echo -e "${GREEN}[INFO]${NC} $1"
}

print_warning() {
    echo -e "${YELLOW}[WARNING]${NC} $1"
}

print_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

# Function to display usage
usage() {
    cat << EOF
Usage: $0 [OPTIONS]

Deploy Business Process Agents infrastructure to Azure AKS.

OPTIONS:
    -e, --environment ENV       Environment (dev, staging, prod). Default: dev
    -l, --location LOCATION     Azure region. Default: eastus
    -g, --resource-group RG     Resource group name (required)
    -s, --subscription SUB      Azure subscription ID (required)
    -p, --postgres-password PWD PostgreSQL admin password (required)
    -h, --help                  Display this help message

EXAMPLE:
    $0 -e dev -g bpa-dev-rg -s 12345678-1234-1234-1234-123456789012 -p 'MySecurePassword123!'

EOF
    exit 1
}

# Parse command line arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        -e|--environment)
            ENVIRONMENT="$2"
            shift 2
            ;;
        -l|--location)
            LOCATION="$2"
            shift 2
            ;;
        -g|--resource-group)
            RESOURCE_GROUP="$2"
            shift 2
            ;;
        -s|--subscription)
            SUBSCRIPTION_ID="$2"
            shift 2
            ;;
        -p|--postgres-password)
            POSTGRES_PASSWORD="$2"
            shift 2
            ;;
        -h|--help)
            usage
            ;;
        *)
            print_error "Unknown option: $1"
            usage
            ;;
    esac
done

# Validate required parameters
if [[ -z "$RESOURCE_GROUP" ]]; then
    print_error "Resource group is required"
    usage
fi

if [[ -z "$SUBSCRIPTION_ID" ]]; then
    print_error "Subscription ID is required"
    usage
fi

if [[ -z "$POSTGRES_PASSWORD" ]]; then
    print_error "PostgreSQL password is required"
    usage
fi

# Validate environment
if [[ ! "$ENVIRONMENT" =~ ^(dev|staging|prod)$ ]]; then
    print_error "Invalid environment. Must be one of: dev, staging, prod"
    exit 1
fi

print_info "Starting deployment with the following configuration:"
echo "  Environment: $ENVIRONMENT"
echo "  Location: $LOCATION"
echo "  Resource Group: $RESOURCE_GROUP"
echo "  Subscription: $SUBSCRIPTION_ID"
echo ""

# Check if Azure CLI is installed
if ! command -v az &> /dev/null; then
    print_error "Azure CLI is not installed. Please install it from https://docs.microsoft.com/en-us/cli/azure/install-azure-cli"
    exit 1
fi

# Login check
print_info "Checking Azure CLI login status..."
if ! az account show &> /dev/null; then
    print_warning "Not logged in to Azure CLI. Please log in."
    az login
fi

# Set subscription
print_info "Setting subscription to $SUBSCRIPTION_ID..."
az account set --subscription "$SUBSCRIPTION_ID"

# Create resource group if it doesn't exist
print_info "Creating resource group $RESOURCE_GROUP in $LOCATION..."
az group create \
    --name "$RESOURCE_GROUP" \
    --location "$LOCATION" \
    --tags Environment="$ENVIRONMENT" Project="BusinessProcessAgents" ManagedBy="Bicep"

# Validate Bicep template
print_info "Validating Bicep template..."
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
BICEP_DIR="$SCRIPT_DIR/../bicep"

az deployment group validate \
    --resource-group "$RESOURCE_GROUP" \
    --template-file "$BICEP_DIR/main.bicep" \
    --parameters "$BICEP_DIR/main.parameters.$ENVIRONMENT.json" \
    --parameters postgresAdminPassword="$POSTGRES_PASSWORD"

if [[ $? -ne 0 ]]; then
    print_error "Bicep template validation failed"
    exit 1
fi

print_info "Template validation successful"

# Deploy infrastructure
print_info "Deploying infrastructure to Azure..."
DEPLOYMENT_NAME="bpa-$ENVIRONMENT-$(date +%Y%m%d-%H%M%S)"

az deployment group create \
    --name "$DEPLOYMENT_NAME" \
    --resource-group "$RESOURCE_GROUP" \
    --template-file "$BICEP_DIR/main.bicep" \
    --parameters "$BICEP_DIR/main.parameters.$ENVIRONMENT.json" \
    --parameters postgresAdminPassword="$POSTGRES_PASSWORD" \
    --verbose

if [[ $? -ne 0 ]]; then
    print_error "Deployment failed"
    exit 1
fi

print_info "Deployment completed successfully!"

# Get outputs
print_info "Retrieving deployment outputs..."

AKS_NAME=$(az deployment group show \
    --name "$DEPLOYMENT_NAME" \
    --resource-group "$RESOURCE_GROUP" \
    --query properties.outputs.aksClusterName.value \
    --output tsv)

ACR_NAME=$(az deployment group show \
    --name "$DEPLOYMENT_NAME" \
    --resource-group "$RESOURCE_GROUP" \
    --query properties.outputs.acrName.value \
    --output tsv)

KV_NAME=$(az deployment group show \
    --name "$DEPLOYMENT_NAME" \
    --resource-group "$RESOURCE_GROUP" \
    --query properties.outputs.keyVaultName.value \
    --output tsv)

print_info "Deployment Summary:"
echo "  AKS Cluster: $AKS_NAME"
echo "  Container Registry: $ACR_NAME"
echo "  Key Vault: $KV_NAME"
echo ""

# Get AKS credentials
print_info "Getting AKS credentials..."
az aks get-credentials \
    --resource-group "$RESOURCE_GROUP" \
    --name "$AKS_NAME" \
    --overwrite-existing

# Verify cluster access
print_info "Verifying cluster access..."
kubectl cluster-info

print_info "Next steps:"
echo "  1. Build and push container images to ACR: az acr login --name $ACR_NAME"
echo "  2. Deploy application using Helm: helm install bpa ./helm/business-process-agents -f infra/helm/values-aks-$ENVIRONMENT.yaml"
echo "  3. Configure kubectl context: kubectl config use-context $AKS_NAME"
echo ""

print_info "Deployment complete! 🎉"
