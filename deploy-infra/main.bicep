@description('The location where resources will be deployed')
param location string = 'uksouth'

@description('Base name for all resources')
param baseName string = 'expensemgmt'

@description('Whether to deploy GenAI resources (Azure OpenAI and AI Search)')
param deployGenAI bool = false

@description('Timestamp for unique naming (must be parameter for utcNow)')
param timestamp string = utcNow('yyyyMMddHHmm')

@description('The object ID of the Azure AD administrator for SQL Server')
param adminObjectId string

@description('The login name of the Azure AD administrator for SQL Server')
param adminLogin string

@description('The principal type of the Azure AD administrator (User or Application)')
@allowed(['User', 'Application'])
param adminPrincipalType string = 'User'

var uniqueSuffix = uniqueString(resourceGroup().id)

// Deploy managed identity first
module managedIdentity 'modules/managed-identity.bicep' = {
  name: 'deploy-managed-identity'
  params: {
    location: location
    baseName: baseName
    timestamp: timestamp
  }
}

// Deploy monitoring resources
module monitoring 'modules/monitoring.bicep' = {
  name: 'deploy-monitoring'
  params: {
    location: location
    baseName: baseName
    uniqueSuffix: uniqueSuffix
  }
}

// Deploy App Service
module appService 'modules/app-service.bicep' = {
  name: 'deploy-app-service'
  params: {
    location: location
    baseName: baseName
    uniqueSuffix: uniqueSuffix
    managedIdentityId: managedIdentity.outputs.managedIdentityId
    appInsightsConnectionString: monitoring.outputs.appInsightsConnectionString
  }
}

// Deploy Azure SQL
module azureSQL 'modules/azure-sql.bicep' = {
  name: 'deploy-azure-sql'
  params: {
    location: location
    baseName: baseName
    uniqueSuffix: uniqueSuffix
    adminObjectId: adminObjectId
    adminLogin: adminLogin
    adminPrincipalType: adminPrincipalType
  }
}

// Deploy GenAI resources conditionally
module genAI 'modules/genai.bicep' = if (deployGenAI) {
  name: 'deploy-genai'
  params: {
    location: location
    baseName: baseName
    uniqueSuffix: uniqueSuffix
    managedIdentityPrincipalId: appService.outputs.managedIdentityPrincipalId
  }
}

// Deploy App Service diagnostics after App Service is created
module appServiceDiagnostics 'modules/app-service-diagnostics.bicep' = {
  name: 'deploy-app-service-diagnostics'
  params: {
    appServiceName: appService.outputs.webAppName
    logAnalyticsWorkspaceId: monitoring.outputs.logAnalyticsWorkspaceId
    databaseId: azureSQL.outputs.databaseId
  }
}

// Outputs
@description('The name of the web app')
output webAppName string = appService.outputs.webAppName

@description('The default hostname of the web app')
output webAppHostName string = appService.outputs.webAppHostName

@description('The fully qualified domain name of the SQL Server')
output sqlServerFqdn string = azureSQL.outputs.sqlServerFqdn

@description('The name of the SQL Server')
output sqlServerName string = azureSQL.outputs.sqlServerName

@description('The name of the database')
output databaseName string = azureSQL.outputs.databaseName

@description('The client ID of the managed identity')
output managedIdentityClientId string = managedIdentity.outputs.managedIdentityClientId

@description('The name of the managed identity')
output managedIdentityName string = managedIdentity.outputs.managedIdentityName

@description('The Application Insights connection string')
output appInsightsConnectionString string = monitoring.outputs.appInsightsConnectionString

@description('The endpoint URL for Azure OpenAI (empty if not deployed)')
output openAIEndpoint string = deployGenAI ? genAI.outputs.openAIEndpoint : ''

@description('The name of the deployed OpenAI model (empty if not deployed)')
output openAIModelName string = deployGenAI ? genAI.outputs.openAIModelName : ''

@description('The endpoint URL for Azure AI Search (empty if not deployed)')
output searchEndpoint string = deployGenAI ? genAI.outputs.searchEndpoint : ''
