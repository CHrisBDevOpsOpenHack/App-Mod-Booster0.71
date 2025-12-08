@description('Azure region for resources')
param location string

@description('Base name for resources')
param baseName string = 'expensemgmt'

@description('Deploy GenAI resources (Azure OpenAI and AI Search)')
param deployGenAI bool = false

@description('Azure AD admin Object ID for SQL Server')
param adminObjectId string

@description('Azure AD admin User Principal Name or Service Principal Name')
param adminPrincipalName string

@description('Principal type for SQL admin - User for interactive, Application for CI/CD')
@allowed(['User', 'Application'])
param adminPrincipalType string = 'User'

@description('Timestamp for unique naming')
param timestamp string = utcNow('yyyyMMddHHmm')

// Deploy monitoring first (without App Service diagnostics)
module monitoring 'modules/monitoring.bicep' = {
  name: 'monitoring-deployment'
  params: {
    baseName: baseName
    location: location
  }
}

// Deploy managed identity early as it's needed by other resources
module managedIdentity 'modules/managed-identity.bicep' = {
  name: 'managed-identity-deployment'
  params: {
    baseName: baseName
    location: location
    timestamp: timestamp
  }
}

// Deploy App Service
module appService 'modules/app-service.bicep' = {
  name: 'app-service-deployment'
  params: {
    baseName: baseName
    location: location
    appInsightsConnectionString: monitoring.outputs.appInsightsConnectionString
    managedIdentityId: managedIdentity.outputs.managedIdentityId
    managedIdentityClientId: managedIdentity.outputs.managedIdentityClientId
    managedIdentityPrincipalId: managedIdentity.outputs.managedIdentityPrincipalId
  }
}

// Deploy App Service diagnostics after App Service is created
module appServiceDiagnostics 'modules/app-service-diagnostics.bicep' = {
  name: 'app-service-diagnostics-deployment'
  params: {
    appServiceName: appService.outputs.webAppName
    logAnalyticsWorkspaceId: monitoring.outputs.logAnalyticsWorkspaceId
  }
}

// Deploy Azure SQL
module azureSql 'modules/azure-sql.bicep' = {
  name: 'azure-sql-deployment'
  params: {
    baseName: baseName
    location: location
    adminObjectId: adminObjectId
    adminPrincipalName: adminPrincipalName
    adminPrincipalType: adminPrincipalType
    logAnalyticsWorkspaceId: monitoring.outputs.logAnalyticsWorkspaceId
  }
}

// Conditionally deploy GenAI resources
module genAI 'modules/genai.bicep' = if (deployGenAI) {
  name: 'genai-deployment'
  params: {
    baseName: baseName
    location: 'swedencentral'
    managedIdentityPrincipalId: managedIdentity.outputs.managedIdentityPrincipalId
  }
}

// Outputs
output webAppName string = appService.outputs.webAppName
output webAppDefaultHostName string = appService.outputs.webAppDefaultHostName
output sqlServerName string = azureSql.outputs.sqlServerName
output sqlServerFqdn string = azureSql.outputs.sqlServerFqdn
output databaseName string = azureSql.outputs.databaseName
output managedIdentityName string = managedIdentity.outputs.managedIdentityName
output managedIdentityClientId string = managedIdentity.outputs.managedIdentityClientId
output managedIdentityPrincipalId string = managedIdentity.outputs.managedIdentityPrincipalId
output appInsightsConnectionString string = monitoring.outputs.appInsightsConnectionString
output logAnalyticsWorkspaceId string = monitoring.outputs.logAnalyticsWorkspaceId

// Conditional GenAI outputs using null-safe operators
output openAIEndpoint string = genAI.?outputs.?openAIEndpoint ?? ''
output openAIModelName string = genAI.?outputs.?openAIModelName ?? ''
output openAIName string = genAI.?outputs.?openAIName ?? ''
output searchEndpoint string = genAI.?outputs.?searchEndpoint ?? ''
output searchName string = genAI.?outputs.?searchName ?? ''
