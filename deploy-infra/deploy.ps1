#Requires -Version 7.0

<#
.SYNOPSIS
    Deploys the Expense Management application infrastructure to Azure.

.DESCRIPTION
    This script automates the complete infrastructure deployment including:
    - Azure App Service with managed identity
    - Azure SQL Database with Entra ID-only authentication
    - Application Insights and Log Analytics
    - Optional: Azure OpenAI and AI Search (with -DeployGenAI switch)

.PARAMETER ResourceGroup
    The name of the Azure resource group to create or use.

.PARAMETER Location
    The Azure region for resources. Default is 'uksouth'.

.PARAMETER BaseName
    Base name for resources. Default is 'expensemgmt'.

.PARAMETER DeployGenAI
    Switch to deploy Azure OpenAI and AI Search resources.

.PARAMETER SkipDatabaseSetup
    Switch to skip database schema import and stored procedure deployment.

.EXAMPLE
    .\deploy.ps1 -ResourceGroup "rg-expensemgmt-20241206" -Location "uksouth"

.EXAMPLE
    .\deploy.ps1 -ResourceGroup "rg-expensemgmt-20241206" -Location "uksouth" -DeployGenAI
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

# Set error action preference
$ErrorActionPreference = "Stop"

# Detect CI/CD environment
$IsCI = $env:GITHUB_ACTIONS -eq "true" -or $env:TF_BUILD -eq "true" -or $env:CI -eq "true"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Expense Management Infrastructure Deployment" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Check PowerShell version
if ($PSVersionTable.PSVersion.Major -lt 7) {
    Write-Warning "You are using PowerShell $($PSVersionTable.PSVersion). PowerShell 7+ is recommended for best compatibility."
    Write-Host "Download PowerShell 7: https://aka.ms/powershell-release" -ForegroundColor Yellow
    Write-Host ""
}

# Check Azure CLI
Write-Host "Checking prerequisites..." -ForegroundColor Yellow
try {
    $azVersion = az version --output json | ConvertFrom-Json
    Write-Host "✓ Azure CLI version: $($azVersion.'azure-cli')" -ForegroundColor Green
}
catch {
    Write-Error "Azure CLI is not installed. Please install it from: https://aka.ms/installazurecliwindows"
    exit 1
}

# Check if logged in to Azure
Write-Host "Checking Azure login status..." -ForegroundColor Yellow
try {
    $account = az account show --output json 2>$null | ConvertFrom-Json
    if ($null -eq $account) {
        throw "Not logged in"
    }
    Write-Host "✓ Logged in to Azure as: $($account.user.name)" -ForegroundColor Green
    Write-Host "  Subscription: $($account.name) ($($account.id))" -ForegroundColor Gray
}
catch {
    Write-Error "Not logged in to Azure. Please run 'az login' first."
    exit 1
}

# Get admin credentials based on environment
Write-Host ""
Write-Host "Retrieving administrator credentials..." -ForegroundColor Yellow

if ($IsCI) {
    Write-Host "Running in CI/CD mode" -ForegroundColor Cyan
    
    # In CI/CD, we use the Service Principal that authenticated via OIDC
    $servicePrincipalClientId = $env:AZURE_CLIENT_ID
    if ([string]::IsNullOrEmpty($servicePrincipalClientId)) {
        Write-Error "AZURE_CLIENT_ID environment variable not found. Ensure OIDC authentication is configured."
        exit 1
    }
    
    # Get Service Principal details
    $spDetails = az ad sp show --id $servicePrincipalClientId --output json | ConvertFrom-Json
    $adminObjectId = $spDetails.id
    $adminUsername = $spDetails.displayName
    $adminPrincipalType = "Application"
    
    Write-Host "✓ Using Service Principal: $adminUsername" -ForegroundColor Green
    Write-Host "  Object ID: $adminObjectId" -ForegroundColor Gray
}
else {
    Write-Host "Running in interactive mode" -ForegroundColor Cyan
    
    # Get current user details
    $userDetails = az ad signed-in-user show --output json | ConvertFrom-Json
    $adminObjectId = $userDetails.id
    $adminUsername = $userDetails.userPrincipalName
    $adminPrincipalType = "User"
    
    Write-Host "✓ Using current user: $adminUsername" -ForegroundColor Green
    Write-Host "  Object ID: $adminObjectId" -ForegroundColor Gray
}

# Create resource group
Write-Host ""
Write-Host "Creating resource group..." -ForegroundColor Yellow
$rgExists = az group exists --name $ResourceGroup
if ($rgExists -eq "true") {
    Write-Host "✓ Resource group '$ResourceGroup' already exists" -ForegroundColor Green
}
else {
    az group create --name $ResourceGroup --location $Location --output none
    Write-Host "✓ Resource group '$ResourceGroup' created" -ForegroundColor Green
}

# Deploy Bicep templates
Write-Host ""
Write-Host "Deploying infrastructure (this may take 5-10 minutes)..." -ForegroundColor Yellow

$deploymentName = "infra-deployment-$(Get-Date -Format 'yyyyMMddHHmmss')"

$deployParams = @(
    "--resource-group", $ResourceGroup,
    "--name", $deploymentName,
    "--template-file", "./deploy-infra/main.bicep",
    "--parameters", "location=$Location",
    "--parameters", "baseName=$BaseName",
    "--parameters", "adminObjectId=$adminObjectId",
    "--parameters", "adminUsername=$adminUsername",
    "--parameters", "adminPrincipalType=$adminPrincipalType",
    "--parameters", "deployGenAI=$($DeployGenAI.IsPresent.ToString().ToLower())"
)

try {
    az deployment group create @deployParams --output none
    Write-Host "✓ Infrastructure deployment completed" -ForegroundColor Green
}
catch {
    Write-Error "Infrastructure deployment failed: $_"
    exit 1
}

# Get deployment outputs
Write-Host ""
Write-Host "Retrieving deployment outputs..." -ForegroundColor Yellow

$outputs = az deployment group show --resource-group $ResourceGroup --name $deploymentName --query "properties.outputs" --output json | ConvertFrom-Json

$webAppName = $outputs.webAppName.value
$sqlServerFqdn = $outputs.sqlServerFqdn.value
$databaseName = $outputs.databaseName.value
$managedIdentityName = $outputs.managedIdentityName.value
$managedIdentityClientId = $outputs.managedIdentityClientId.value
$appInsightsConnectionString = $outputs.appInsightsConnectionString.value

Write-Host "✓ Web App: $webAppName" -ForegroundColor Green
Write-Host "✓ SQL Server: $sqlServerFqdn" -ForegroundColor Green
Write-Host "✓ Managed Identity: $managedIdentityName" -ForegroundColor Green

# Database setup
if (-not $SkipDatabaseSetup) {
    Write-Host ""
    Write-Host "Setting up database..." -ForegroundColor Yellow
    
    # Check sqlcmd installation
    try {
        $sqlcmdVersion = sqlcmd --version 2>&1
        Write-Host "✓ sqlcmd found: $sqlcmdVersion" -ForegroundColor Green
    }
    catch {
        Write-Error "sqlcmd (go-sqlcmd) is not installed. Install it with: winget install sqlcmd"
        Write-Host "Or download from: https://github.com/microsoft/go-sqlcmd/releases" -ForegroundColor Yellow
        exit 1
    }
    
    # Add current IP to firewall (for interactive mode)
    if (-not $IsCI) {
        Write-Host "Adding your IP address to SQL Server firewall..." -ForegroundColor Yellow
        $myIp = (Invoke-WebRequest -Uri "https://api.ipify.org" -UseBasicParsing).Content.Trim()
        az sql server firewall-rule create `
            --resource-group $ResourceGroup `
            --server ($sqlServerFqdn -replace '\.database\.windows\.net$', '') `
            --name "AllowMyIP" `
            --start-ip-address $myIp `
            --end-ip-address $myIp `
            --output none
        Write-Host "✓ Firewall rule added for IP: $myIp" -ForegroundColor Green
    }
    
    # Wait for SQL Server to be ready
    Write-Host "Waiting for SQL Server to be ready..." -ForegroundColor Yellow
    Start-Sleep -Seconds 30
    
    # Determine authentication method for sqlcmd
    $authMethod = if ($IsCI) { "ActiveDirectoryAzCli" } else { "ActiveDirectoryDefault" }
    
    # Import database schema
    Write-Host "Importing database schema..." -ForegroundColor Yellow
    $schemaFile = "./Database-Schema/database_schema.sql"
    
    if (-not (Test-Path $schemaFile)) {
        Write-Error "Schema file not found: $schemaFile"
        exit 1
    }
    
    try {
        sqlcmd -S $sqlServerFqdn -d $databaseName "--authentication-method=$authMethod" -i $schemaFile
        Write-Host "✓ Database schema imported" -ForegroundColor Green
    }
    catch {
        Write-Error "Failed to import database schema: $_"
        exit 1
    }
    
    # Create managed identity database user using SID-based approach
    Write-Host "Creating database user for managed identity..." -ForegroundColor Yellow
    
    # Convert Client ID (GUID) to SID hex format
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
    
    try {
        $createUserSql | sqlcmd -S $sqlServerFqdn -d $databaseName "--authentication-method=$authMethod"
        Write-Host "✓ Managed identity database user created" -ForegroundColor Green
    }
    catch {
        Write-Error "Failed to create managed identity database user: $_"
        exit 1
    }
    
    # Import stored procedures
    Write-Host "Importing stored procedures..." -ForegroundColor Yellow
    $storedProcFile = "./stored-procedures.sql"
    
    if (-not (Test-Path $storedProcFile)) {
        Write-Error "Stored procedures file not found: $storedProcFile"
        exit 1
    }
    
    try {
        sqlcmd -S $sqlServerFqdn -d $databaseName "--authentication-method=$authMethod" -i $storedProcFile
        Write-Host "✓ Stored procedures imported" -ForegroundColor Green
    }
    catch {
        Write-Error "Failed to import stored procedures: $_"
        exit 1
    }
}
else {
    Write-Host ""
    Write-Host "Skipping database setup (SkipDatabaseSetup flag set)" -ForegroundColor Yellow
}

# Configure App Service settings
Write-Host ""
Write-Host "Configuring App Service settings..." -ForegroundColor Yellow

$connectionString = "Server=tcp:$sqlServerFqdn,1433;Initial Catalog=$databaseName;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;Authentication=Active Directory Managed Identity;User Id=$managedIdentityClientId;"

az webapp config connection-string set `
    --resource-group $ResourceGroup `
    --name $webAppName `
    --connection-string-type SQLAzure `
    --settings DefaultConnection="$connectionString" `
    --output none

Write-Host "✓ Connection string configured" -ForegroundColor Green

# Configure GenAI settings if deployed
if ($DeployGenAI) {
    Write-Host "Configuring GenAI settings..." -ForegroundColor Yellow
    
    $openAIEndpoint = $outputs.openAIEndpoint.value
    $openAIModelName = $outputs.openAIModelName.value
    $searchEndpoint = $outputs.searchEndpoint.value
    
    az webapp config appsettings set `
        --resource-group $ResourceGroup `
        --name $webAppName `
        --settings `
            "OpenAI__Endpoint=$openAIEndpoint" `
            "OpenAI__DeploymentName=$openAIModelName" `
            "AzureSearch__Endpoint=$searchEndpoint" `
        --output none
    
    Write-Host "✓ GenAI settings configured" -ForegroundColor Green
}

# Save deployment context
Write-Host ""
Write-Host "Saving deployment context..." -ForegroundColor Yellow

$contextFile = "./.deployment-context.json"
$context = @{
    resourceGroup = $ResourceGroup
    location = $Location
    webAppName = $webAppName
    sqlServerFqdn = $sqlServerFqdn
    databaseName = $databaseName
    managedIdentityClientId = $managedIdentityClientId
    deployedGenAI = $DeployGenAI.IsPresent
    deploymentDate = (Get-Date).ToString("yyyy-MM-dd HH:mm:ss")
}

$context | ConvertTo-Json | Out-File -FilePath $contextFile -Encoding UTF8
Write-Host "✓ Deployment context saved to: $contextFile" -ForegroundColor Green

# Summary
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Deployment Complete!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Resource Group: $ResourceGroup" -ForegroundColor White
Write-Host "Web App Name: $webAppName" -ForegroundColor White
Write-Host "Web App URL: https://$($outputs.webAppHostName.value)" -ForegroundColor White
Write-Host "SQL Server: $sqlServerFqdn" -ForegroundColor White
Write-Host "Database: $databaseName" -ForegroundColor White
Write-Host "Managed Identity: $managedIdentityName" -ForegroundColor White

if ($DeployGenAI) {
    Write-Host ""
    Write-Host "GenAI Resources:" -ForegroundColor Yellow
    Write-Host "Azure OpenAI: $($outputs.openAIName.value)" -ForegroundColor White
    Write-Host "AI Search: $($outputs.searchName.value)" -ForegroundColor White
}

Write-Host ""
Write-Host "Next Steps:" -ForegroundColor Yellow
Write-Host "1. Deploy application code: .\deploy-app\deploy.ps1" -ForegroundColor White
Write-Host "2. View the app at: https://$($outputs.webAppHostName.value)/Index" -ForegroundColor White
Write-Host ""
