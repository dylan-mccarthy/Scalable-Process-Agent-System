# Azure AI Foundry Integration Guide

This guide explains how to configure and use Azure AI Foundry with the Business Process Agents platform.

## Overview

The Node Runtime integrates with Azure AI Foundry to provide LLM capabilities for agent execution. Azure AI Foundry offers:

- Multiple model deployment options (GPT-4, GPT-3.5, custom models)
- Enterprise-grade security and compliance
- Managed identity authentication
- Regional deployment options
- Cost management and monitoring

## Prerequisites

1. **Azure Subscription**: An active Azure subscription
2. **Azure AI Foundry Resource**: A provisioned Azure AI Foundry resource
3. **Model Deployment**: At least one model deployment (e.g., gpt-4o-mini, gpt-4, etc.)

## Configuration

### 1. Azure AI Foundry Setup

#### Create Azure AI Foundry Resource

```bash
# Create a resource group
az group create --name rg-bpa-agents --location eastus

# Create Azure AI Foundry resource
az cognitiveservices account create \
  --name my-ai-foundry \
  --resource-group rg-bpa-agents \
  --kind AIServices \
  --sku S0 \
  --location eastus
```

#### Deploy a Model

1. Navigate to your Azure AI Foundry resource in the Azure Portal
2. Go to "Deployments" section
3. Create a new deployment:
   - Model: gpt-4o-mini (or your preferred model)
   - Deployment name: gpt-4o-mini
   - Version: Latest
4. Note the endpoint URL: `https://your-resource.openai.azure.com/`

### 2. Node Runtime Configuration

Update `appsettings.json` in the Node.Runtime project:

#### Option A: API Key Authentication

```json
{
  "AgentRuntime": {
    "DefaultModel": "gpt-4o-mini",
    "DefaultTemperature": 0.7,
    "MaxTokens": 4000,
    "MaxDurationSeconds": 60,
    "AzureAIFoundry": {
      "Endpoint": "https://your-resource.openai.azure.com/",
      "DeploymentName": "gpt-4o-mini",
      "ApiKey": "your-api-key-here",
      "UseManagedIdentity": false
    }
  }
}
```

**Security Note**: Never commit API keys to source control. Use one of these approaches:

1. **User Secrets** (Development):
   ```bash
   cd src/Node.Runtime
   dotnet user-secrets set "AgentRuntime:AzureAIFoundry:ApiKey" "your-api-key"
   ```

2. **Environment Variables** (Production):
   ```bash
   export AgentRuntime__AzureAIFoundry__ApiKey="your-api-key"
   ```

3. **Azure Key Vault** (Recommended for Production)

#### Option B: Managed Identity Authentication (Recommended)

```json
{
  "AgentRuntime": {
    "DefaultModel": "gpt-4o-mini",
    "DefaultTemperature": 0.7,
    "MaxTokens": 4000,
    "MaxDurationSeconds": 60,
    "AzureAIFoundry": {
      "Endpoint": "https://your-resource.openai.azure.com/",
      "DeploymentName": "gpt-4o-mini",
      "UseManagedIdentity": true
    }
  }
}
```

**Managed Identity Setup**:

1. Enable system-assigned managed identity on your Node Runtime (AKS, VM, App Service, etc.)
2. Grant the managed identity access to the Azure AI Foundry resource:

```bash
# Get the Node Runtime's managed identity principal ID
PRINCIPAL_ID=$(az <resource-type> show --name <your-node> --resource-group <rg> --query identity.principalId -o tsv)

# Get the Azure AI Foundry resource ID
AI_RESOURCE_ID=$(az cognitiveservices account show --name my-ai-foundry --resource-group rg-bpa-agents --query id -o tsv)

# Assign Cognitive Services User role
az role assignment create \
  --assignee $PRINCIPAL_ID \
  --role "Cognitive Services User" \
  --scope $AI_RESOURCE_ID
```

### 3. Configuration Options

| Option | Required | Default | Description |
|--------|----------|---------|-------------|
| `Endpoint` | Yes | - | Azure AI Foundry endpoint URL |
| `DeploymentName` | Yes | - | Model deployment name |
| `ApiKey` | No* | - | API key for authentication |
| `UseManagedIdentity` | No | false | Use managed identity instead of API key |

\* Required if `UseManagedIdentity` is false or not provided

## Usage

Once configured, the Node Runtime will automatically use Azure AI Foundry for agent execution:

```csharp
// Agent execution happens automatically when a lease is pulled
// No additional code changes needed
```

## Model Selection

Azure AI Foundry supports various models:

### GPT-4 Family
- **gpt-4o**: Latest GPT-4 optimized model
- **gpt-4o-mini**: Smaller, faster, cost-effective GPT-4
- **gpt-4**: Standard GPT-4 model
- **gpt-4-32k**: Extended context window

### GPT-3.5 Family
- **gpt-3.5-turbo**: Fast and cost-effective
- **gpt-3.5-turbo-16k**: Extended context window

### Custom Models
- Deploy your own fine-tuned models
- Use open-source models from the catalog

## Cost Management

### Token Usage Tracking

The platform automatically tracks token usage:

```csharp
public class AgentExecutionResult
{
    public int TokensIn { get; set; }      // Input tokens used
    public int TokensOut { get; set; }     // Output tokens used
    public double UsdCost { get; set; }    // Estimated cost in USD
}
```

### Budget Constraints

Set per-execution limits in agent definitions:

```json
{
  "agentId": "invoice-classifier",
  "budget": {
    "maxTokens": 2000,
    "maxDurationSeconds": 30
  }
}
```

### Monitoring Costs

1. **Azure Portal**: View costs in the Azure AI Foundry resource
2. **Application Logs**: Token usage logged for each execution
3. **OpenTelemetry Metrics**: Track token consumption over time

## Troubleshooting

### Common Issues

#### "Azure AI Foundry configuration is not set"

**Cause**: Missing or incomplete configuration

**Solution**: Ensure `AgentRuntime:AzureAIFoundry` section exists in appsettings.json with `Endpoint` and `DeploymentName`

#### "401 Unauthorized"

**Cause**: Invalid API key or insufficient permissions

**Solutions**:
- Verify API key is correct
- Check managed identity has "Cognitive Services User" role
- Ensure endpoint URL is correct

#### "Model deployment not found"

**Cause**: DeploymentName doesn't match actual deployment

**Solution**: Verify deployment name in Azure Portal matches configuration

#### "Rate limit exceeded"

**Cause**: Too many requests to Azure AI Foundry

**Solutions**:
- Implement request throttling in agent design
- Upgrade to higher pricing tier
- Use multiple deployments with load balancing

### Debug Logging

Enable detailed logging:

```json
{
  "Logging": {
    "LogLevel": {
      "Node.Runtime.Services.AgentExecutorService": "Debug",
      "Node.Runtime.Services.AzureAIFoundryChatClient": "Debug"
    }
  }
}
```

## Security Best Practices

1. **Never commit secrets**: Use User Secrets, Key Vault, or environment variables
2. **Use Managed Identity**: Preferred over API keys in production
3. **Network Security**: 
   - Use private endpoints for Azure AI Foundry
   - Restrict network access with firewalls
4. **Role-Based Access**: Grant least-privilege access
5. **Monitor Access**: Enable diagnostic logs and alerts
6. **Rotate Keys**: Regularly rotate API keys if used

## Performance Optimization

### Connection Pooling

The platform automatically manages connections to Azure AI Foundry.

### Request Caching

Consider implementing caching for repetitive requests:

```csharp
// Cache common classification results
// Implement in custom agent logic
```

### Regional Deployment

Deploy Azure AI Foundry resources close to your Node Runtime instances:

```bash
# Deploy in same region as AKS cluster
az cognitiveservices account create \
  --name my-ai-foundry-westus \
  --resource-group rg-bpa-agents \
  --location westus
```

## Examples

### Invoice Classification Agent

```json
{
  "agentId": "invoice-classifier",
  "name": "Invoice Classifier",
  "instructions": "Classify invoices into categories: urgent, normal, low-priority",
  "modelProfile": {
    "temperature": 0.3,
    "maxTokens": 500
  },
  "budget": {
    "maxTokens": 1000,
    "maxDurationSeconds": 20
  }
}
```

### Customer Support Agent

```json
{
  "agentId": "customer-support",
  "name": "Customer Support Assistant",
  "instructions": "Help customers with common questions. Be polite and concise.",
  "modelProfile": {
    "temperature": 0.7,
    "maxTokens": 1000
  }
}
```

## Related Documentation

- [System Architecture Document](../sad.md)
- [Azure AI Foundry Documentation](https://learn.microsoft.com/en-us/azure/ai-foundry/)
- [Azure AI Foundry Tool Registry](AZURE_AI_FOUNDRY_TOOLS.md)
- [Authentication Guide](../AUTHENTICATION.md)

## Support

For issues or questions:
- Create an issue in the GitHub repository
- Contact Platform Engineering team
- Check Azure AI Foundry status page
