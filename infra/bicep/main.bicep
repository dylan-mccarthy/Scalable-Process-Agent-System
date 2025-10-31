// Main Bicep template for Business Process Agents AKS Infrastructure
// Task: E6-T3 - AKS environment provisioning

targetScope = 'resourceGroup'

// ============================================================================
// Parameters
// ============================================================================

@description('The location for all resources')
param location string = resourceGroup().location

@description('Environment name (dev, staging, prod)')
@allowed([
  'dev'
  'staging'
  'prod'
])
param environment string = 'dev'

@description('Base name for all resources')
param baseName string = 'bpa'

@description('AKS cluster configuration')
param aksConfig object = {
  nodeCount: 3
  nodeVmSize: 'Standard_D4s_v3'
  enableAutoScaling: true
  minNodeCount: 2
  maxNodeCount: 10
  kubernetesVersion: '1.28'
}

@description('PostgreSQL configuration')
param postgresConfig object = {
  administratorLogin: 'bpaadmin'
  skuName: 'Standard_D2ds_v4'
  storageSizeGB: 128
  version: '16'
}

@description('Redis configuration')
param redisConfig object = {
  skuName: 'Standard'
  skuFamily: 'C'
  capacity: 1
}

@description('Azure OpenAI configuration')
param openAiConfig object = {
  deploymentName: 'gpt-4o'
  modelName: 'gpt-4o'
  modelVersion: '2024-08-06'
  skuName: 'Standard'
  capacity: 30
}

@description('Administrator password for PostgreSQL (should be stored in Key Vault)')
@secure()
param postgresAdminPassword string

@description('Tags to apply to all resources')
param tags object = {
  Environment: environment
  Project: 'BusinessProcessAgents'
  ManagedBy: 'Bicep'
  Task: 'E6-T3'
}

// ============================================================================
// Variables
// ============================================================================

var resourcePrefix = '${baseName}-${environment}'
var aksClusterName = '${resourcePrefix}-aks'
var acrName = replace('${resourcePrefix}acr', '-', '')
var keyVaultName = '${resourcePrefix}-kv'
var postgresServerName = '${resourcePrefix}-pg'
var redisName = '${resourcePrefix}-redis'
var openAiAccountName = '${resourcePrefix}-openai'
var logAnalyticsName = '${resourcePrefix}-logs'
var appInsightsName = '${resourcePrefix}-appins'

// ============================================================================
// Modules and Resources
// ============================================================================

// Log Analytics Workspace for AKS monitoring
resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: logAnalyticsName
  location: location
  tags: tags
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 30
  }
}

// Application Insights for application monitoring
resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: appInsightsName
  location: location
  tags: tags
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logAnalytics.id
    publicNetworkAccessForIngestion: 'Enabled'
    publicNetworkAccessForQuery: 'Enabled'
  }
}

// Azure Container Registry
resource acr 'Microsoft.ContainerRegistry/registries@2023-11-01-preview' = {
  name: acrName
  location: location
  tags: tags
  sku: {
    name: 'Standard'
  }
  properties: {
    adminUserEnabled: false
    publicNetworkAccess: 'Enabled'
    networkRuleBypassOptions: 'AzureServices'
  }
}

// AKS Cluster
resource aks 'Microsoft.ContainerService/managedClusters@2024-02-01' = {
  name: aksClusterName
  location: location
  tags: tags
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    kubernetesVersion: aksConfig.kubernetesVersion
    dnsPrefix: aksClusterName
    enableRBAC: true
    
    agentPoolProfiles: [
      {
        name: 'system'
        count: aksConfig.nodeCount
        vmSize: aksConfig.nodeVmSize
        osType: 'Linux'
        mode: 'System'
        enableAutoScaling: aksConfig.enableAutoScaling
        minCount: aksConfig.enableAutoScaling ? aksConfig.minNodeCount : null
        maxCount: aksConfig.enableAutoScaling ? aksConfig.maxNodeCount : null
        type: 'VirtualMachineScaleSets'
      }
    ]
    
    networkProfile: {
      networkPlugin: 'azure'
      networkPolicy: 'azure'
      serviceCidr: '10.0.0.0/16'
      dnsServiceIP: '10.0.0.10'
    }
    
    addonProfiles: {
      omsagent: {
        enabled: true
        config: {
          logAnalyticsWorkspaceResourceID: logAnalytics.id
        }
      }
      azureKeyvaultSecretsProvider: {
        enabled: true
        config: {
          enableSecretRotation: 'true'
          rotationPollInterval: '2m'
        }
      }
    }
    
    oidcIssuerProfile: {
      enabled: true
    }
    
    securityProfile: {
      workloadIdentity: {
        enabled: true
      }
    }
  }
}

// Assign ACR Pull role to AKS
resource acrPullRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(acr.id, aks.id, 'AcrPull')
  scope: acr
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '7f951dda-4ed3-4680-a7ca-43fe172d538d') // AcrPull role
    principalId: aks.properties.identityProfile.kubeletidentity.objectId
    principalType: 'ServicePrincipal'
  }
}

// Azure Database for PostgreSQL Flexible Server
resource postgresServer 'Microsoft.DBforPostgreSQL/flexibleServers@2023-06-01-preview' = {
  name: postgresServerName
  location: location
  tags: tags
  sku: {
    name: postgresConfig.skuName
    tier: 'GeneralPurpose'
  }
  properties: {
    administratorLogin: postgresConfig.administratorLogin
    administratorLoginPassword: postgresAdminPassword
    version: postgresConfig.version
    storage: {
      storageSizeGB: postgresConfig.storageSizeGB
      autoGrow: 'Enabled'
    }
    backup: {
      backupRetentionDays: 7
      geoRedundantBackup: 'Disabled'
    }
    highAvailability: {
      mode: 'Disabled'
    }
  }
}

// PostgreSQL Database
resource postgresDatabase 'Microsoft.DBforPostgreSQL/flexibleServers/databases@2023-06-01-preview' = {
  parent: postgresServer
  name: 'bpa'
  properties: {
    charset: 'UTF8'
    collation: 'en_US.utf8'
  }
}

// PostgreSQL Firewall Rule - Allow Azure Services
resource postgresFirewallAzure 'Microsoft.DBforPostgreSQL/flexibleServers/firewallRules@2023-06-01-preview' = {
  parent: postgresServer
  name: 'AllowAzureServices'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

// Azure Cache for Redis
resource redis 'Microsoft.Cache/redis@2023-08-01' = {
  name: redisName
  location: location
  tags: tags
  properties: {
    sku: {
      name: redisConfig.skuName
      family: redisConfig.skuFamily
      capacity: redisConfig.capacity
    }
    enableNonSslPort: false
    minimumTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled'
    redisConfiguration: {
      'maxmemory-policy': 'allkeys-lru'
    }
  }
}

// Azure OpenAI Account
resource openAiAccount 'Microsoft.CognitiveServices/accounts@2023-10-01-preview' = {
  name: openAiAccountName
  location: location
  tags: tags
  kind: 'OpenAI'
  sku: {
    name: 'S0'
  }
  properties: {
    customSubDomainName: openAiAccountName
    publicNetworkAccess: 'Enabled'
    networkAcls: {
      defaultAction: 'Allow'
    }
  }
}

// Azure OpenAI Deployment
resource openAiDeployment 'Microsoft.CognitiveServices/accounts/deployments@2023-10-01-preview' = {
  parent: openAiAccount
  name: openAiConfig.deploymentName
  sku: {
    name: openAiConfig.skuName
    capacity: openAiConfig.capacity
  }
  properties: {
    model: {
      format: 'OpenAI'
      name: openAiConfig.modelName
      version: openAiConfig.modelVersion
    }
  }
}

// Azure Key Vault
resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: keyVaultName
  location: location
  tags: tags
  properties: {
    sku: {
      family: 'A'
      name: 'standard'
    }
    tenantId: subscription().tenantId
    enableRbacAuthorization: true
    enableSoftDelete: true
    softDeleteRetentionInDays: 7
    publicNetworkAccess: 'Enabled'
    networkAcls: {
      defaultAction: 'Allow'
      bypass: 'AzureServices'
    }
  }
}

// Store PostgreSQL connection string in Key Vault
resource kvSecretPostgres 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'postgresql-connection-string'
  properties: {
    value: 'Host=${postgresServer.properties.fullyQualifiedDomainName};Database=bpa;Username=${postgresConfig.administratorLogin};Password=${postgresAdminPassword};SSL Mode=Require'
  }
}

// Store Redis connection string in Key Vault
resource kvSecretRedis 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'redis-connection-string'
  properties: {
    value: '${redis.properties.hostName}:${redis.properties.sslPort},password=${redis.listKeys().primaryKey},ssl=True,abortConnect=False'
  }
}

// Store Azure OpenAI endpoint in Key Vault
resource kvSecretOpenAiEndpoint 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'openai-endpoint'
  properties: {
    value: openAiAccount.properties.endpoint
  }
}

// Store Azure OpenAI API key in Key Vault
resource kvSecretOpenAiKey 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'openai-api-key'
  properties: {
    value: openAiAccount.listKeys().key1
  }
}

// Store Application Insights connection string in Key Vault
resource kvSecretAppInsights 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'appinsights-connection-string'
  properties: {
    value: appInsights.properties.ConnectionString
  }
}

// Grant AKS Key Vault Secrets User role to access secrets
resource keyVaultSecretsUser 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(keyVault.id, aks.id, 'KeyVaultSecretsUser')
  scope: keyVault
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '4633458b-17de-408a-b874-0445c86b69e6') // Key Vault Secrets User
    principalId: aks.properties.addonProfiles.azureKeyvaultSecretsProvider.identity.objectId
    principalType: 'ServicePrincipal'
  }
}

// ============================================================================
// Outputs
// ============================================================================

@description('AKS cluster name')
output aksClusterName string = aks.name

@description('AKS resource ID')
output aksResourceId string = aks.id

@description('ACR name')
output acrName string = acr.name

@description('ACR login server')
output acrLoginServer string = acr.properties.loginServer

@description('Key Vault name')
output keyVaultName string = keyVault.name

@description('Key Vault URI')
output keyVaultUri string = keyVault.properties.vaultUri

@description('PostgreSQL server FQDN')
output postgresServerFqdn string = postgresServer.properties.fullyQualifiedDomainName

@description('PostgreSQL database name')
output postgresDatabaseName string = postgresDatabase.name

@description('Redis host name')
output redisHostName string = redis.properties.hostName

@description('Redis SSL port')
output redisSslPort int = redis.properties.sslPort

@description('Azure OpenAI endpoint')
output openAiEndpoint string = openAiAccount.properties.endpoint

@description('Azure OpenAI deployment name')
output openAiDeploymentName string = openAiDeployment.name

@description('Log Analytics workspace ID')
output logAnalyticsWorkspaceId string = logAnalytics.id

@description('Application Insights instrumentation key')
output appInsightsInstrumentationKey string = appInsights.properties.InstrumentationKey

@description('Application Insights connection string')
output appInsightsConnectionString string = appInsights.properties.ConnectionString
