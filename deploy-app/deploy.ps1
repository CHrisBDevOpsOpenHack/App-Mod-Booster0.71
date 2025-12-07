#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Deploys the application code to Azure App Service.

.DESCRIPTION
    This script automates deployment of the .NET application to Azure App Service.
    It reads deployment context from the infrastructure deployment script for seamless operation.

.PARAMETER ResourceGroup
    The name of the Azure resource group (optional if context file exists)

.PARAMETER WebAppName
    The name of the Azure Web App (optional if context file exists)

.PARAMETER SkipBuild
    Skip the dotnet build and publish step (for redeployments)

.EXAMPLE
    .\deploy.ps1

.EXAMPLE
    .\deploy.ps1 -ResourceGroup "rg-expensemgmt-20251207" -WebAppName "app-expensemgmt-abc123"
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [string]$ResourceGroup,
    
    [Parameter(Mandatory = $false)]
    [string]$WebAppName,
    
    [Parameter(Mandatory = $false)]
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"

Write-Host "============================================" -ForegroundColor Cyan
Write-Host "Expense Management - Application Deployment" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""

# Check for deployment context file
$contextFile = Join-Path $PSScriptRoot ".." ".deployment-context.json"
$context = $null

if (Test-Path $contextFile) {
    Write-Host "Loading deployment context..." -ForegroundColor Yellow
    $context = Get-Content $contextFile | ConvertFrom-Json
    
    if ([string]::IsNullOrEmpty($ResourceGroup)) {
        $ResourceGroup = $context.resourceGroup
    }
    if ([string]::IsNullOrEmpty($WebAppName)) {
        $WebAppName = $context.webAppName
    }
    
    Write-Host "✓ Context loaded" -ForegroundColor Green
}

# Validate parameters
if ([string]::IsNullOrEmpty($ResourceGroup) -or [string]::IsNullOrEmpty($WebAppName)) {
    Write-Error "Resource group and web app name are required. Either run deploy-infra/deploy.ps1 first or provide parameters."
    exit 1
}

Write-Host "Target:" -ForegroundColor Cyan
Write-Host "  Resource Group: $ResourceGroup" -ForegroundColor Gray
Write-Host "  Web App: $WebAppName" -ForegroundColor Gray
Write-Host ""

# Step 1: Check Azure CLI
Write-Host "Checking Azure CLI..." -ForegroundColor Yellow
try {
    $azVersion = az version --output json | ConvertFrom-Json
    Write-Host "✓ Azure CLI version $($azVersion.'azure-cli') found" -ForegroundColor Green
}
catch {
    Write-Error "Azure CLI is not installed. Download from: https://aka.ms/installazurecliwindows"
    exit 1
}

# Step 2: Check login status
Write-Host "Checking Azure login status..." -ForegroundColor Yellow
try {
    $account = az account show --output json | ConvertFrom-Json
    Write-Host "✓ Logged in as $($account.user.name)" -ForegroundColor Green
}
catch {
    Write-Error "Not logged in to Azure. Run 'az login' first."
    exit 1
}

# Step 3: Build and publish application
if (-not $SkipBuild) {
    Write-Host ""
    Write-Host "Building application..." -ForegroundColor Yellow
    
    $projectPath = Join-Path $PSScriptRoot ".." "src" "ExpenseManagement" "ExpenseManagement.csproj"
    $publishPath = Join-Path $PSScriptRoot "publish"
    
    if (Test-Path $publishPath) {
        Remove-Item -Path $publishPath -Recurse -Force
    }
    
    try {
        dotnet publish $projectPath `
            --configuration Release `
            --output $publishPath `
            --nologo `
            --verbosity quiet
        
        Write-Host "✓ Application built and published" -ForegroundColor Green
    }
    catch {
        Write-Error "Build failed: $_"
        exit 1
    }
}
else {
    Write-Host "Skipping build (using existing publish folder)" -ForegroundColor Yellow
    $publishPath = Join-Path $PSScriptRoot "publish"
}

# Step 4: Create deployment package
Write-Host ""
Write-Host "Creating deployment package..." -ForegroundColor Yellow

$zipPath = Join-Path $PSScriptRoot "app.zip"
if (Test-Path $zipPath) {
    Remove-Item -Path $zipPath -Force
}

try {
    Compress-Archive -Path "$publishPath\*" -DestinationPath $zipPath -Force
    Write-Host "✓ Deployment package created" -ForegroundColor Green
}
catch {
    Write-Error "Failed to create deployment package: $_"
    exit 1
}

# Step 5: Deploy to Azure App Service
Write-Host ""
Write-Host "Deploying to Azure App Service..." -ForegroundColor Yellow
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
    # Clean up
    if (Test-Path $zipPath) {
        Remove-Item -Path $zipPath -Force
    }
}

# Step 6: Get application URL
Write-Host ""
Write-Host "Retrieving application URL..." -ForegroundColor Yellow

try {
    $appDetails = az webapp show `
        --resource-group $ResourceGroup `
        --name $WebAppName `
        --output json | ConvertFrom-Json
    
    $appUrl = "https://$($appDetails.defaultHostName)"
    
    Write-Host "✓ Application is available" -ForegroundColor Green
}
catch {
    Write-Warning "Could not retrieve application URL"
    $appUrl = "https://$WebAppName.azurewebsites.net"
}

# Summary
Write-Host ""
Write-Host "============================================" -ForegroundColor Cyan
Write-Host "Deployment Complete!" -ForegroundColor Green
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Application URLs:" -ForegroundColor White
Write-Host "  Main App:     $appUrl/Index" -ForegroundColor Gray
Write-Host "  AI Chat:      $appUrl/Chat" -ForegroundColor Gray
Write-Host "  API Docs:     $appUrl/swagger" -ForegroundColor Gray
Write-Host ""
Write-Host "Note: The application may take 30-60 seconds to start on first access." -ForegroundColor Yellow
Write-Host ""

exit 0
