@description('The location for the SQL Server')
param location string = resourceGroup().location

@description('Base name for resources')
param baseName string

@description('Unique suffix for resource naming')
param uniqueSuffix string

@description('SQL Server Administrator Object ID')
param adminObjectId string

@description('SQL Server Administrator User Principal Name or Display Name')
param adminUsername string

@description('SQL Server Administrator Principal Type')
@allowed(['User', 'Application'])
param adminPrincipalType string = 'User'

@description('Managed Identity Principal ID for database access')
param managedIdentityPrincipalId string

@description('Managed Identity Name')
param managedIdentityName string

// Generate lowercase names for resources
var sqlServerName = toLower('sql-${baseName}-${uniqueSuffix}')
var databaseName = 'Northwind'

// Create SQL Server with Entra ID-only authentication
resource sqlServer 'Microsoft.Sql/servers@2023-05-01-preview' = {
  name: sqlServerName
  location: location
  properties: {
    administrators: {
      administratorType: 'ActiveDirectory'
      principalType: adminPrincipalType
      login: adminUsername
      sid: adminObjectId
      tenantId: subscription().tenantId
      azureADOnlyAuthentication: true
    }
    publicNetworkAccess: 'Enabled'
    minimalTlsVersion: '1.2'
  }
}

// Allow Azure services to access the SQL Server
resource firewallRuleAzureServices 'Microsoft.Sql/servers/firewallRules@2023-05-01-preview' = {
  parent: sqlServer
  name: 'AllowAllWindowsAzureIps'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

// Create the Northwind database with Basic tier
resource database 'Microsoft.Sql/servers/databases@2023-05-01-preview' = {
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

// Output SQL Server details
output sqlServerName string = sqlServer.name
output sqlServerFqdn string = sqlServer.properties.fullyQualifiedDomainName
output databaseName string = database.name
output databaseId string = database.id
