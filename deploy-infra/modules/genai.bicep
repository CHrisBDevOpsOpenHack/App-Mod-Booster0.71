@description('Azure region for GenAI resources - typically Sweden Central for better quota')
param location string = 'swedencentral'

@description('Base name for resources')
param baseName string

@description('Managed Identity Principal ID for role assignments')
param managedIdentityPrincipalId string

var openAIName = toLower('oai-${baseName}-${uniqueString(resourceGroup().id)}')
var searchServiceName = toLower('srch-${baseName}-${uniqueString(resourceGroup().id)}')
var modelName = 'gpt-4o'
var modelDeploymentName = 'gpt-4o'

resource openAI 'Microsoft.CognitiveServices/accounts@2023-05-01' = {
  name: openAIName
  location: location
  kind: 'OpenAI'
  sku: {
    name: 'S0'
  }
  properties: {
    customSubDomainName: openAIName
    publicNetworkAccess: 'Enabled'
  }
}

resource modelDeployment 'Microsoft.CognitiveServices/accounts/deployments@2023-05-01' = {
  parent: openAI
  name: modelDeploymentName
  sku: {
    name: 'Standard'
    capacity: 8
  }
  properties: {
    model: {
      format: 'OpenAI'
      name: modelName
      version: '2024-08-06'
    }
  }
}

resource searchService 'Microsoft.Search/searchServices@2023-11-01' = {
  name: searchServiceName
  location: location
  sku: {
    name: 'basic'
  }
  properties: {
    replicaCount: 1
    partitionCount: 1
    hostingMode: 'default'
  }
}

// Assign Cognitive Services OpenAI User role to the managed identity
resource openAIRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(openAI.id, managedIdentityPrincipalId, 'Cognitive Services OpenAI User')
  scope: openAI
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '5e0bd9bd-7b93-4f28-af87-19fc36ad61bd')
    principalId: managedIdentityPrincipalId
    principalType: 'ServicePrincipal'
  }
}

// Assign Search Index Data Contributor role to the managed identity
resource searchRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(searchService.id, managedIdentityPrincipalId, 'Search Index Data Contributor')
  scope: searchService
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '8ebe5a00-799e-43f5-93ac-243d3dce84a7')
    principalId: managedIdentityPrincipalId
    principalType: 'ServicePrincipal'
  }
}

@description('The OpenAI endpoint')
output openAIEndpoint string = openAI.properties.endpoint

@description('The OpenAI model deployment name')
output openAIModelName string = modelDeploymentName

@description('The OpenAI resource name')
output openAIName string = openAI.name

@description('The Search Service endpoint')
output searchServiceEndpoint string = 'https://${searchService.name}.search.windows.net'

@description('The Search Service name')
output searchServiceName string = searchService.name
