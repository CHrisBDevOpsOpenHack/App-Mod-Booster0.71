@description('The location where the App Service will be deployed')
param location string

@description('Base name for the App Service')
param baseName string

@description('Unique suffix for resource naming')
param uniqueSuffix string

@description('The resource ID of the managed identity to assign to the App Service')
param managedIdentityId string

@description('The Application Insights connection string')
param appInsightsConnectionString string

var appServicePlanName = 'asp-${baseName}-${uniqueSuffix}'
var webAppName = toLower('app-${baseName}-${uniqueSuffix}')

resource appServicePlan 'Microsoft.Web/serverfarms@2023-01-01' = {
  name: appServicePlanName
  location: location
  sku: {
    name: 'S1'
    tier: 'Standard'
    capacity: 1
  }
  kind: 'linux'
  properties: {
    reserved: true
  }
}

resource webApp 'Microsoft.Web/sites@2023-01-01' = {
  name: webAppName
  location: location
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${managedIdentityId}': {}
    }
  }
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|8.0'
      alwaysOn: true
      ftpsState: 'Disabled'
      minTlsVersion: '1.2'
      appSettings: [
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: appInsightsConnectionString
        }
        {
          name: 'ApplicationInsightsAgent_EXTENSION_VERSION'
          value: '~3'
        }
        {
          name: 'XDT_MicrosoftApplicationInsights_Mode'
          value: 'recommended'
        }
      ]
    }
  }
}

@description('The resource ID of the web app')
output webAppId string = webApp.id

@description('The name of the web app')
output webAppName string = webApp.name

@description('The default hostname of the web app')
output webAppHostName string = webApp.properties.defaultHostName

@description('The principal ID of the managed identity assigned to the App Service')
output managedIdentityPrincipalId string = webApp.identity.userAssignedIdentities[managedIdentityId].principalId
