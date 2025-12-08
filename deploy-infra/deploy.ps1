#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Deploys the infrastructure for the Expense Management application.

.DESCRIPTION
    This script deploys all Azure infrastructure including App Service, SQL Database, 
    Managed Identity, Monitoring, and optionally GenAI resources (Azure OpenAI and AI Search).
    
    The script handles:
    - Azure CLI validation and authentication
    - Resource group creation
    - Bicep template deployment
    - SQL Server configuration and firewall rules
    - Database schema import
    - Stored procedures deployment
    - Managed identity database user creation
    - App Service configuration
    - Deployment context file creation

.PARAMETER ResourceGroup
    The name of the Azure resource group to create/use. Should be unique (include date/time).

.PARAMETER Location
    The Azure region where resources will be deployed (e.g., 'uksouth', 'eastus').

.PARAMETER BaseName
    The base name for all resources. Defaults to 'expensemgmt'.

.PARAMETER DeployGenAI
    Switch to deploy GenAI resources (Azure OpenAI and AI Search).

.PARAMETER SkipDatabaseSetup
    Switch to skip database schema and stored procedures setup (useful for redeployments).

.EXAMPLE
    .\deploy.ps1 -ResourceGroup "rg-expensemgmt-20251207" -Location "uksouth"

.EXAMPLE
    .\deploy.ps1 -ResourceGroup "rg-expensemgmt-20251207" -Location "uksouth" -DeployGenAI
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ResourceGroup,
    
    [Parameter(Mandatory = $true)]
    [string]$Location,
    
    [Parameter(Mandatory = $false)]
    [string]$BaseName = 'expensemgmt',
    
    [Parameter(Mandatory = $false)]
    [switch]$DeployGenAI,
    
    [Parameter(Mandatory = $false)]
    [switch]$SkipDatabaseSetup
)

$ErrorActionPreference = 'Stop'

# Detect CI/CD environment
$IsCI = $env:GITHUB_ACTIONS -eq "true" -or $env:TF_BUILD -eq "true" -or $env:CI -eq "true"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Expense Management - Infrastructure   " -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

if ($IsCI) {
    Write-Host "Running in CI/CD mode" -ForegroundColor Yellow
} else {
    Write-Host "Running in interactive mode" -ForegroundColor Yellow
}

# Check PowerShell version
if ($PSVersionTable.PSVersion.Major -lt 7) {
    Write-Warning "You are using PowerShell $($PSVersionTable.PSVersion). PowerShell 7+ is recommended for best compatibility."
}

# Check Azure CLI is installed
Write-Host "Checking Azure CLI..." -ForegroundColor Cyan
try {
    $azVersion = az version --output json 2>$null | ConvertFrom-Json
    Write-Host "✓ Azure CLI version: $($azVersion.'azure-cli')" -ForegroundColor Green
} catch {
    Write-Error "Azure CLI is not installed. Please install from: https://aka.ms/installazurecliwindows"
    exit 1
}

# Check if logged in to Azure
Write-Host "Checking Azure authentication..." -ForegroundColor Cyan
try {
    $account = az account show 2>$null | ConvertFrom-Json
    Write-Host "✓ Logged in as: $($account.user.name)" -ForegroundColor Green
    Write-Host "✓ Subscription: $($account.name)" -ForegroundColor Green
} catch {
    Write-Error "Not logged in to Azure. Please run 'az login' first."
    exit 1
}

# Get administrator credentials based on environment
Write-Host "Retrieving Azure AD credentials..." -ForegroundColor Cyan
if ($IsCI) {
    # CI/CD: Use Service Principal
    $servicePrincipalClientId = $env:AZURE_CLIENT_ID
    if ([string]::IsNullOrEmpty($servicePrincipalClientId)) {
        Write-Error "AZURE_CLIENT_ID environment variable not found in CI/CD environment"
        exit 1
    }
    
    $spInfo = az ad sp show --id $servicePrincipalClientId 2>$null | ConvertFrom-Json
    if (-not $spInfo) {
        Write-Error "Failed to retrieve Service Principal information"
        exit 1
    }
    
    $adminObjectId = $spInfo.id
    $adminUsername = $spInfo.displayName
    $adminPrincipalType = "Application"
    
    Write-Host "✓ Service Principal: $adminUsername" -ForegroundColor Green
    Write-Host "✓ Object ID: $adminObjectId" -ForegroundColor Green
} else {
    # Interactive: Use current user
    $currentUser = az ad signed-in-user show 2>$null | ConvertFrom-Json
    if (-not $currentUser) {
        Write-Error "Failed to retrieve current user information"
        exit 1
    }
    
    $adminObjectId = $currentUser.id
    $adminUsername = $currentUser.userPrincipalName
    $adminPrincipalType = "User"
    
    Write-Host "✓ User: $adminUsername" -ForegroundColor Green
    Write-Host "✓ Object ID: $adminObjectId" -ForegroundColor Green
}

# Create resource group if it doesn't exist
Write-Host ""
Write-Host "Creating resource group..." -ForegroundColor Cyan
$rgExists = az group exists --name $ResourceGroup
if ($rgExists -eq "false") {
    az group create --name $ResourceGroup --location $Location --output none
    Write-Host "✓ Resource group created: $ResourceGroup" -ForegroundColor Green
} else {
    Write-Host "✓ Resource group already exists: $ResourceGroup" -ForegroundColor Yellow
}

# Deploy Bicep template
Write-Host ""
Write-Host "Deploying infrastructure (this may take 5-10 minutes)..." -ForegroundColor Cyan
$deploymentName = "infra-$(Get-Date -Format 'yyyyMMddHHmmss')"

# Capture stdout and stderr separately - warnings go to stderr and break JSON parsing if mixed
$deployOutput = $null
$deployError = $null
$deployOutput = az deployment group create `
    --resource-group $ResourceGroup `
    --name $deploymentName `
    --template-file "./deploy-infra/main.bicep" `
    --parameters location=$Location baseName=$BaseName adminObjectId=$adminObjectId adminUsername=$adminUsername adminPrincipalType=$adminPrincipalType deployGenAI=$($DeployGenAI.ToString().ToLower()) `
    --output json 2>$null

if ($LASTEXITCODE -ne 0) {
    # Re-run to capture the actual error
    $deployError = az deployment group create `
        --resource-group $ResourceGroup `
        --name "$deploymentName-retry" `
        --template-file "./deploy-infra/main.bicep" `
        --parameters location=$Location baseName=$BaseName adminObjectId=$adminObjectId adminUsername=$adminUsername adminPrincipalType=$adminPrincipalType deployGenAI=$($DeployGenAI.ToString().ToLower()) `
        --output json 2>&1
    Write-Error "Infrastructure deployment failed:`n$deployError"
    exit 1
}

$deployment = $deployOutput | ConvertFrom-Json
Write-Host "✓ Infrastructure deployed successfully" -ForegroundColor Green

# Extract outputs
$outputs = $deployment.properties.outputs
$webAppName = $outputs.webAppName.value
$sqlServerFqdn = $outputs.sqlServerFqdn.value
$sqlServerName = $outputs.sqlServerName.value
$sqlDatabaseName = $outputs.sqlDatabaseName.value
$managedIdentityClientId = $outputs.managedIdentityClientId.value
$managedIdentityName = $outputs.managedIdentityName.value
$appInsightsConnectionString = $outputs.appInsightsConnectionString.value

Write-Host ""
Write-Host "Deployed Resources:" -ForegroundColor Cyan
Write-Host "  Web App: $webAppName" -ForegroundColor White
Write-Host "  SQL Server: $sqlServerFqdn" -ForegroundColor White
Write-Host "  Database: $sqlDatabaseName" -ForegroundColor White
Write-Host "  Managed Identity: $managedIdentityName" -ForegroundColor White

if (-not $SkipDatabaseSetup) {
    # Add current IP to SQL Server firewall
    Write-Host ""
    Write-Host "Configuring SQL Server firewall..." -ForegroundColor Cyan
    
    if (-not $IsCI) {
        # Get current public IP for interactive mode
        try {
            $currentIP = (Invoke-WebRequest -Uri "https://api.ipify.org" -UseBasicParsing).Content.Trim()
            az sql server firewall-rule create `
                --resource-group $ResourceGroup `
                --server $sqlServerName `
                --name "CurrentIP" `
                --start-ip-address $currentIP `
                --end-ip-address $currentIP `
                --output none 2>$null
            Write-Host "✓ Added firewall rule for IP: $currentIP" -ForegroundColor Green
        } catch {
            Write-Warning "Could not add current IP to firewall. Manual configuration may be needed."
        }
    }
    
    # Wait for SQL Server to be ready
    Write-Host "Waiting for SQL Server to be ready..." -ForegroundColor Cyan
    Start-Sleep -Seconds 30
    
    # Import database schema
    Write-Host ""
    Write-Host "Importing database schema..." -ForegroundColor Cyan
    $schemaFile = "./Database-Schema/database_schema.sql"
    
    if (-not (Test-Path $schemaFile)) {
        Write-Error "Database schema file not found: $schemaFile"
        exit 1
    }
    
    $authMethod = if ($IsCI) { "ActiveDirectoryAzCli" } else { "ActiveDirectoryDefault" }
    
    try {
        sqlcmd -S $sqlServerFqdn -d $sqlDatabaseName "--authentication-method=$authMethod" -i $schemaFile
        if ($LASTEXITCODE -eq 0) {
            Write-Host "✓ Database schema imported successfully" -ForegroundColor Green
        } else {
            Write-Warning "Database schema import may have encountered issues. Check output above."
        }
    } catch {
        Write-Error "Failed to import database schema: $_"
        exit 1
    }
    
    # Create managed identity database user
    Write-Host ""
    Write-Host "Configuring managed identity database access..." -ForegroundColor Cyan
    
    # Convert Client ID to SID for database user creation
    $guidBytes = [System.Guid]::Parse($managedIdentityClientId).ToByteArray()
    $sidHex = "0x" + [System.BitConverter]::ToString($guidBytes).Replace("-", "")
    
    $createUserSql = @"
-- Check if user already exists and drop if needed
IF EXISTS (SELECT * FROM sys.database_principals WHERE name = '$managedIdentityName')
BEGIN
    DROP USER [$managedIdentityName];
END

-- Create user with SID (no Directory Reader required)
CREATE USER [$managedIdentityName] WITH SID = $sidHex, TYPE = E;

-- Grant database roles
ALTER ROLE db_datareader ADD MEMBER [$managedIdentityName];
ALTER ROLE db_datawriter ADD MEMBER [$managedIdentityName];

-- Grant execute permission for stored procedures
GRANT EXECUTE TO [$managedIdentityName];
"@
    
    $tempSqlFile = [System.IO.Path]::GetTempFileName() + ".sql"
    try {
        $createUserSql | Out-File -FilePath $tempSqlFile -Encoding UTF8
        sqlcmd -S $sqlServerFqdn -d $sqlDatabaseName "--authentication-method=$authMethod" -i $tempSqlFile
        
        if ($LASTEXITCODE -eq 0) {
            Write-Host "✓ Managed identity database user created with permissions" -ForegroundColor Green
        } else {
            Write-Warning "Managed identity user creation may have encountered issues."
        }
    } finally {
        Remove-Item -Path $tempSqlFile -Force -ErrorAction SilentlyContinue
    }
    
    # Deploy stored procedures
    Write-Host ""
    Write-Host "Deploying stored procedures..." -ForegroundColor Cyan
    $storedProcFile = "./stored-procedures.sql"
    
    if (Test-Path $storedProcFile) {
        try {
            sqlcmd -S $sqlServerFqdn -d $sqlDatabaseName "--authentication-method=$authMethod" -i $storedProcFile
            if ($LASTEXITCODE -eq 0) {
                Write-Host "✓ Stored procedures deployed successfully" -ForegroundColor Green
            } else {
                Write-Warning "Stored procedures deployment may have encountered issues."
            }
        } catch {
            Write-Warning "Failed to deploy stored procedures: $_"
        }
    } else {
        Write-Warning "Stored procedures file not found: $storedProcFile"
    }
}

# Configure App Service settings
Write-Host ""
Write-Host "Configuring App Service settings..." -ForegroundColor Cyan

# Build connection string with managed identity authentication
$connectionString = "Server=tcp:$sqlServerFqdn,1433;Initial Catalog=$sqlDatabaseName;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;Authentication=Active Directory Managed Identity;User Id=$managedIdentityClientId;"

# Set critical App Service configuration
az webapp config connection-string set `
    --name $webAppName `
    --resource-group $ResourceGroup `
    --connection-string-type SQLAzure `
    --settings DefaultConnection="$connectionString" `
    --output none

az webapp config appsettings set `
    --name $webAppName `
    --resource-group $ResourceGroup `
    --settings `
        "AZURE_CLIENT_ID=$managedIdentityClientId" `
        "ManagedIdentityClientId=$managedIdentityClientId" `
    --output none

Write-Host "✓ App Service configured with connection string and managed identity" -ForegroundColor Green

# Configure GenAI settings if deployed
if ($DeployGenAI) {
    $openAIEndpoint = $outputs.openAIEndpoint.value
    $openAIModelName = $outputs.openAIModelName.value
    
    if (-not [string]::IsNullOrEmpty($openAIEndpoint)) {
        Write-Host "Configuring GenAI settings..." -ForegroundColor Cyan
        
        az webapp config appsettings set `
            --name $webAppName `
            --resource-group $ResourceGroup `
            --settings `
                "GenAISettings__OpenAIEndpoint=$openAIEndpoint" `
                "GenAISettings__OpenAIModelName=$openAIModelName" `
            --output none
        
        Write-Host "✓ GenAI settings configured" -ForegroundColor Green
    }
}

# Save deployment context
Write-Host ""
Write-Host "Saving deployment context..." -ForegroundColor Cyan

$contextFile = "./.deployment-context.json"
$context = @{
    resourceGroup = $ResourceGroup
    location = $Location
    webAppName = $webAppName
    sqlServerFqdn = $sqlServerFqdn
    sqlServerName = $sqlServerName
    sqlDatabaseName = $sqlDatabaseName
    managedIdentityClientId = $managedIdentityClientId
    managedIdentityName = $managedIdentityName
    deploymentDate = (Get-Date).ToString("yyyy-MM-dd HH:mm:ss")
}

if ($DeployGenAI -and $outputs.openAIEndpoint.value) {
    $context.openAIEndpoint = $outputs.openAIEndpoint.value
    $context.openAIModelName = $outputs.openAIModelName.value
}

$context | ConvertTo-Json | Out-File -FilePath $contextFile -Encoding UTF8
Write-Host "✓ Deployment context saved to: $contextFile" -ForegroundColor Green

# Summary
Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "  Infrastructure Deployment Complete!   " -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Cyan
Write-Host "1. Deploy the application code:" -ForegroundColor White
Write-Host "   .\deploy-app\deploy.ps1" -ForegroundColor Yellow
Write-Host ""
Write-Host "2. Once deployed, access your application at:" -ForegroundColor White
Write-Host "   https://$($outputs.webAppHostName.value)/Index" -ForegroundColor Yellow
Write-Host ""

if ($DeployGenAI -and $outputs.openAIEndpoint.value) {
    Write-Host "3. Access the AI Chat interface at:" -ForegroundColor White
    Write-Host "   https://$($outputs.webAppHostName.value)/Chat" -ForegroundColor Yellow
    Write-Host ""
}

Write-Host "Deployment context saved for seamless app deployment." -ForegroundColor White
Write-Host ""

exit 0
