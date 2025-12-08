using './main.bicep'

// Required parameters - these will be overridden by the deployment script
param location = 'uksouth'
param baseName = 'expensemgmt'
param deployGenAI = false
param adminObjectId = ''
param adminPrincipalName = ''
param adminPrincipalType = 'User'
