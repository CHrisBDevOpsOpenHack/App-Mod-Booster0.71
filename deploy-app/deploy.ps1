#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Deploys the application code to Azure App Service.

.DESCRIPTION
    This script builds and deploys the Expense Management application to Azure App Service.
    It automatically reads deployment context from the infrastructure deployment if available.

.PARAMETER ResourceGroup
    Optional: Azure resource group name (overrides context file).

.PARAMETER WebAppName
    Optional: Azure Web App name (overrides context file).

.PARAMETER SkipBuild
    Switch to skip the build step and use existing publish output.

.PARAMETER ConfigureSettings
    Switch to configure App Service settings after deployment.

.EXAMPLE
    .\deploy.ps1
    
.EXAMPLE
    .\deploy.ps1 -ResourceGroup "rg-expensemgmt-20251208" -WebAppName "app-expensemgmt-20251208"
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

$ErrorActionPreference = "Stop"

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "Application Deployment" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

# Try to read deployment context
$contextPath = "../.deployment-context.json"
$context = $null

if (Test-Path $contextPath) {
    Write-Host "Reading deployment context..." -ForegroundColor Yellow
    $context = Get-Content $contextPath | ConvertFrom-Json
    
    if (-not $ResourceGroup) {
        $ResourceGroup = $context.resourceGroup
    }
    if (-not $WebAppName) {
        $WebAppName = $context.webAppName
    }
    
    Write-Host "✓ Context loaded" -ForegroundColor Green
}

# Validate required parameters
if (-not $ResourceGroup -or -not $WebAppName) {
    Write-Error "ResourceGroup and WebAppName are required. Either run deploy-infra/deploy.ps1 first or provide these parameters."
    exit 1
}

Write-Host "Deployment target:" -ForegroundColor Yellow
Write-Host "  Resource Group: $ResourceGroup" -ForegroundColor White
Write-Host "  Web App: $WebAppName" -ForegroundColor White

# Check if Azure CLI is installed and logged in
Write-Host "`nChecking Azure CLI..." -ForegroundColor Yellow
try {
    $accountInfo = az account show 2>$null | ConvertFrom-Json
    if (-not $accountInfo) {
        Write-Error "Not logged in to Azure. Please run 'az login' first."
        exit 1
    }
    Write-Host "✓ Logged in as: $($accountInfo.user.name)" -ForegroundColor Green
}
catch {
    Write-Error "Azure CLI is not installed or not logged in. Please install from https://aka.ms/azure-cli"
    exit 1
}

# Build and publish the application
if (-not $SkipBuild) {
    Write-Host "`nBuilding application..." -ForegroundColor Yellow
    
    $projectPath = "../src/ExpenseManagement/ExpenseManagement.csproj"
    $publishPath = "../src/ExpenseManagement/bin/Release/net8.0/publish"
    
    if (-not (Test-Path $projectPath)) {
        Write-Error "Project file not found: $projectPath"
        exit 1
    }
    
    try {
        dotnet publish $projectPath `
            --configuration Release `
            --output $publishPath `
            --runtime linux-x64 `
            --self-contained false
        
        Write-Host "✓ Application built successfully" -ForegroundColor Green
    }
    catch {
        Write-Error "Build failed: $_"
        exit 1
    }
}
else {
    Write-Host "Skipping build (using existing publish output)" -ForegroundColor Yellow
}

# Create deployment package
Write-Host "`nCreating deployment package..." -ForegroundColor Yellow

$publishPath = "../src/ExpenseManagement/bin/Release/net8.0/publish"
$zipPath = "../deploy-app/app.zip"

if (Test-Path $zipPath) {
    Remove-Item $zipPath -Force
}

try {
    # Create zip with files at root level
    Compress-Archive -Path "$publishPath/*" -DestinationPath $zipPath -Force
    Write-Host "✓ Deployment package created" -ForegroundColor Green
}
catch {
    Write-Error "Failed to create deployment package: $_"
    exit 1
}

# Deploy to Azure App Service
Write-Host "`nDeploying to Azure App Service..." -ForegroundColor Yellow
Write-Host "This may take 2-3 minutes..." -ForegroundColor Gray

try {
    az webapp deploy `
        --resource-group $ResourceGroup `
        --name $WebAppName `
        --src-path $zipPath `
        --type zip `
        --clean true `
        --restart true `
        --output none
    
    Write-Host "✓ Application deployed successfully" -ForegroundColor Green
}
catch {
    Write-Error "Deployment failed: $_"
    exit 1
}
finally {
    # Clean up zip file
    if (Test-Path $zipPath) {
        Remove-Item $zipPath -Force
    }
}

# Configure settings if requested
if ($ConfigureSettings -and $context) {
    Write-Host "`nConfiguring App Service settings..." -ForegroundColor Yellow
    
    $connectionString = "Server=tcp:$($context.sqlServerFqdn),1433;Initial Catalog=$($context.databaseName);Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;Authentication=Active Directory Managed Identity;User Id=$($context.managedIdentityClientId);"
    
    az webapp config connection-string set `
        --resource-group $ResourceGroup `
        --name $WebAppName `
        --connection-string-type SQLAzure `
        --settings DefaultConnection="$connectionString" `
        --output none
    
    az webapp config appsettings set `
        --resource-group $ResourceGroup `
        --name $WebAppName `
        --settings "AZURE_CLIENT_ID=$($context.managedIdentityClientId)" "ManagedIdentityClientId=$($context.managedIdentityClientId)" `
        --output none
    
    Write-Host "✓ App Service settings configured" -ForegroundColor Green
}

# Wait for app to start
Write-Host "`nWaiting for application to start..." -ForegroundColor Yellow
Start-Sleep -Seconds 10

# Get the application URL
$webAppUrl = "https://$WebAppName.azurewebsites.net"

# Summary
Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "Deployment Complete!" -ForegroundColor Green
Write-Host "========================================`n" -ForegroundColor Cyan
Write-Host "Application URLs:" -ForegroundColor Yellow
Write-Host "  Dashboard: $webAppUrl/Index" -ForegroundColor White
Write-Host "  Chat: $webAppUrl/Chat" -ForegroundColor White
Write-Host "  API Docs: $webAppUrl/swagger" -ForegroundColor White
Write-Host ""
