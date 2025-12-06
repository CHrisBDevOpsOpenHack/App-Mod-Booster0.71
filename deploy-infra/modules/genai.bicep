@description('Base name for resources')
param baseName string

@description('Unique suffix for resource naming')
param uniqueSuffix string

@description('Managed Identity Principal ID for role assignments')
param managedIdentityPrincipalId string

// Generate lowercase names for resources (Azure OpenAI requires lowercase)
var openAIName = toLower('aoai-${baseName}-${uniqueSuffix}')
var searchName = toLower('srch-${baseName}-${uniqueSuffix}')
var modelDeploymentName = 'gpt-4o'
var modelName = 'gpt-4o'
var modelVersion = '2024-08-06'

// Deploy Azure OpenAI in Sweden Central for better quota availability
resource openAI 'Microsoft.CognitiveServices/accounts@2023-05-01' = {
  name: openAIName
  location: 'swedencentral'
  kind: 'OpenAI'
  sku: {
    name: 'S0'
  }
  properties: {
    customSubDomainName: openAIName
    publicNetworkAccess: 'Enabled'
  }
}

// Deploy the GPT-4o model with capacity 8
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
      version: modelVersion
    }
  }
}

// Create Azure AI Search with lowest cost development SKU
resource search 'Microsoft.Search/searchServices@2023-11-01' = {
  name: searchName
  location: 'uksouth'
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
var openAIUserRoleId = '5e0bd9bd-7b93-4f28-af87-19fc36ad61bd'
resource openAIRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: openAI
  name: guid(openAI.id, managedIdentityPrincipalId, openAIUserRoleId)
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', openAIUserRoleId)
    principalId: managedIdentityPrincipalId
    principalType: 'ServicePrincipal'
  }
}

// Assign Search Index Data Reader role to the managed identity
var searchIndexDataReaderRoleId = '1407120a-92aa-4202-b7e9-c0e197c71c8f'
resource searchRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: search
  name: guid(search.id, managedIdentityPrincipalId, searchIndexDataReaderRoleId)
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', searchIndexDataReaderRoleId)
    principalId: managedIdentityPrincipalId
    principalType: 'ServicePrincipal'
  }
}

// Output GenAI details
output openAIName string = openAI.name
output openAIEndpoint string = openAI.properties.endpoint
output openAIModelName string = modelDeploymentName
output searchName string = search.name
output searchEndpoint string = 'https://${search.name}.search.windows.net'
