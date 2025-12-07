@description('Azure region for the resources')
param location string

@description('Base name for resources')
param baseName string

@description('SQL Server administrator Object ID')
param adminObjectId string

@description('SQL Server administrator User Principal Name')
param adminUsername string

@description('Principal type for SQL Server admin - User for interactive, Application for CI/CD')
@allowed(['User', 'Application'])
param adminPrincipalType string = 'User'

var sqlServerName = 'sql-${baseName}-${uniqueString(resourceGroup().id)}'
var databaseName = 'Northwind'

resource sqlServer 'Microsoft.Sql/servers@2022-05-01-preview' = {
  name: sqlServerName
  location: location
  properties: {
    administrators: {
      administratorType: 'ActiveDirectory'
      login: adminUsername
      sid: adminObjectId
      tenantId: subscription().tenantId
      azureADOnlyAuthentication: true
      principalType: adminPrincipalType
    }
    publicNetworkAccess: 'Enabled'
  }
}

resource sqlServerFirewallRule 'Microsoft.Sql/servers/firewallRules@2022-05-01-preview' = {
  parent: sqlServer
  name: 'AllowAzureServices'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

resource database 'Microsoft.Sql/servers/databases@2022-05-01-preview' = {
  parent: sqlServer
  name: databaseName
  location: location
  sku: {
    name: 'Basic'
    tier: 'Basic'
  }
  properties: {
    collation: 'SQL_Latin1_General_CP1_CI_AS'
  }
}

@description('The name of the SQL server')
output sqlServerName string = sqlServer.name

@description('The fully qualified domain name of the SQL server')
output sqlServerFqdn string = sqlServer.properties.fullyQualifiedDomainName

@description('The name of the database')
output databaseName string = database.name
