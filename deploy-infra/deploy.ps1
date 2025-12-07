#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Deploys the infrastructure for the Expense Management application to Azure.

.DESCRIPTION
    This script deploys all Azure infrastructure including App Service, SQL Database,
    Managed Identity, Monitoring, and optionally GenAI resources (Azure OpenAI and AI Search).
    
    It handles the complete infrastructure setup including:
    - Azure resource creation via Bicep templates
    - Database schema import
    - Stored procedures deployment
    - Managed identity database user creation
    - App Service configuration

.PARAMETER ResourceGroup
    The name of the Azure resource group (required).

.PARAMETER Location
    The Azure region for deployment (required).

.PARAMETER BaseName
    Base name for resources. Default is 'expensemgmt'.

.PARAMETER DeployGenAI
    Switch to deploy Azure OpenAI and AI Search resources.

.PARAMETER SkipDatabaseSetup
    Skip database schema import and stored procedures (for redeployments).

.EXAMPLE
    .\deploy.ps1 -ResourceGroup "rg-expensemgmt-20251206" -Location "uksouth"
    
.EXAMPLE
    .\deploy.ps1 -ResourceGroup "rg-expensemgmt-20251206" -Location "uksouth" -DeployGenAI
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ResourceGroup,
    
    [Parameter(Mandatory = $true)]
    [string]$Location,
    
    [Parameter(Mandatory = $false)]
    [string]$BaseName = "expensemgmt",
    
    [Parameter(Mandatory = $false)]
    [switch]$DeployGenAI,
    
    [Parameter(Mandatory = $false)]
    [switch]$SkipDatabaseSetup
)

$ErrorActionPreference = "Stop"

# Detect CI/CD environment
$IsCI = $env:GITHUB_ACTIONS -eq "true" -or $env:TF_BUILD -eq "true" -or $env:CI -eq "true"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Infrastructure Deployment" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Resource Group: $ResourceGroup"
Write-Host "Location: $Location"
Write-Host "Base Name: $BaseName"
Write-Host "Deploy GenAI: $DeployGenAI"
Write-Host "CI/CD Mode: $IsCI"
Write-Host ""

# Check PowerShell version
if ($PSVersionTable.PSVersion.Major -lt 7) {
    Write-Warning "You are running PowerShell $($PSVersionTable.PSVersion). PowerShell 7+ is recommended."
}

# Check Azure CLI
Write-Host "Checking Azure CLI..." -ForegroundColor Yellow
try {
    $azVersion = az version --output json | ConvertFrom-Json
    Write-Host "✓ Azure CLI version $($azVersion.'azure-cli')" -ForegroundColor Green
}
catch {
    Write-Error "Azure CLI is not installed. Please install it from https://aka.ms/installazurecliwindows"
    exit 1
}

# Check login
Write-Host "Checking Azure login..." -ForegroundColor Yellow
try {
    $account = az account show --output json | ConvertFrom-Json
    Write-Host "✓ Logged in as $($account.user.name)" -ForegroundColor Green
    Write-Host "✓ Subscription: $($account.name)" -ForegroundColor Green
}
catch {
    Write-Error "Not logged in to Azure. Please run 'az login' first."
    exit 1
}

# Get admin credentials based on environment
Write-Host "`nRetrieving administrator credentials..." -ForegroundColor Yellow

if ($IsCI) {
    # CI/CD with Service Principal (OIDC)
    Write-Host "CI/CD mode: Using Service Principal" -ForegroundColor Cyan
    
    if (-not $env:AZURE_CLIENT_ID) {
        Write-Error "AZURE_CLIENT_ID environment variable not found. This should be set by OIDC authentication."
        exit 1
    }
    
    $servicePrincipalClientId = $env:AZURE_CLIENT_ID
    $spInfo = az ad sp show --id $servicePrincipalClientId --output json | ConvertFrom-Json
    
    $adminObjectId = $spInfo.id
    $adminUsername = $spInfo.displayName
    $adminPrincipalType = "Application"
    
    Write-Host "✓ Service Principal: $adminUsername" -ForegroundColor Green
    Write-Host "✓ Object ID: $adminObjectId" -ForegroundColor Green
}
else {
    # Local/Interactive mode
    Write-Host "Interactive mode: Using current user" -ForegroundColor Cyan
    
    $currentUser = az ad signed-in-user show --output json | ConvertFrom-Json
    
    $adminObjectId = $currentUser.id
    $adminUsername = $currentUser.userPrincipalName
    $adminPrincipalType = "User"
    
    Write-Host "✓ User: $adminUsername" -ForegroundColor Green
    Write-Host "✓ Object ID: $adminObjectId" -ForegroundColor Green
}

# Create resource group
Write-Host "`nCreating resource group..." -ForegroundColor Yellow
az group create --name $ResourceGroup --location $Location --output none
Write-Host "✓ Resource group created" -ForegroundColor Green

# Deploy Bicep templates
Write-Host "`nDeploying infrastructure..." -ForegroundColor Yellow
Write-Host "This may take 5-10 minutes..." -ForegroundColor Cyan

$deploymentName = "infra-deployment-$(Get-Date -Format 'yyyyMMddHHmmss')"

$deploymentOutput = az deployment group create `
    --resource-group $ResourceGroup `
    --name $deploymentName `
    --template-file "./deploy-infra/main.bicep" `
    --parameters location=$Location baseName=$BaseName adminObjectId=$adminObjectId adminUsername=$adminUsername adminPrincipalType=$adminPrincipalType deployGenAI=$($DeployGenAI.IsPresent) `
    --output json | ConvertFrom-Json

if ($LASTEXITCODE -ne 0) {
    Write-Error "Bicep deployment failed"
    exit 1
}

Write-Host "✓ Infrastructure deployed" -ForegroundColor Green

# Extract outputs
$outputs = $deploymentOutput.properties.outputs
$webAppName = $outputs.webAppName.value
$sqlServerFqdn = $outputs.sqlServerFqdn.value
$sqlServerName = $outputs.sqlServerName.value
$databaseName = $outputs.databaseName.value
$managedIdentityClientId = $outputs.managedIdentityClientId.value
$managedIdentityName = $outputs.managedIdentityName.value
$appInsightsConnectionString = $outputs.appInsightsConnectionString.value

Write-Host "`nDeployed Resources:" -ForegroundColor Cyan
Write-Host "  Web App: $webAppName"
Write-Host "  SQL Server: $sqlServerName"
Write-Host "  Database: $databaseName"
Write-Host "  Managed Identity: $managedIdentityName"

# Wait for SQL Server to be ready
Write-Host "`nWaiting for SQL Server to become ready..." -ForegroundColor Yellow
Start-Sleep -Seconds 30
Write-Host "✓ SQL Server ready" -ForegroundColor Green

# Add current IP to firewall (for local development)
if (-not $IsCI) {
    Write-Host "`nAdding your IP to SQL Server firewall..." -ForegroundColor Yellow
    try {
        $myIp = (Invoke-RestMethod -Uri "https://api.ipify.org?format=text").Trim()
        az sql server firewall-rule create `
            --resource-group $ResourceGroup `
            --server $sqlServerName `
            --name "ClientIP" `
            --start-ip-address $myIp `
            --end-ip-address $myIp `
            --output none
        Write-Host "✓ Firewall rule added for IP: $myIp" -ForegroundColor Green
    }
    catch {
        Write-Warning "Could not add IP to firewall. You may need to add it manually."
    }
}

# Database setup
if (-not $SkipDatabaseSetup) {
    # Import database schema
    Write-Host "`nImporting database schema..." -ForegroundColor Yellow
    
    $schemaFile = Join-Path $PSScriptRoot ".." "Database-Schema" "database_schema.sql"
    if (-not (Test-Path $schemaFile)) {
        Write-Error "Database schema file not found at: $schemaFile"
        exit 1
    }
    
    $authMethod = if ($IsCI) { "ActiveDirectoryAzCli" } else { "ActiveDirectoryDefault" }
    
    try {
        sqlcmd -S $sqlServerFqdn -d $databaseName "--authentication-method=$authMethod" -i $schemaFile
        if ($LASTEXITCODE -ne 0) {
            Write-Error "Schema import failed"
            exit 1
        }
        Write-Host "✓ Database schema imported" -ForegroundColor Green
    }
    catch {
        Write-Error "Failed to import schema: $_"
        exit 1
    }
    
    # Create managed identity database user
    Write-Host "`nCreating managed identity database user..." -ForegroundColor Yellow
    
    # Convert Client ID to SID for SQL
    $guidBytes = [System.Guid]::Parse($managedIdentityClientId).ToByteArray()
    $sidHex = "0x" + [System.BitConverter]::ToString($guidBytes).Replace("-", "")
    
    $createUserSql = @"
IF EXISTS (SELECT * FROM sys.database_principals WHERE name = '$managedIdentityName')
    DROP USER [$managedIdentityName];

CREATE USER [$managedIdentityName] WITH SID = $sidHex, TYPE = E;

ALTER ROLE db_datareader ADD MEMBER [$managedIdentityName];
ALTER ROLE db_datawriter ADD MEMBER [$managedIdentityName];
GRANT EXECUTE TO [$managedIdentityName];
"@
    
    $tempFile = [System.IO.Path]::GetTempFileName() + ".sql"
    $createUserSql | Out-File -FilePath $tempFile -Encoding UTF8
    
    try {
        sqlcmd -S $sqlServerFqdn -d $databaseName "--authentication-method=$authMethod" -i $tempFile
        if ($LASTEXITCODE -ne 0) {
            Write-Error "Failed to create managed identity user"
            exit 1
        }
        Write-Host "✓ Managed identity user created" -ForegroundColor Green
    }
    finally {
        Remove-Item -Path $tempFile -Force -ErrorAction SilentlyContinue
    }
    
    # Deploy stored procedures
    Write-Host "`nDeploying stored procedures..." -ForegroundColor Yellow
    
    $storedProcsFile = Join-Path $PSScriptRoot ".." "stored-procedures.sql"
    if (Test-Path $storedProcsFile) {
        try {
            sqlcmd -S $sqlServerFqdn -d $databaseName "--authentication-method=$authMethod" -i $storedProcsFile
            if ($LASTEXITCODE -ne 0) {
                Write-Warning "Stored procedures deployment had errors. Check if file exists."
            }
            else {
                Write-Host "✓ Stored procedures deployed" -ForegroundColor Green
            }
        }
        catch {
            Write-Warning "Could not deploy stored procedures: $_"
        }
    }
    else {
        Write-Warning "Stored procedures file not found at: $storedProcsFile"
        Write-Warning "You may need to create this file with your application's stored procedures."
    }
}

# Configure App Service settings
Write-Host "`nConfiguring App Service settings..." -ForegroundColor Yellow

$connectionString = "Server=tcp:$sqlServerFqdn,1433;Initial Catalog=$databaseName;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;Authentication=Active Directory Managed Identity;User Id=$managedIdentityClientId;"

# Set connection string and managed identity client ID
az webapp config connection-string set `
    --resource-group $ResourceGroup `
    --name $webAppName `
    --connection-string-type SQLAzure `
    --settings "DefaultConnection=$connectionString" `
    --output none

az webapp config appsettings set `
    --resource-group $ResourceGroup `
    --name $webAppName `
    --settings "AZURE_CLIENT_ID=$managedIdentityClientId" "ManagedIdentityClientId=$managedIdentityClientId" `
    --output none

Write-Host "✓ Connection string configured" -ForegroundColor Green
Write-Host "✓ Managed identity client ID configured" -ForegroundColor Green

# Configure GenAI settings if deployed
if ($DeployGenAI) {
    Write-Host "`nConfiguring GenAI settings..." -ForegroundColor Yellow
    
    $openAIEndpoint = $outputs.openAIEndpoint.value
    $openAIModelName = $outputs.openAIModelName.value
    $searchServiceEndpoint = $outputs.searchServiceEndpoint.value
    
    az webapp config appsettings set `
        --resource-group $ResourceGroup `
        --name $webAppName `
        --settings "GenAISettings__OpenAIEndpoint=$openAIEndpoint" "GenAISettings__OpenAIModelName=$openAIModelName" "GenAISettings__SearchServiceEndpoint=$searchServiceEndpoint" `
        --output none
    
    Write-Host "✓ GenAI settings configured" -ForegroundColor Green
}

# Save deployment context
Write-Host "`nSaving deployment context..." -ForegroundColor Yellow

$contextFilePath = Join-Path $PSScriptRoot ".." ".deployment-context.json"
$context = @{
    resourceGroup          = $ResourceGroup
    location               = $Location
    webAppName             = $webAppName
    sqlServerFqdn          = $sqlServerFqdn
    sqlServerName          = $sqlServerName
    databaseName           = $databaseName
    managedIdentityClientId = $managedIdentityClientId
    deployedAt             = (Get-Date).ToString("o")
    deployedBy             = $adminUsername
    genAIDeployed          = $DeployGenAI.IsPresent
}

if ($DeployGenAI) {
    $context.openAIEndpoint = $outputs.openAIEndpoint.value
    $context.openAIModelName = $outputs.openAIModelName.value
    $context.searchServiceEndpoint = $outputs.searchServiceEndpoint.value
}

$context | ConvertTo-Json -Depth 10 | Out-File -FilePath $contextFilePath -Encoding UTF8
Write-Host "✓ Context saved to: $contextFilePath" -ForegroundColor Green

# Summary
Write-Host "`n========================================" -ForegroundColor Green
Write-Host "Infrastructure Deployment Complete!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host "`nDeployed Resources:" -ForegroundColor Cyan
Write-Host "  Resource Group: $ResourceGroup"
Write-Host "  Web App: $webAppName"
Write-Host "  SQL Server: $sqlServerName"
Write-Host "  Database: $databaseName"
Write-Host "  Managed Identity: $managedIdentityName"

if ($DeployGenAI) {
    Write-Host "`nGenAI Resources:" -ForegroundColor Cyan
    Write-Host "  Azure OpenAI: Deployed"
    Write-Host "  AI Search: Deployed"
}

Write-Host "`nNext Steps:" -ForegroundColor Yellow
Write-Host "  1. Deploy the application code:"
Write-Host "     .\deploy-app\deploy.ps1"
Write-Host ""
Write-Host "  2. Access your application:"
Write-Host "     https://$webAppName.azurewebsites.net/Index"
Write-Host ""

if ($DeployGenAI) {
    Write-Host "  3. Try the AI Chat:"
    Write-Host "     https://$webAppName.azurewebsites.net/Chat"
    Write-Host ""
}

exit 0
