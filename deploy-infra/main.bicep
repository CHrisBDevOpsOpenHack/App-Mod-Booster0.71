@description('The Azure region for all resources')
param location string = 'uksouth'

@description('Base name for resources')
param baseName string = 'expensemgmt'

@description('SQL Server Administrator Object ID')
param adminObjectId string

@description('SQL Server Administrator Username')
param adminUsername string

@description('SQL Server Administrator Principal Type')
@allowed(['User', 'Application'])
param adminPrincipalType string = 'User'

@description('Deploy GenAI resources (Azure OpenAI and AI Search)')
param deployGenAI bool = false

// Generate unique suffix for resource names
var uniqueSuffix = uniqueString(resourceGroup().id)
var timestamp = utcNow('yyyyMMddHHmm')

// Deploy managed identity first (needed by other resources)
module managedIdentity 'modules/managed-identity.bicep' = {
  name: 'managedIdentity-deployment'
  params: {
    location: location
    baseName: baseName
    timestamp: timestamp
  }
}

// Deploy monitoring resources
module monitoring 'modules/monitoring.bicep' = {
  name: 'monitoring-deployment'
  params: {
    location: location
    baseName: baseName
    uniqueSuffix: uniqueSuffix
    sqlServerName: azureSQL.outputs.sqlServerName
    sqlDatabaseName: azureSQL.outputs.databaseName
    appServiceName: appService.outputs.webAppName
  }
  dependsOn: [
    azureSQL
    appService
  ]
}

// Deploy App Service with managed identity
module appService 'modules/app-service.bicep' = {
  name: 'appService-deployment'
  params: {
    location: location
    baseName: baseName
    uniqueSuffix: uniqueSuffix
    managedIdentityId: managedIdentity.outputs.managedIdentityId
    managedIdentityClientId: managedIdentity.outputs.managedIdentityClientId
    appInsightsConnectionString: monitoring.outputs.appInsightsConnectionString
  }
}

// Deploy Azure SQL with Entra ID-only authentication
module azureSQL 'modules/azure-sql.bicep' = {
  name: 'azureSQL-deployment'
  params: {
    location: location
    baseName: baseName
    uniqueSuffix: uniqueSuffix
    adminObjectId: adminObjectId
    adminUsername: adminUsername
    adminPrincipalType: adminPrincipalType
    managedIdentityPrincipalId: managedIdentity.outputs.managedIdentityPrincipalId
    managedIdentityName: managedIdentity.outputs.managedIdentityName
  }
}

// Conditionally deploy GenAI resources
module genAI 'modules/genai.bicep' = if (deployGenAI) {
  name: 'genAI-deployment'
  params: {
    baseName: baseName
    uniqueSuffix: uniqueSuffix
    managedIdentityPrincipalId: managedIdentity.outputs.managedIdentityPrincipalId
  }
}

// Output deployment details
output resourceGroupName string = resourceGroup().name
output location string = location
output webAppName string = appService.outputs.webAppName
output webAppHostName string = appService.outputs.webAppHostName
output sqlServerName string = azureSQL.outputs.sqlServerName
output sqlServerFqdn string = azureSQL.outputs.sqlServerFqdn
output databaseName string = azureSQL.outputs.databaseName
output managedIdentityName string = managedIdentity.outputs.managedIdentityName
output managedIdentityClientId string = managedIdentity.outputs.managedIdentityClientId
output managedIdentityPrincipalId string = managedIdentity.outputs.managedIdentityPrincipalId
output appInsightsConnectionString string = monitoring.outputs.appInsightsConnectionString

// Conditional GenAI outputs (use null-safe operators)
output openAIEndpoint string = deployGenAI ? genAI.outputs.openAIEndpoint : ''
output openAIModelName string = deployGenAI ? genAI.outputs.openAIModelName : ''
output openAIName string = deployGenAI ? genAI.outputs.openAIName : ''
output searchEndpoint string = deployGenAI ? genAI.outputs.searchEndpoint : ''
output searchName string = deployGenAI ? genAI.outputs.searchName : ''
