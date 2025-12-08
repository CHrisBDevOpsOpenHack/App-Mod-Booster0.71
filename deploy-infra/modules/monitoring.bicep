@description('Base name for resources')
param baseName string

@description('Azure region for resources')
param location string

var uniqueSuffix = uniqueString(resourceGroup().id)
var logAnalyticsName = toLower('log-${baseName}-${uniqueSuffix}')
var appInsightsName = toLower('appi-${baseName}-${uniqueSuffix}')

resource logAnalyticsWorkspace 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: logAnalyticsName
  location: location
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 30
  }
}

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

output logAnalyticsWorkspaceId string = logAnalyticsWorkspace.id
output logAnalyticsWorkspaceName string = logAnalyticsWorkspace.name
output appInsightsConnectionString string = applicationInsights.properties.ConnectionString
output appInsightsInstrumentationKey string = applicationInsights.properties.InstrumentationKey
output appInsightsName string = applicationInsights.name
