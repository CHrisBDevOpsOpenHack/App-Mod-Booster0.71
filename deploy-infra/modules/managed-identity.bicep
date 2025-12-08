@description('Azure region for the managed identity')
param location string

@description('Base name used for resource naming')
param baseName string

@description('Timestamp suffix to ensure unique resource names (format: yyyyMMddHHmm)')
param timestamp string

var identityName = toLower('mid-${baseName}-${timestamp}')

resource userAssignedIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: identityName
  location: location
}

output name string = userAssignedIdentity.name
output clientId string = userAssignedIdentity.properties.clientId
output principalId string = userAssignedIdentity.properties.principalId
output resourceId string = userAssignedIdentity.id
