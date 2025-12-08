@description('Azure region for all resources')
param location string

@description('Base name for resource naming')
param baseName string = 'expensemgmt'

@description('Deploy GenAI resources (Azure OpenAI and AI Search)')
param deployGenAI bool = false

@description('Object ID for the SQL administrator')
param adminObjectId string

@description('User principal name or display name for SQL administrator')
param adminUserPrincipalName string

@description('Principal type for the SQL administrator')
@allowed([
  'User'
  'Application'
])
param adminPrincipalType string = 'User'

@description('Timestamp suffix for resource uniqueness')
param timestamp string = utcNow('yyyyMMddHHmm')

module managedIdentity './modules/managed-identity.bicep' = {
  name: 'managedIdentity'
  params: {
    location: location
    baseName: baseName
    timestamp: timestamp
  }
}

module sql './modules/azure-sql.bicep' = {
  name: 'azureSql'
  params: {
    location: location
    baseName: baseName
    adminObjectId: adminObjectId
    adminUserPrincipalName: adminUserPrincipalName
    adminPrincipalType: adminPrincipalType
  }
}

module monitoring './modules/monitoring.bicep' = {
  name: 'monitoring'
  params: {
    location: location
    baseName: baseName
    sqlDatabaseId: sql.outputs.sqlServerName != '' ? resourceId('Microsoft.Sql/servers/databases', sql.outputs.sqlServerName, sql.outputs.databaseName) : ''
  }
}

module appService './modules/app-service.bicep' = {
  name: 'appService'
  params: {
    location: location
    baseName: baseName
    managedIdentityResourceId: managedIdentity.outputs.resourceId
    appInsightsConnectionString: monitoring.outputs.appInsightsConnectionString
  }
}

module appServiceDiagnostics './modules/app-service-diagnostics.bicep' = {
  name: 'appServiceDiagnostics'
  params: {
    appServiceName: appService.outputs.webAppName
    logAnalyticsWorkspaceId: monitoring.outputs.logAnalyticsWorkspaceId
  }
}

module genAI './modules/genai.bicep' = if (deployGenAI) {
  name: 'genai'
  params: {
    baseName: baseName
    openaiLocation: 'swedencentral'
    location: location
    managedIdentityPrincipalId: appService.outputs.managedIdentityPrincipalId
  }
}

output webAppName string = appService.outputs.webAppName
output appServicePlanName string = appService.outputs.appServicePlanName
output managedIdentityClientId string = appService.outputs.managedIdentityClientId
output managedIdentityPrincipalId string = appService.outputs.managedIdentityPrincipalId
output sqlServerName string = sql.outputs.sqlServerName
output sqlServerFqdn string = sql.outputs.sqlServerFqdn
output databaseName string = sql.outputs.databaseName
output appInsightsConnectionString string = monitoring.outputs.appInsightsConnectionString
output logAnalyticsWorkspaceId string = monitoring.outputs.logAnalyticsWorkspaceId
output openAIEndpoint string = genAI.?outputs.?openAIEndpoint ?? ''
output openAIModelName string = genAI.?outputs.?openAIModelName ?? ''
output searchEndpoint string = genAI.?outputs.?searchEndpoint ?? ''
