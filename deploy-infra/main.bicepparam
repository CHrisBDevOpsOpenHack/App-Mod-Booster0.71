using './main.bicep'

// These parameters will be provided by the deployment script
param location = 'uksouth'
param baseName = 'expensemgmt'
param adminObjectId = ''
param adminUsername = ''
param adminPrincipalType = 'User'
param deployGenAI = false
