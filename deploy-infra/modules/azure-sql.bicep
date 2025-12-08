@description('Azure region for SQL Server')
param location string

@description('Base name for resources')
param baseName string

@description('Admin Object ID for Entra ID administrator')
param adminObjectId string

@description('Admin user principal name or display name for SQL administrator')
param adminUserPrincipalName string

@description('Principal type for the SQL administrator')
@allowed([
  'User'
  'Application'
])
param adminPrincipalType string = 'User'

var uniqueSuffix = uniqueString(resourceGroup().id)
var sqlServerName = toLower('sql-${baseName}-${uniqueSuffix}')
var databaseName = 'Northwind'

resource sqlServer 'Microsoft.Sql/servers@2022-02-01-preview' = {
  name: sqlServerName
  location: location
  properties: {
    administrators: {
      administratorType: 'ActiveDirectory'
      login: adminUserPrincipalName
      sid: adminObjectId
      principalType: adminPrincipalType
      azureADOnlyAuthentication: true
    }
    publicNetworkAccess: 'Enabled'
  }
}

resource azureServiceFirewall 'Microsoft.Sql/servers/firewallRules@2022-02-01-preview' = {
  name: 'AllowAzureServices'
  parent: sqlServer
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

resource sqlDatabase 'Microsoft.Sql/servers/databases@2022-02-01-preview' = {
  name: databaseName
  parent: sqlServer
  sku: {
    name: 'Basic'
    tier: 'Basic'
  }
  properties: {
    maxSizeBytes: 2147483648
  }
}

output sqlServerName string = sqlServer.name
output sqlServerFqdn string = sqlServer.properties.fullyQualifiedDomainName
output databaseName string = sqlDatabase.name
