@description('Azure region for the App Service')
param location string

@description('Base name for resources')
param baseName string

@description('User-assigned managed identity resource ID')
param managedIdentityResourceId string

@description('Application Insights connection string')
param appInsightsConnectionString string = ''

var uniqueSuffix = uniqueString(resourceGroup().id)
var appServicePlanName = toLower('asp-${baseName}-${uniqueSuffix}')
var webAppName = toLower('web-${baseName}-${uniqueSuffix}')

resource appServicePlan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: appServicePlanName
  location: location
  sku: {
    name: 'S1'
    tier: 'Standard'
  }
  properties: {
    reserved: true
  }
}

resource webApp 'Microsoft.Web/sites@2023-12-01' = {
  name: webAppName
  location: location
  kind: 'app'
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${managedIdentityResourceId}': {}
    }
  }
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    clientAffinityEnabled: false
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|8.0'
      alwaysOn: true
      minimumTlsVersion: '1.2'
    }
  }
}

resource appSettings 'Microsoft.Web/sites/config@2023-12-01' = {
  name: '${webApp.name}/appsettings'
  properties: union({
    'ASPNETCORE_ENVIRONMENT': 'Production'
  }, empty(appInsightsConnectionString) ? {} : {
    'APPLICATIONINSIGHTS_CONNECTION_STRING': appInsightsConnectionString
  })
}

output webAppName string = webApp.name
output webAppId string = webApp.id
output appServicePlanName string = appServicePlan.name
output managedIdentityClientId string = reference(managedIdentityResourceId, '2023-01-31', 'Full').properties.clientId
output managedIdentityPrincipalId string = reference(managedIdentityResourceId, '2023-01-31', 'Full').properties.principalId
