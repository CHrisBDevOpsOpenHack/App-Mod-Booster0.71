@description('The location where GenAI resources will be deployed')
param location string

@description('Base name for GenAI resources')
param baseName string

@description('Unique suffix for resource naming')
param uniqueSuffix string

@description('The principal ID of the managed identity to grant access')
param managedIdentityPrincipalId string

// Azure OpenAI should be in Sweden Central for better quota availability
var openAILocation = 'swedencentral'
var openAIName = toLower('oai-${baseName}-${uniqueSuffix}')
var searchName = toLower('srch-${baseName}-${uniqueSuffix}')
var modelDeploymentName = 'gpt-4o'
var modelName = 'gpt-4o'
var modelVersion = '2024-08-06'

resource openAI 'Microsoft.CognitiveServices/accounts@2024-04-01-preview' = {
  name: openAIName
  location: openAILocation
  kind: 'OpenAI'
  sku: {
    name: 'S0'
  }
  properties: {
    customSubDomainName: openAIName
    publicNetworkAccess: 'Enabled'
  }
}

resource modelDeployment 'Microsoft.CognitiveServices/accounts/deployments@2024-04-01-preview' = {
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
      version: modelVersion
    }
  }
}

resource aiSearch 'Microsoft.Search/searchServices@2023-11-01' = {
  name: searchName
  location: location
  sku: {
    name: 'basic'
  }
  properties: {
    replicaCount: 1
    partitionCount: 1
    hostingMode: 'default'
    publicNetworkAccess: 'enabled'
  }
}

// Assign Cognitive Services OpenAI User role to the managed identity
var cognitiveServicesOpenAIUserRoleId = '5e0bd9bd-7b93-4f28-af87-19fc36ad61bd'
resource openAIRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(openAI.id, managedIdentityPrincipalId, cognitiveServicesOpenAIUserRoleId)
  scope: openAI
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', cognitiveServicesOpenAIUserRoleId)
    principalId: managedIdentityPrincipalId
    principalType: 'ServicePrincipal'
  }
}

// Assign Search Index Data Contributor role to the managed identity
var searchIndexDataContributorRoleId = '8ebe5a00-799e-43f5-93ac-243d3dce84a7'
resource searchRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(aiSearch.id, managedIdentityPrincipalId, searchIndexDataContributorRoleId)
  scope: aiSearch
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', searchIndexDataContributorRoleId)
    principalId: managedIdentityPrincipalId
    principalType: 'ServicePrincipal'
  }
}

@description('The endpoint URL for Azure OpenAI')
output openAIEndpoint string = openAI.properties.endpoint

@description('The name of the deployed model')
output openAIModelName string = modelDeploymentName

@description('The name of the Azure OpenAI resource')
output openAIName string = openAI.name

@description('The endpoint URL for Azure AI Search')
output searchEndpoint string = 'https://${aiSearch.name}.search.windows.net'

@description('The name of the Azure AI Search resource')
output searchName string = aiSearch.name
