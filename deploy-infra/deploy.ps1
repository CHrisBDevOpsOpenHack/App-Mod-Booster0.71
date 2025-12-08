#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Deploys the Azure infrastructure for the Expense Management application.

.DESCRIPTION
    This script automates the deployment of all Azure resources including:
    - Managed Identity
    - App Service (S1 tier)
    - Azure SQL Database with Entra ID authentication
    - Log Analytics and Application Insights
    - Optional: Azure OpenAI and AI Search (with -DeployGenAI switch)

.PARAMETER ResourceGroup
    The name of the Azure resource group (required)

.PARAMETER Location
    The Azure region for deployment (required)

.PARAMETER BaseName
    Base name for resources (optional, defaults to 'expensemgmt')

.PARAMETER DeployGenAI
    Switch to deploy GenAI resources (Azure OpenAI and AI Search)

.PARAMETER SkipDatabaseSetup
    Skip database schema and stored procedures setup (for redeployments)

.EXAMPLE
    .\deploy.ps1 -ResourceGroup "rg-expensemgmt-20251208" -Location "uksouth"

.EXAMPLE
    .\deploy.ps1 -ResourceGroup "rg-expensemgmt-20251208" -Location "uksouth" -DeployGenAI
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

# Check PowerShell version
if ($PSVersionTable.PSVersion.Major -lt 7) {
    Write-Warning "You are running PowerShell $($PSVersionTable.PSVersion). PowerShell 7+ is recommended for better performance and compatibility."
}

Write-Host "======================================" -ForegroundColor Cyan
Write-Host "Expense Management - Infrastructure Deployment" -ForegroundColor Cyan
Write-Host "======================================" -ForegroundColor Cyan
Write-Host ""

# Detect if running in CI/CD
$IsCI = $env:GITHUB_ACTIONS -eq "true" -or $env:TF_BUILD -eq "true" -or $env:CI -eq "true"

if ($IsCI) {
    Write-Host "Running in CI/CD mode" -ForegroundColor Yellow
} else {
    Write-Host "Running in interactive mode" -ForegroundColor Green
}

# Check Azure CLI
Write-Host "Checking Azure CLI..." -ForegroundColor Yellow
try {
    $azVersion = az version --output json 2>$null | ConvertFrom-Json
    Write-Host "✓ Azure CLI version: $($azVersion.'azure-cli')" -ForegroundColor Green
} catch {
    Write-Error "Azure CLI is not installed. Please install it from https://docs.microsoft.com/cli/azure/install-azure-cli"
    exit 1
}

# Check login
Write-Host "Checking Azure login..." -ForegroundColor Yellow
try {
    $account = az account show --output json 2>$null | ConvertFrom-Json
    Write-Host "✓ Logged in to Azure as: $($account.user.name)" -ForegroundColor Green
    Write-Host "✓ Subscription: $($account.name)" -ForegroundColor Green
} catch {
    Write-Error "Not logged in to Azure. Please run 'az login' first."
    exit 1
}

# Get admin credentials
Write-Host "Retrieving administrator credentials..." -ForegroundColor Yellow

if ($IsCI) {
    # CI/CD mode: Use Service Principal
    $servicePrincipalClientId = $env:AZURE_CLIENT_ID
    if ([string]::IsNullOrEmpty($servicePrincipalClientId)) {
        Write-Error "AZURE_CLIENT_ID environment variable is not set"
        exit 1
    }
    
    Write-Host "Retrieving Service Principal details..." -ForegroundColor Yellow
    $spDetails = az ad sp show --id $servicePrincipalClientId --output json 2>$null | ConvertFrom-Json
    
    $adminObjectId = $spDetails.id
    $adminLogin = $spDetails.displayName
    $adminPrincipalType = "Application"
    
    Write-Host "✓ Service Principal: $adminLogin" -ForegroundColor Green
    Write-Host "✓ Object ID: $adminObjectId" -ForegroundColor Green
} else {
    # Interactive mode: Use signed-in user
    $currentUser = az ad signed-in-user show --output json 2>$null | ConvertFrom-Json
    
    $adminObjectId = $currentUser.id
    $adminLogin = $currentUser.userPrincipalName
    $adminPrincipalType = "User"
    
    Write-Host "✓ User: $adminLogin" -ForegroundColor Green
    Write-Host "✓ Object ID: $adminObjectId" -ForegroundColor Green
}

# Create resource group
Write-Host ""
Write-Host "Creating resource group '$ResourceGroup' in '$Location'..." -ForegroundColor Yellow
az group create --name $ResourceGroup --location $Location --output none
Write-Host "✓ Resource group created" -ForegroundColor Green

# Deploy Bicep template
Write-Host ""
Write-Host "Deploying infrastructure with Bicep..." -ForegroundColor Yellow
Write-Host "  - Base name: $BaseName" -ForegroundColor Cyan
Write-Host "  - Deploy GenAI: $DeployGenAI" -ForegroundColor Cyan

$deployGenAIValue = $DeployGenAI.ToString().ToLower()

$deployment = az deployment group create `
    --resource-group $ResourceGroup `
    --template-file "$PSScriptRoot/main.bicep" `
    --parameters location=$Location baseName=$BaseName deployGenAI=$deployGenAIValue adminObjectId=$adminObjectId adminLogin=$adminLogin adminPrincipalType=$adminPrincipalType `
    --output json 2>$null | ConvertFrom-Json

if ($LASTEXITCODE -ne 0) {
    Write-Error "Bicep deployment failed"
    exit 1
}

Write-Host "✓ Infrastructure deployed successfully" -ForegroundColor Green

# Extract outputs
$outputs = $deployment.properties.outputs
$webAppName = $outputs.webAppName.value
$sqlServerFqdn = $outputs.sqlServerFqdn.value
$sqlServerName = $outputs.sqlServerName.value
$databaseName = $outputs.databaseName.value
$managedIdentityClientId = $outputs.managedIdentityClientId.value
$managedIdentityName = $outputs.managedIdentityName.value
$appInsightsConnectionString = $outputs.appInsightsConnectionString.value

Write-Host ""
Write-Host "Deployed Resources:" -ForegroundColor Cyan
Write-Host "  - Web App: $webAppName" -ForegroundColor White
Write-Host "  - SQL Server: $sqlServerFqdn" -ForegroundColor White
Write-Host "  - Database: $databaseName" -ForegroundColor White
Write-Host "  - Managed Identity: $managedIdentityName" -ForegroundColor White

# Configure database
if (-not $SkipDatabaseSetup) {
    Write-Host ""
    Write-Host "Configuring SQL Database..." -ForegroundColor Yellow
    
    # Wait for SQL Server to be ready
    Write-Host "Waiting for SQL Server to be fully available..." -ForegroundColor Yellow
    Start-Sleep -Seconds 30
    
    # Add current IP to firewall
    Write-Host "Adding your IP address to SQL Server firewall..." -ForegroundColor Yellow
    $myIp = (Invoke-RestMethod -Uri "https://api.ipify.org").Trim()
    Write-Host "  Your IP: $myIp" -ForegroundColor Cyan
    
    az sql server firewall-rule create `
        --resource-group $ResourceGroup `
        --server $sqlServerName `
        --name "ClientIP" `
        --start-ip-address $myIp `
        --end-ip-address $myIp `
        --output none
    
    Write-Host "✓ Firewall rule created" -ForegroundColor Green
    
    # Wait a bit for firewall rule to take effect
    Start-Sleep -Seconds 10
    
    # Import database schema
    Write-Host "Importing database schema..." -ForegroundColor Yellow
    $schemaFile = Join-Path (Split-Path -Parent $PSScriptRoot) "Database-Schema/database_schema.sql"
    
    if (-not (Test-Path $schemaFile)) {
        Write-Error "Schema file not found: $schemaFile"
        exit 1
    }
    
    $authMethod = if ($IsCI) { "ActiveDirectoryAzCli" } else { "ActiveDirectoryDefault" }
    
    sqlcmd -S $sqlServerFqdn -d $databaseName "--authentication-method=$authMethod" -i $schemaFile
    
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to import database schema"
        exit 1
    }
    
    Write-Host "✓ Database schema imported" -ForegroundColor Green
    
    # Create database user for managed identity (SID-based approach)
    Write-Host "Creating database user for managed identity..." -ForegroundColor Yellow
    
    # Convert Client ID to SID hex format
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
    
    sqlcmd -S $sqlServerFqdn -d $databaseName "--authentication-method=$authMethod" -i $tempFile
    
    Remove-Item -Path $tempFile -Force -ErrorAction SilentlyContinue
    
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to create database user for managed identity"
        exit 1
    }
    
    Write-Host "✓ Managed identity granted database permissions" -ForegroundColor Green
    
    # Import stored procedures
    Write-Host "Creating stored procedures..." -ForegroundColor Yellow
    $storedProcFile = Join-Path (Split-Path -Parent $PSScriptRoot) "stored-procedures.sql"
    
    if (Test-Path $storedProcFile) {
        sqlcmd -S $sqlServerFqdn -d $databaseName "--authentication-method=$authMethod" -i $storedProcFile
        
        if ($LASTEXITCODE -eq 0) {
            Write-Host "✓ Stored procedures created" -ForegroundColor Green
        } else {
            Write-Warning "Failed to create stored procedures. You may need to create them manually."
        }
    } else {
        Write-Warning "Stored procedures file not found: $storedProcFile"
    }
}

# Configure App Service settings
Write-Host ""
Write-Host "Configuring App Service settings..." -ForegroundColor Yellow

$connectionString = "Server=tcp:$sqlServerFqdn,1433;Initial Catalog=$databaseName;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;Authentication=Active Directory Managed Identity;User Id=$managedIdentityClientId;"

az webapp config connection-string set `
    --resource-group $ResourceGroup `
    --name $webAppName `
    --connection-string-type SQLAzure `
    --settings DefaultConnection=$connectionString `
    --output none

az webapp config appsettings set `
    --resource-group $ResourceGroup `
    --name $webAppName `
    --settings "AZURE_CLIENT_ID=$managedIdentityClientId" "ManagedIdentityClientId=$managedIdentityClientId" `
    --output none

Write-Host "✓ App Service configured with connection string and managed identity" -ForegroundColor Green

# Configure GenAI settings if deployed
if ($DeployGenAI) {
    Write-Host ""
    Write-Host "Configuring GenAI settings..." -ForegroundColor Yellow
    
    $openAIEndpoint = $outputs.openAIEndpoint.value
    $openAIModelName = $outputs.openAIModelName.value
    $searchEndpoint = $outputs.searchEndpoint.value
    
    Write-Host "  - OpenAI Endpoint: $openAIEndpoint" -ForegroundColor Cyan
    Write-Host "  - Model: $openAIModelName" -ForegroundColor Cyan
    Write-Host "  - Search Endpoint: $searchEndpoint" -ForegroundColor Cyan
    
    az webapp config appsettings set `
        --resource-group $ResourceGroup `
        --name $webAppName `
        --settings "GenAISettings__OpenAIEndpoint=$openAIEndpoint" "GenAISettings__OpenAIModelName=$openAIModelName" "GenAISettings__SearchEndpoint=$searchEndpoint" `
        --output none
    
    Write-Host "✓ GenAI settings configured" -ForegroundColor Green
}

# Save deployment context
Write-Host ""
Write-Host "Saving deployment context..." -ForegroundColor Yellow

$contextPath = Join-Path (Split-Path -Parent $PSScriptRoot) ".deployment-context.json"

$context = @{
    resourceGroup = $ResourceGroup
    location = $Location
    webAppName = $webAppName
    sqlServerFqdn = $sqlServerFqdn
    sqlServerName = $sqlServerName
    databaseName = $databaseName
    managedIdentityClientId = $managedIdentityClientId
    deployedAt = (Get-Date).ToString("yyyy-MM-dd HH:mm:ss")
}

if ($DeployGenAI) {
    $context.openAIEndpoint = $outputs.openAIEndpoint.value
    $context.openAIModelName = $outputs.openAIModelName.value
    $context.searchEndpoint = $outputs.searchEndpoint.value
}

$context | ConvertTo-Json | Out-File -FilePath $contextPath -Encoding UTF8

Write-Host "✓ Context saved to: $contextPath" -ForegroundColor Green

# Summary
Write-Host ""
Write-Host "======================================" -ForegroundColor Cyan
Write-Host "Deployment Complete!" -ForegroundColor Green
Write-Host "======================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "1. Run the application deployment script:" -ForegroundColor White
Write-Host "   .\deploy-app\deploy.ps1" -ForegroundColor Cyan
Write-Host ""
Write-Host "2. Access your application at:" -ForegroundColor White
Write-Host "   https://$webAppName.azurewebsites.net/Index" -ForegroundColor Cyan
Write-Host ""

exit 0
