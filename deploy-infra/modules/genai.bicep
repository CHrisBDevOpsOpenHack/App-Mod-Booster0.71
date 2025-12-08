@description('Base name for resources')
param baseName string

@description('Region for Azure OpenAI resources (must support GPT-4o)')
param openaiLocation string = 'swedencentral'

@description('Region for Azure AI Search')
param location string

@description('Managed identity principal ID used for role assignments')
param managedIdentityPrincipalId string

var uniqueSuffix = uniqueString(resourceGroup().id)
var openAIName = toLower('oai-${baseName}-${uniqueSuffix}')
var searchName = toLower('search-${baseName}-${uniqueSuffix}')
var openAIDeploymentName = 'gpt-4o'

resource openAI 'Microsoft.CognitiveServices/accounts@2023-05-01' = {
  name: openAIName
  location: openaiLocation
  kind: 'OpenAI'
  sku: {
    name: 'S0'
  }
  properties: {
    customSubDomainName: openAIName
    publicNetworkAccess: 'Enabled'
  }
  tags: {
    managedIdentityPrincipalId: managedIdentityPrincipalId
  }
}

resource openAIDeployment 'Microsoft.CognitiveServices/accounts/deployments@2023-05-01' = {
  parent: openAI
  name: openAIDeploymentName
  sku: {
    name: 'Standard'
    capacity: 8
  }
  properties: {
    model: {
      format: 'OpenAI'
      name: 'gpt-4o'
      version: '2024-05-01'
    }
    raiPolicyName: 'Microsoft.Default'
  }
}

resource search 'Microsoft.Search/searchServices@2023-11-01' = {
  name: searchName
  location: location
  sku: {
    name: 'standard'
  }
  properties: {
    hostingMode: 'default'
    partitionCount: 1
    replicaCount: 1
    publicNetworkAccess: 'enabled'
  }
  tags: {
    managedIdentityPrincipalId: managedIdentityPrincipalId
  }
}

output openAIEndpoint string = openAI.properties.endpoint
output openAIModelName string = openAIDeployment.name
output openAIResourceName string = openAI.name
output searchEndpoint string = search.properties.hostName
output searchServiceName string = search.name
