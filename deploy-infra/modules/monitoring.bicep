@description('The location where monitoring resources will be deployed')
param location string

@description('Base name for monitoring resources')
param baseName string

@description('Unique suffix for resource naming')
param uniqueSuffix string

@description('The name of the App Service (optional, used for diagnostics)')
param appServiceName string = ''

var logAnalyticsName = 'log-${baseName}-${uniqueSuffix}'
var appInsightsName = 'appi-${baseName}-${uniqueSuffix}'

resource logAnalyticsWorkspace 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: logAnalyticsName
  location: location
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 30
    features: {
      enableLogAccessUsingOnlyResourcePermissions: true
    }
  }
}

resource applicationInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: appInsightsName
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logAnalyticsWorkspace.id
    IngestionMode: 'LogAnalytics'
    publicNetworkAccessForIngestion: 'Enabled'
    publicNetworkAccessForQuery: 'Enabled'
  }
}

@description('The resource ID of the Log Analytics workspace')
output logAnalyticsWorkspaceId string = logAnalyticsWorkspace.id

@description('The workspace ID (GUID) of the Log Analytics workspace')
output logAnalyticsWorkspaceGuid string = logAnalyticsWorkspace.properties.customerId

@description('The Application Insights instrumentation key')
output appInsightsInstrumentationKey string = applicationInsights.properties.InstrumentationKey

@description('The Application Insights connection string')
output appInsightsConnectionString string = applicationInsights.properties.ConnectionString

@description('The resource ID of Application Insights')
output appInsightsId string = applicationInsights.id

@description('The name of Application Insights')
output appInsightsName string = applicationInsights.name
