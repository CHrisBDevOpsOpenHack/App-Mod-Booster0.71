<#
.SYNOPSIS
    Deploys infrastructure for the Expense Management application.

.DESCRIPTION
    This script automates the deployment of all Azure infrastructure including:
    - Resource Group (if needed)
    - Managed Identity
    - App Service with App Service Plan
    - Azure SQL Database with Entra ID authentication
    - Application Insights and Log Analytics
    - Optionally: Azure OpenAI and AI Search

.PARAMETER ResourceGroup
    Name of the Azure resource group (required)

.PARAMETER Location
    Azure region for deployment (required)

.PARAMETER BaseName
    Base name for resources (optional, defaults to 'expensemgmt')

.PARAMETER DeployGenAI
    Switch to deploy Azure OpenAI and AI Search resources

.PARAMETER SkipDatabaseSetup
    Switch to skip database schema and stored procedure deployment

.EXAMPLE
    .\deploy.ps1 -ResourceGroup "rg-expensemgmt-20251208" -Location "uksouth"

.EXAMPLE
    .\deploy.ps1 -ResourceGroup "rg-expensemgmt-20251208" -Location "uksouth" -DeployGenAI
#>

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
Write-Host "Infrastructure Deployment Script" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Resource Group: $ResourceGroup"
Write-Host "Location: $Location"
Write-Host "Base Name: $BaseName"
Write-Host "Deploy GenAI: $DeployGenAI"
Write-Host "CI/CD Mode: $IsCI"
Write-Host ""

# Check PowerShell version
if ($PSVersionTable.PSVersion.Major -lt 7) {
    Write-Host "Warning: Running PowerShell $($PSVersionTable.PSVersion). PowerShell 7+ is recommended." -ForegroundColor Yellow
}

# Check Azure CLI is installed
Write-Host "Checking Azure CLI..." -ForegroundColor Yellow
try {
    $azVersion = az version --output json 2>$null | ConvertFrom-Json
    Write-Host "Azure CLI version: $($azVersion.'azure-cli')" -ForegroundColor Green
} catch {
    Write-Host "Error: Azure CLI is not installed or not in PATH." -ForegroundColor Red
    exit 1
}

# Check user is logged in
Write-Host "Checking Azure login status..." -ForegroundColor Yellow
$account = az account show --output json 2>$null | ConvertFrom-Json
if (-not $account) {
    Write-Host "Error: Not logged in to Azure. Please run 'az login' first." -ForegroundColor Red
    exit 1
}
Write-Host "Logged in as: $($account.user.name)" -ForegroundColor Green
Write-Host "Subscription: $($account.name)" -ForegroundColor Green

# Get admin credentials based on environment
if ($IsCI) {
    Write-Host "CI/CD mode: Using Service Principal credentials..." -ForegroundColor Yellow
    $servicePrincipalClientId = $env:AZURE_CLIENT_ID
    if (-not $servicePrincipalClientId) {
        Write-Host "Error: AZURE_CLIENT_ID environment variable not set." -ForegroundColor Red
        exit 1
    }
    
    $spInfo = az ad sp show --id $servicePrincipalClientId --output json 2>$null | ConvertFrom-Json
    $adminObjectId = $spInfo.id
    $adminPrincipalName = $spInfo.displayName
    $adminPrincipalType = "Application"
    
    Write-Host "Service Principal: $adminPrincipalName" -ForegroundColor Green
} else {
    Write-Host "Interactive mode: Getting current user credentials..." -ForegroundColor Yellow
    $currentUser = az ad signed-in-user show --output json 2>$null | ConvertFrom-Json
    $adminObjectId = $currentUser.id
    $adminPrincipalName = $currentUser.userPrincipalName
    $adminPrincipalType = "User"
    
    Write-Host "Admin User: $adminPrincipalName" -ForegroundColor Green
}

Write-Host "Admin Object ID: $adminObjectId" -ForegroundColor Green

# Create resource group if it doesn't exist
Write-Host ""
Write-Host "Creating resource group if needed..." -ForegroundColor Yellow
az group create --name $ResourceGroup --location $Location --output none
Write-Host "Resource group ready: $ResourceGroup" -ForegroundColor Green

# Deploy Bicep templates
Write-Host ""
Write-Host "Deploying infrastructure with Bicep..." -ForegroundColor Yellow
Write-Host "This may take several minutes..." -ForegroundColor Yellow

$scriptDir = $PSScriptRoot
$templateFile = Join-Path $scriptDir "main.bicep"

$deployGenAILower = $DeployGenAI.ToString().ToLower()

$deploymentOutput = az deployment group create `
    --resource-group $ResourceGroup `
    --template-file $templateFile `
    --parameters location=$Location baseName=$BaseName deployGenAI=$deployGenAILower adminObjectId=$adminObjectId adminPrincipalName=$adminPrincipalName adminPrincipalType=$adminPrincipalType `
    --output json 2>$null

if ($LASTEXITCODE -ne 0) {
    Write-Host "Error: Bicep deployment failed." -ForegroundColor Red
    exit 1
}

$deployment = $deploymentOutput | ConvertFrom-Json
$outputs = $deployment.properties.outputs

Write-Host "Infrastructure deployment completed!" -ForegroundColor Green

# Extract outputs
$webAppName = $outputs.webAppName.value
$sqlServerFqdn = $outputs.sqlServerFqdn.value
$databaseName = $outputs.databaseName.value
$managedIdentityName = $outputs.managedIdentityName.value
$managedIdentityClientId = $outputs.managedIdentityClientId.value
$appInsightsConnectionString = $outputs.appInsightsConnectionString.value

Write-Host ""
Write-Host "Deployed Resources:" -ForegroundColor Cyan
Write-Host "  Web App: $webAppName"
Write-Host "  SQL Server: $sqlServerFqdn"
Write-Host "  Database: $databaseName"
Write-Host "  Managed Identity: $managedIdentityName"

if ($DeployGenAI) {
    $openAIEndpoint = $outputs.openAIEndpoint.value
    $openAIModelName = $outputs.openAIModelName.value
    $searchEndpoint = $outputs.searchEndpoint.value
    Write-Host "  OpenAI Endpoint: $openAIEndpoint"
    Write-Host "  OpenAI Model: $openAIModelName"
    Write-Host "  Search Endpoint: $searchEndpoint"
}

if (-not $SkipDatabaseSetup) {
    Write-Host ""
    Write-Host "Setting up database..." -ForegroundColor Yellow
    
    # Add current IP to firewall
    Write-Host "Adding current IP to SQL firewall..." -ForegroundColor Yellow
    $sqlServerName = $sqlServerFqdn.Split('.')[0]
    
    if (-not $IsCI) {
        $currentIp = (Invoke-RestMethod -Uri "https://api.ipify.org?format=text" -TimeoutSec 10)
        Write-Host "Current IP: $currentIp" -ForegroundColor Green
        
        az sql server firewall-rule create `
            --resource-group $ResourceGroup `
            --server $sqlServerName `
            --name "DeploymentClient" `
            --start-ip-address $currentIp `
            --end-ip-address $currentIp `
            --output none 2>$null
    }
    
    # Wait for SQL Server to be ready
    Write-Host "Waiting for SQL Server to be ready..." -ForegroundColor Yellow
    Start-Sleep -Seconds 30
    
    # Determine authentication method
    $authMethod = if ($IsCI) { "ActiveDirectoryAzCli" } else { "ActiveDirectoryDefault" }
    
    # Import database schema
    Write-Host "Importing database schema..." -ForegroundColor Yellow
    $repoRoot = Split-Path -Parent $scriptDir
    $schemaFile = Join-Path $repoRoot "Database-Schema/database_schema.sql"
    
    if (Test-Path $schemaFile) {
        sqlcmd -S $sqlServerFqdn -d $databaseName "--authentication-method=$authMethod" -i $schemaFile
        if ($LASTEXITCODE -eq 0) {
            Write-Host "Database schema imported successfully." -ForegroundColor Green
        } else {
            Write-Host "Warning: Database schema import may have failed." -ForegroundColor Yellow
        }
    } else {
        Write-Host "Warning: Schema file not found at $schemaFile" -ForegroundColor Yellow
    }
    
    # Create managed identity database user using SID-based approach
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

PRINT 'Managed identity user created and permissions granted.';
"@
    
    $tempFile = [System.IO.Path]::GetTempFileName() + ".sql"
    $createUserSql | Out-File -FilePath $tempFile -Encoding UTF8
    
    sqlcmd -S $sqlServerFqdn -d $databaseName "--authentication-method=$authMethod" -i $tempFile
    Remove-Item -Path $tempFile -Force -ErrorAction SilentlyContinue
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "Managed identity database user created." -ForegroundColor Green
    } else {
        Write-Host "Warning: Managed identity user creation may have failed." -ForegroundColor Yellow
    }
    
    # Import stored procedures
    Write-Host "Importing stored procedures..." -ForegroundColor Yellow
    $storedProcsFile = Join-Path $repoRoot "stored-procedures.sql"
    
    if (Test-Path $storedProcsFile) {
        sqlcmd -S $sqlServerFqdn -d $databaseName "--authentication-method=$authMethod" -i $storedProcsFile
        if ($LASTEXITCODE -eq 0) {
            Write-Host "Stored procedures imported successfully." -ForegroundColor Green
        } else {
            Write-Host "Warning: Stored procedures import may have failed." -ForegroundColor Yellow
        }
    } else {
        Write-Host "Warning: Stored procedures file not found at $storedProcsFile" -ForegroundColor Yellow
    }
}

# Configure App Service settings
Write-Host ""
Write-Host "Configuring App Service settings..." -ForegroundColor Yellow

$connectionString = "Server=tcp:$sqlServerFqdn,1433;Initial Catalog=$databaseName;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;Authentication=Active Directory Managed Identity;User Id=$managedIdentityClientId;"

az webapp config appsettings set `
    --resource-group $ResourceGroup `
    --name $webAppName `
    --settings "AZURE_CLIENT_ID=$managedIdentityClientId" "ManagedIdentityClientId=$managedIdentityClientId" `
    --output none

az webapp config connection-string set `
    --resource-group $ResourceGroup `
    --name $webAppName `
    --connection-string-type SQLAzure `
    --settings "DefaultConnection=$connectionString" `
    --output none

if ($DeployGenAI) {
    az webapp config appsettings set `
        --resource-group $ResourceGroup `
        --name $webAppName `
        --settings "GenAISettings__OpenAIEndpoint=$openAIEndpoint" "GenAISettings__OpenAIModelName=$openAIModelName" "GenAISettings__SearchEndpoint=$searchEndpoint" `
        --output none
}

Write-Host "App Service settings configured." -ForegroundColor Green

# Save deployment context for app deployment script
Write-Host ""
Write-Host "Saving deployment context..." -ForegroundColor Yellow

$context = @{
    ResourceGroup = $ResourceGroup
    WebAppName = $webAppName
    SqlServerFqdn = $sqlServerFqdn
    DatabaseName = $databaseName
    ManagedIdentityClientId = $managedIdentityClientId
    ManagedIdentityName = $managedIdentityName
    AppInsightsConnectionString = $appInsightsConnectionString
    DeployGenAI = $DeployGenAI.IsPresent
}

if ($DeployGenAI) {
    $context.OpenAIEndpoint = $openAIEndpoint
    $context.OpenAIModelName = $openAIModelName
    $context.SearchEndpoint = $searchEndpoint
}

$contextPath = Join-Path $repoRoot ".deployment-context.json"
$context | ConvertTo-Json -Depth 10 | Out-File -FilePath $contextPath -Encoding UTF8

Write-Host "Deployment context saved to: $contextPath" -ForegroundColor Green

# Summary
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Deployment Complete!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Next Steps:" -ForegroundColor Yellow
Write-Host "1. Run the application deployment script:"
Write-Host "   .\deploy-app\deploy.ps1"
Write-Host ""
Write-Host "Application URL: https://$webAppName.azurewebsites.net/Index"
Write-Host ""
