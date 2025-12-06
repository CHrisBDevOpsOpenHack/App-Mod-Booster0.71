@description('The location for the managed identity')
param location string = resourceGroup().location

@description('Base name for resources')
param baseName string

@description('Timestamp for unique naming')
param timestamp string

// Create user-assigned managed identity with unique name
var managedIdentityName = 'mid-${baseName}-${timestamp}'

resource managedIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: managedIdentityName
  location: location
}

// Output the managed identity details
output managedIdentityId string = managedIdentity.id
output managedIdentityName string = managedIdentity.name
output managedIdentityClientId string = managedIdentity.properties.clientId
output managedIdentityPrincipalId string = managedIdentity.properties.principalId
