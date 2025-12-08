using './main.bicep'

param location = 'uksouth'
param baseName = 'expensemgmt'
param deployGenAI = false
// adminObjectId, adminLogin, and adminPrincipalType will be provided at deployment time
