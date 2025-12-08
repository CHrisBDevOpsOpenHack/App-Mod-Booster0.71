@description('The location where the SQL Server will be deployed')
param location string

@description('Base name for the SQL Server')
param baseName string

@description('Unique suffix for resource naming')
param uniqueSuffix string

@description('The object ID of the Azure AD administrator')
param adminObjectId string

@description('The login name of the Azure AD administrator')
param adminLogin string

@description('The principal type of the Azure AD administrator (User or Application)')
@allowed(['User', 'Application'])
param adminPrincipalType string = 'User'

var sqlServerName = toLower('sql-${baseName}-${uniqueSuffix}')
var databaseName = 'Northwind'

resource sqlServer 'Microsoft.Sql/servers@2023-05-01-preview' = {
  name: sqlServerName
  location: location
  properties: {
    administratorLogin: 'sqladmin' // Not used with Azure AD-only auth
    administratorLoginPassword: newGuid() // Not used with Azure AD-only auth
    version: '12.0'
    minimalTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled'
    administrators: {
      administratorType: 'ActiveDirectory'
      principalType: adminPrincipalType
      login: adminLogin
      sid: adminObjectId
      tenantId: subscription().tenantId
      azureADOnlyAuthentication: true
    }
  }
}

resource sqlDatabase 'Microsoft.Sql/servers/databases@2023-05-01-preview' = {
  parent: sqlServer
  name: databaseName
  location: location
  sku: {
    name: 'Basic'
    tier: 'Basic'
  }
  properties: {
    collation: 'SQL_Latin1_General_CP1_CI_AS'
    maxSizeBytes: 2147483648 // 2GB
  }
}

resource sqlFirewallRuleAzure 'Microsoft.Sql/servers/firewallRules@2023-05-01-preview' = {
  parent: sqlServer
  name: 'AllowAzureServices'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

@description('The fully qualified domain name of the SQL Server')
output sqlServerFqdn string = sqlServer.properties.fullyQualifiedDomainName

@description('The name of the SQL Server')
output sqlServerName string = sqlServer.name

@description('The name of the database')
output databaseName string = databaseName

@description('The resource ID of the SQL Server')
output sqlServerId string = sqlServer.id

@description('The resource ID of the database')
output databaseId string = sqlDatabase.id
