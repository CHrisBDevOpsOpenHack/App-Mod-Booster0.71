@description('The location for monitoring resources')
param location string = resourceGroup().location

@description('Base name for resources')
param baseName string

@description('Unique suffix for resource naming')
param uniqueSuffix string

@description('SQL Server name')
param sqlServerName string

@description('SQL Database name')
param sqlDatabaseName string

@description('App Service name')
param appServiceName string

// Generate lowercase names for resources
var logAnalyticsWorkspaceName = toLower('law-${baseName}-${uniqueSuffix}')
var appInsightsName = toLower('appi-${baseName}-${uniqueSuffix}')

// Create Log Analytics Workspace
resource logAnalyticsWorkspace 'Microsoft.OperationalInsights/workspaces@2022-10-01' = {
  name: logAnalyticsWorkspaceName
  location: location
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 30
  }
}

// Create Application Insights
resource applicationInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: appInsightsName
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logAnalyticsWorkspace.id
    publicNetworkAccessForIngestion: 'Enabled'
    publicNetworkAccessForQuery: 'Enabled'
  }
}

// Reference existing SQL Database
resource sqlDatabase 'Microsoft.Sql/servers/databases@2023-05-01-preview' existing = {
  name: '${sqlServerName}/${sqlDatabaseName}'
}

// Configure diagnostic settings for SQL Database
resource databaseDiagnostics 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = {
  scope: sqlDatabase
  name: 'SQLDatabaseDiagnostics'
  properties: {
    workspaceId: logAnalyticsWorkspace.id
    logs: [
      {
        category: 'SQLInsights'
        enabled: true
      }
      {
        category: 'AutomaticTuning'
        enabled: true
      }
      {
        category: 'QueryStoreRuntimeStatistics'
        enabled: true
      }
      {
        category: 'QueryStoreWaitStatistics'
        enabled: true
      }
      {
        category: 'Errors'
        enabled: true
      }
      {
        category: 'DatabaseWaitStatistics'
        enabled: true
      }
      {
        category: 'Timeouts'
        enabled: true
      }
      {
        category: 'Blocks'
        enabled: true
      }
      {
        category: 'Deadlocks'
        enabled: true
      }
    ]
    metrics: [
      {
        category: 'Basic'
        enabled: true
      }
      {
        category: 'InstanceAndAppAdvanced'
        enabled: true
      }
    ]
  }
}

// Reference existing App Service
resource appService 'Microsoft.Web/sites@2023-01-01' existing = {
  name: appServiceName
}

// Configure diagnostic settings for App Service
resource appServiceDiagnostics 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = {
  scope: appService
  name: 'AppServiceDiagnostics'
  properties: {
    workspaceId: logAnalyticsWorkspace.id
    logs: [
      {
        category: 'AppServiceHTTPLogs'
        enabled: true
      }
      {
        category: 'AppServiceConsoleLogs'
        enabled: true
      }
      {
        category: 'AppServiceAppLogs'
        enabled: true
      }
      {
        category: 'AppServiceAuditLogs'
        enabled: true
      }
      {
        category: 'AppServicePlatformLogs'
        enabled: true
      }
    ]
    metrics: [
      {
        category: 'AllMetrics'
        enabled: true
      }
    ]
  }
}

// Output monitoring details
output logAnalyticsWorkspaceId string = logAnalyticsWorkspace.id
output appInsightsId string = applicationInsights.id
output appInsightsConnectionString string = applicationInsights.properties.ConnectionString
output appInsightsInstrumentationKey string = applicationInsights.properties.InstrumentationKey
