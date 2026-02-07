<#
.SYNOPSIS
    Deploys Azure infrastructure for the Expense Management application.

.DESCRIPTION
    This script automates the complete infrastructure deployment including:
    - Azure resources via Bicep (App Service, SQL Database, Managed Identity, Monitoring)
    - Optional GenAI resources (Azure OpenAI, AI Search) with -DeployGenAI switch
    - Database schema import
    - Managed identity database user creation (SID-based, no Directory Reader required)
    - Stored procedures deployment
    - App Service configuration
    - Creates .deployment-context.json for app deployment handoff

.PARAMETER ResourceGroup
    Name of the Azure resource group (required). Use fresh names with timestamps.

.PARAMETER Location
    Azure region for deployment (required). Example: 'uksouth', 'eastus'

.PARAMETER BaseName
    Base name for resource naming (optional). Defaults to 'expensemgmt'.

.PARAMETER DeployGenAI
    Switch to deploy GenAI resources (Azure OpenAI, AI Search).

.EXAMPLE
    .\deploy.ps1 -ResourceGroup "rg-expensemgmt-20260207" -Location "uksouth"
    
.EXAMPLE
    .\deploy.ps1 -ResourceGroup "rg-expensemgmt-20260207" -Location "uksouth" -DeployGenAI
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)]
    [string]$ResourceGroup,
    
    [Parameter(Mandatory=$true)]
    [string]$Location,
    
    [Parameter(Mandatory=$false)]
    [string]$BaseName = "expensemgmt",
    
    [Parameter(Mandatory=$false)]
    [switch]$DeployGenAI
)

$ErrorActionPreference = "Stop"

# Display header
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host " Infrastructure Deployment Script" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Detect CI/CD environment
$IsCI = $env:GITHUB_ACTIONS -eq "true" -or $env:TF_BUILD -eq "true" -or $env:CI -eq "true"

if ($IsCI) {
    Write-Host "✓ Running in CI/CD mode (GitHub Actions)" -ForegroundColor Green
} else {
    Write-Host "✓ Running in local/interactive mode" -ForegroundColor Green
}

# PowerShell version check
if ($PSVersionTable.PSVersion.Major -lt 7) {
    Write-Warning "PowerShell 7+ is recommended. You are using version $($PSVersionTable.PSVersion)"
}

# Check Azure CLI
Write-Host ""
Write-Host "Checking prerequisites..." -ForegroundColor Yellow
try {
    $azVersion = az version --output json 2>$null | ConvertFrom-Json
    Write-Host "✓ Azure CLI version $($azVersion.'azure-cli')" -ForegroundColor Green
} catch {
    Write-Error "Azure CLI is not installed. Install from https://aka.ms/azure-cli"
    exit 1
}

# Check sqlcmd
try {
    $sqlcmdVersion = sqlcmd --version 2>&1
    Write-Host "✓ sqlcmd detected: $($sqlcmdVersion -split "`n" | Select-Object -First 1)" -ForegroundColor Green
} catch {
    Write-Error "sqlcmd (go-sqlcmd) is not installed. Install with: winget install sqlcmd"
    exit 1
}

# Check Azure CLI login
Write-Host ""
Write-Host "Checking Azure CLI authentication..." -ForegroundColor Yellow
try {
    $accountInfo = az account show --output json 2>$null | ConvertFrom-Json
    Write-Host "✓ Authenticated to subscription: $($accountInfo.name)" -ForegroundColor Green
} catch {
    Write-Error "Not logged in to Azure. Run: az login"
    exit 1
}

# Get administrator credentials (different for local vs CI/CD)
Write-Host ""
Write-Host "Retrieving administrator credentials..." -ForegroundColor Yellow

if ($IsCI) {
    # CI/CD: Use Service Principal from environment
    $servicePrincipalClientId = $env:AZURE_CLIENT_ID
    if ([string]::IsNullOrWhiteSpace($servicePrincipalClientId)) {
        Write-Error "AZURE_CLIENT_ID environment variable not found. Ensure OIDC authentication is configured."
        exit 1
    }
    
    $spInfo = az ad sp show --id $servicePrincipalClientId --output json 2>$null | ConvertFrom-Json
    $sqlAdminObjectId = $spInfo.id  # Object ID
    $sqlAdminLogin = $spInfo.displayName
    $adminPrincipalType = "Application"
    $authMethod = "ActiveDirectoryAzCli"
    
    Write-Host "✓ Using Service Principal: $sqlAdminLogin" -ForegroundColor Green
    Write-Host "  Object ID: $sqlAdminObjectId" -ForegroundColor Gray
} else {
    # Local: Use signed-in user
    $userInfo = az ad signed-in-user show --output json 2>$null | ConvertFrom-Json
    $sqlAdminObjectId = $userInfo.id  # Object ID
    $sqlAdminLogin = $userInfo.userPrincipalName
    $adminPrincipalType = "User"
    $authMethod = "ActiveDirectoryDefault"
    
    Write-Host "✓ Using signed-in user: $sqlAdminLogin" -ForegroundColor Green
    Write-Host "  Object ID: $sqlAdminObjectId" -ForegroundColor Gray
}

# Create resource group
Write-Host ""
Write-Host "Creating resource group: $ResourceGroup in $Location..." -ForegroundColor Yellow
az group create --name $ResourceGroup --location $Location --output none 2>$null
if ($LASTEXITCODE -eq 0) {
    Write-Host "✓ Resource group ready" -ForegroundColor Green
} else {
    Write-Error "Failed to create resource group"
    exit 1
}

# Deploy Bicep infrastructure
Write-Host ""
Write-Host "Deploying Azure infrastructure..." -ForegroundColor Yellow
Write-Host "  Resource Group: $ResourceGroup" -ForegroundColor Gray
Write-Host "  Location: $Location" -ForegroundColor Gray
Write-Host "  Base Name: $BaseName" -ForegroundColor Gray
Write-Host "  Deploy GenAI: $DeployGenAI" -ForegroundColor Gray
Write-Host ""
Write-Host "(This may take 3-5 minutes...)" -ForegroundColor Yellow

$scriptDir = $PSScriptRoot
$bicepFile = Join-Path $scriptDir "main.bicep"

$deployOutput = az deployment group create `
    --resource-group $ResourceGroup `
    --template-file $bicepFile `
    --parameters location=$Location baseName=$BaseName sqlAdminObjectId=$sqlAdminObjectId sqlAdminLogin=$sqlAdminLogin adminPrincipalType=$adminPrincipalType deployGenAI=$($DeployGenAI.ToString().ToLower()) `
    --output json 2>$null

# Handle Azure Policy timing issues
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($deployOutput)) {
    Write-Warning "Deployment command returned an error. Checking for Azure Policy timing issues..."
    Write-Host "Waiting for policy deployments to settle..." -ForegroundColor Yellow
    Start-Sleep -Seconds 15
    
    # Find the main Bicep deployment (not policy-related)
    $allDeployments = az deployment group list --resource-group $ResourceGroup --output json 2>$null | ConvertFrom-Json
    $mainDeployment = $allDeployments | Where-Object { 
        $_.name -notlike "PolicyDeployment_*" -and 
        $_.name -notlike "Failure-Anomalies-*" -and
        $_.properties.provisioningState -eq "Succeeded"
    } | Sort-Object -Property @{Expression={[datetime]$_.properties.timestamp}; Descending=$true} | Select-Object -First 1
    
    if ($mainDeployment) {
        Write-Host "✓ Found successful deployment: $($mainDeployment.name)" -ForegroundColor Green
        $deployOutput = az deployment group show --resource-group $ResourceGroup --name $mainDeployment.name --output json 2>$null
    } else {
        Write-Error "Infrastructure deployment failed. Check Azure portal for details."
        exit 1
    }
}

$deployment = $deployOutput | ConvertFrom-Json

# Extract outputs
$webAppName = $deployment.properties.outputs.webAppName.value
$sqlServerFqdn = $deployment.properties.outputs.sqlServerFqdn.value
$databaseName = $deployment.properties.outputs.databaseName.value
$managedIdentityName = $deployment.properties.outputs.managedIdentityName.value
$managedIdentityClientId = $deployment.properties.outputs.managedIdentityClientId.value
$appInsightsConnectionString = $deployment.properties.outputs.appInsightsConnectionString.value
$openAIEndpoint = $deployment.properties.outputs.openAIEndpoint.value
$openAIModelName = $deployment.properties.outputs.openAIModelName.value

Write-Host ""
Write-Host "✓ Infrastructure deployment succeeded" -ForegroundColor Green
Write-Host "  Web App: $webAppName" -ForegroundColor Gray
Write-Host "  SQL Server: $sqlServerFqdn" -ForegroundColor Gray
Write-Host "  Database: $databaseName" -ForegroundColor Gray
Write-Host "  Managed Identity: $managedIdentityName" -ForegroundColor Gray

# Wait for SQL Server to be ready
Write-Host ""
Write-Host "Waiting for SQL Server to be fully ready..." -ForegroundColor Yellow
Start-Sleep -Seconds 30
Write-Host "✓ SQL Server ready" -ForegroundColor Green

# Add current IP to SQL firewall (local mode only)
if (-not $IsCI) {
    Write-Host ""
    Write-Host "Adding your IP to SQL Server firewall..." -ForegroundColor Yellow
    try {
        $publicIp = (Invoke-RestMethod -Uri "https://api.ipify.org?format=json").ip
        $sqlServerName = $sqlServerFqdn -replace '\.database\.windows\.net$', ''
        
        az sql server firewall-rule create `
            --resource-group $ResourceGroup `
            --server $sqlServerName `
            --name "DeploymentScript" `
            --start-ip-address $publicIp `
            --end-ip-address $publicIp `
            --output none 2>$null
        
        Write-Host "✓ Firewall rule created for IP: $publicIp" -ForegroundColor Green
    } catch {
        Write-Warning "Could not add firewall rule. You may need to add it manually."
    }
}

# Import database schema
Write-Host ""
Write-Host "Importing database schema..." -ForegroundColor Yellow
$repoRoot = Split-Path -Parent $scriptDir
$schemaFile = Join-Path $repoRoot "Database-Schema/database_schema.sql"

if (-not (Test-Path $schemaFile)) {
    Write-Error "Database schema file not found: $schemaFile"
    exit 1
}

try {
    sqlcmd -S $sqlServerFqdn -d $databaseName "--authentication-method=$authMethod" -i $schemaFile 2>&1 | Out-Null
    Write-Host "✓ Database schema imported" -ForegroundColor Green
} catch {
    Write-Error "Failed to import database schema: $_"
    exit 1
}

# Create managed identity database user (SID-based, no Directory Reader required)
Write-Host ""
Write-Host "Creating managed identity database user..." -ForegroundColor Yellow

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
    sqlcmd -S $sqlServerFqdn -d $databaseName "--authentication-method=$authMethod" -i $tempFile 2>&1 | Out-Null
    Write-Host "✓ Managed identity user created with permissions" -ForegroundColor Green
} catch {
    Write-Error "Failed to create managed identity user: $_"
    exit 1
} finally {
    Remove-Item -Path $tempFile -Force -ErrorAction SilentlyContinue
}

# Import stored procedures
Write-Host ""
Write-Host "Importing stored procedures..." -ForegroundColor Yellow
$storedProcsFile = Join-Path $repoRoot "stored-procedures.sql"

if (-not (Test-Path $storedProcsFile)) {
    Write-Error "Stored procedures file not found: $storedProcsFile"
    exit 1
}

try {
    sqlcmd -S $sqlServerFqdn -d $databaseName "--authentication-method=$authMethod" -i $storedProcsFile 2>&1 | Out-Null
    Write-Host "✓ Stored procedures imported" -ForegroundColor Green
} catch {
    Write-Error "Failed to import stored procedures: $_"
    exit 1
}

# Configure App Service settings
Write-Host ""
Write-Host "Configuring App Service settings..." -ForegroundColor Yellow

# Build connection string with Managed Identity authentication
$connectionString = "Server=tcp:$sqlServerFqdn,1433;Initial Catalog=$databaseName;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;Authentication=Active Directory Managed Identity;User Id=$managedIdentityClientId;"

# Core settings (always configured)
$appSettings = @"
[
    {
        "name": "AZURE_CLIENT_ID",
        "value": "$managedIdentityClientId"
    },
    {
        "name": "APPLICATIONINSIGHTS_CONNECTION_STRING",
        "value": "$appInsightsConnectionString"
    }
]
"@

az webapp config appsettings set `
    --resource-group $ResourceGroup `
    --name $webAppName `
    --settings @- `
    --output none 2>$null <<< $appSettings

# Connection string (separate command)
az webapp config connection-string set `
    --resource-group $ResourceGroup `
    --name $webAppName `
    --connection-string-type SQLAzure `
    --settings DefaultConnection="$connectionString" `
    --output none 2>$null

Write-Host "✓ Core App Service settings configured" -ForegroundColor Green

# GenAI settings (if deployed)
if ($DeployGenAI -and -not [string]::IsNullOrWhiteSpace($openAIEndpoint)) {
    Write-Host ""
    Write-Host "Configuring GenAI settings..." -ForegroundColor Yellow
    
    $genAISettings = @"
[
    {
        "name": "GenAISettings__OpenAIEndpoint",
        "value": "$openAIEndpoint"
    },
    {
        "name": "GenAISettings__OpenAIModelName",
        "value": "$openAIModelName"
    },
    {
        "name": "ManagedIdentityClientId",
        "value": "$managedIdentityClientId"
    }
]
"@
    
    az webapp config appsettings set `
        --resource-group $ResourceGroup `
        --name $webAppName `
        --settings @- `
        --output none 2>$null <<< $genAISettings
    
    Write-Host "✓ GenAI settings configured" -ForegroundColor Green
}

# Create deployment context file
Write-Host ""
Write-Host "Creating deployment context file..." -ForegroundColor Yellow

$contextFile = Join-Path $repoRoot ".deployment-context.json"
$context = @{
    resourceGroup = $ResourceGroup
    location = $Location
    webAppName = $webAppName
    sqlServerFqdn = $sqlServerFqdn
    databaseName = $databaseName
    managedIdentityName = $managedIdentityName
    managedIdentityClientId = $managedIdentityClientId
    appInsightsConnectionString = $appInsightsConnectionString
    deployGenAI = $DeployGenAI.IsPresent
    openAIEndpoint = $openAIEndpoint
    openAIModelName = $openAIModelName
    timestamp = (Get-Date).ToString("yyyy-MM-ddTHH:mm:ssZ")
}

$context | ConvertTo-Json -Depth 10 | Out-File -FilePath $contextFile -Encoding UTF8
Write-Host "✓ Context file created: .deployment-context.json" -ForegroundColor Green

# Display summary
Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host " Infrastructure Deployment Complete" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
Write-Host "Resource Group:     $ResourceGroup" -ForegroundColor Cyan
Write-Host "Location:           $Location" -ForegroundColor Cyan
Write-Host "Web App:            $webAppName" -ForegroundColor Cyan
Write-Host "SQL Server:         $sqlServerFqdn" -ForegroundColor Cyan
Write-Host "Database:           $databaseName" -ForegroundColor Cyan
Write-Host "Managed Identity:   $managedIdentityName" -ForegroundColor Cyan

if ($DeployGenAI -and -not [string]::IsNullOrWhiteSpace($openAIEndpoint)) {
    Write-Host ""
    Write-Host "GenAI Resources:" -ForegroundColor Yellow
    Write-Host "  OpenAI Endpoint: $openAIEndpoint" -ForegroundColor Gray
    Write-Host "  Model Name:      $openAIModelName" -ForegroundColor Gray
}

Write-Host ""
Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "  1. Deploy the application with: .\deploy-app\deploy.ps1" -ForegroundColor Gray
Write-Host "  2. Or run both with: .\deploy-all.ps1 -ResourceGroup $ResourceGroup -Location $Location" -ForegroundColor Gray
Write-Host ""
