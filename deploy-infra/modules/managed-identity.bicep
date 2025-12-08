@description('Base name for resources')
param baseName string

@description('Azure region for resources')
param location string

@description('Timestamp for unique naming')
param timestamp string = utcNow('yyyyMMddHHmm')

var managedIdentityName = toLower('mid-${baseName}-${timestamp}')

resource managedIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: managedIdentityName
  location: location
}

output managedIdentityId string = managedIdentity.id
output managedIdentityName string = managedIdentity.name
output managedIdentityClientId string = managedIdentity.properties.clientId
output managedIdentityPrincipalId string = managedIdentity.properties.principalId
