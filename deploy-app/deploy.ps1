#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Deploys the Expense Management application code to Azure App Service.

.DESCRIPTION
    This script builds and deploys the .NET application to Azure App Service.
    It reads deployment context from the infrastructure deployment automatically,
    so no parameters are required if you've run deploy-infra/deploy.ps1 first.

.PARAMETER ResourceGroup
    Optional. The Azure resource group name. If not provided, reads from deployment context.

.PARAMETER WebAppName
    Optional. The App Service name. If not provided, reads from deployment context.

.PARAMETER SkipBuild
    Optional switch. Skip the build step (useful for redeployments).

.PARAMETER ConfigureSettings
    Optional switch. Configure App Service settings after deployment.

.EXAMPLE
    .\deploy.ps1
    
    Deploys using the deployment context file created by infrastructure deployment.

.EXAMPLE
    .\deploy.ps1 -ResourceGroup "rg-expensemgmt-20251207" -WebAppName "app-expensemgmt-abc123"
    
    Deploys to a specific resource group and web app.

.EXAMPLE
    .\deploy.ps1 -SkipBuild
    
    Redeploys without rebuilding (faster for testing deployment process).
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [string]$ResourceGroup,
    
    [Parameter(Mandatory = $false)]
    [string]$WebAppName,
    
    [Parameter(Mandatory = $false)]
    [switch]$SkipBuild,
    
    [Parameter(Mandatory = $false)]
    [switch]$ConfigureSettings
)

$ErrorActionPreference = 'Stop'

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Expense Management - App Deployment   " -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Try to load deployment context
$contextFile = "./.deployment-context.json"
$context = $null

if (Test-Path $contextFile) {
    Write-Host "Loading deployment context..." -ForegroundColor Cyan
    $context = Get-Content $contextFile | ConvertFrom-Json
    
    # Use context values if parameters not provided
    if ([string]::IsNullOrEmpty($ResourceGroup)) {
        $ResourceGroup = $context.resourceGroup
    }
    if ([string]::IsNullOrEmpty($WebAppName)) {
        $WebAppName = $context.webAppName
    }
    
    Write-Host "✓ Loaded context from: $contextFile" -ForegroundColor Green
} else {
    Write-Host "⚠ No deployment context file found" -ForegroundColor Yellow
    Write-Host "  Run deploy-infra/deploy.ps1 first or provide parameters manually" -ForegroundColor Yellow
}

# Validate required parameters
if ([string]::IsNullOrEmpty($ResourceGroup) -or [string]::IsNullOrEmpty($WebAppName)) {
    Write-Error "ResourceGroup and WebAppName are required. Either run deploy-infra/deploy.ps1 first or provide them as parameters."
    exit 1
}

Write-Host ""
Write-Host "Deployment target:" -ForegroundColor Cyan
Write-Host "  Resource Group: $ResourceGroup" -ForegroundColor White
Write-Host "  Web App: $WebAppName" -ForegroundColor White

# Check Azure CLI is installed
Write-Host ""
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
} catch {
    Write-Error "Not logged in to Azure. Please run 'az login' first."
    exit 1
}

# Verify App Service exists
Write-Host ""
Write-Host "Verifying App Service exists..." -ForegroundColor Cyan
$appExists = az webapp show --name $WebAppName --resource-group $ResourceGroup 2>$null
if (-not $appExists) {
    Write-Error "App Service '$WebAppName' not found in resource group '$ResourceGroup'"
    exit 1
}
Write-Host "✓ App Service found" -ForegroundColor Green

$projectPath = "./src/ExpenseManagement"
$publishPath = "./src/ExpenseManagement/bin/Release/net8.0/publish"
$zipPath = "./deploy-app/app-package.zip"

if (-not $SkipBuild) {
    # Build and publish the application
    Write-Host ""
    Write-Host "Building .NET application..." -ForegroundColor Cyan
    
    if (-not (Test-Path $projectPath)) {
        Write-Error "Project not found at: $projectPath"
        exit 1
    }
    
    Push-Location $projectPath
    try {
        # Clean previous build
        dotnet clean --configuration Release --nologo --verbosity quiet
        
        # Build and publish
        dotnet publish --configuration Release --output "./bin/Release/net8.0/publish" --nologo
        
        if ($LASTEXITCODE -ne 0) {
            Write-Error "Build failed"
            exit 1
        }
        
        Write-Host "✓ Application built successfully" -ForegroundColor Green
    }
    finally {
        Pop-Location
    }
} else {
    Write-Host ""
    Write-Host "Skipping build (using existing artifacts)" -ForegroundColor Yellow
}

# Create deployment package
Write-Host ""
Write-Host "Creating deployment package..." -ForegroundColor Cyan

if (-not (Test-Path $publishPath)) {
    Write-Error "Publish directory not found: $publishPath. Run without -SkipBuild first."
    exit 1
}

# Remove old zip if exists
if (Test-Path $zipPath) {
    Remove-Item $zipPath -Force
}

# Create zip with files at root level (not in subdirectory)
Compress-Archive -Path "$publishPath/*" -DestinationPath $zipPath -Force

$zipSize = (Get-Item $zipPath).Length / 1MB
Write-Host "✓ Package created: $zipPath ($([math]::Round($zipSize, 2)) MB)" -ForegroundColor Green

# Deploy to Azure App Service
Write-Host ""
Write-Host "Deploying to Azure App Service..." -ForegroundColor Cyan
Write-Host "This may take 2-3 minutes..." -ForegroundColor Yellow

az webapp deploy `
    --resource-group $ResourceGroup `
    --name $WebAppName `
    --src-path $zipPath `
    --type zip `
    --clean true `
    --restart true `
    --timeout 300 `
    --output none

if ($LASTEXITCODE -ne 0) {
    Write-Error "Deployment failed"
    exit 1
}

Write-Host "✓ Application deployed successfully" -ForegroundColor Green

# Clean up deployment package
Write-Host ""
Write-Host "Cleaning up..." -ForegroundColor Cyan
Remove-Item $zipPath -Force -ErrorAction SilentlyContinue
Write-Host "✓ Temporary files removed" -ForegroundColor Green

# Configure settings if requested
if ($ConfigureSettings -and $context) {
    Write-Host ""
    Write-Host "Configuring App Service settings..." -ForegroundColor Cyan
    
    if ($context.managedIdentityClientId -and $context.sqlServerFqdn) {
        $connectionString = "Server=tcp:$($context.sqlServerFqdn),1433;Initial Catalog=$($context.sqlDatabaseName);Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;Authentication=Active Directory Managed Identity;User Id=$($context.managedIdentityClientId);"
        
        az webapp config connection-string set `
            --name $WebAppName `
            --resource-group $ResourceGroup `
            --connection-string-type SQLAzure `
            --settings DefaultConnection="$connectionString" `
            --output none
        
        az webapp config appsettings set `
            --name $WebAppName `
            --resource-group $ResourceGroup `
            --settings `
                "AZURE_CLIENT_ID=$($context.managedIdentityClientId)" `
                "ManagedIdentityClientId=$($context.managedIdentityClientId)" `
            --output none
        
        Write-Host "✓ Settings configured" -ForegroundColor Green
    }
}

# Get App Service URL
$appUrl = az webapp show --name $WebAppName --resource-group $ResourceGroup --query "defaultHostName" -o tsv

# Summary
Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "  Application Deployment Complete!      " -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
Write-Host "Access your application at:" -ForegroundColor Cyan
Write-Host "  Main App: https://$appUrl/Index" -ForegroundColor Yellow
Write-Host "  API Docs: https://$appUrl/swagger" -ForegroundColor Yellow

if ($context -and $context.openAIEndpoint) {
    Write-Host "  AI Chat:  https://$appUrl/Chat" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "Note: The application may take 1-2 minutes to fully start." -ForegroundColor White
Write-Host ""

exit 0
