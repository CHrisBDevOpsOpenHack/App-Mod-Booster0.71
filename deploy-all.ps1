#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Unified deployment script that deploys both infrastructure and application.

.DESCRIPTION
    This script orchestrates the complete deployment by calling both infrastructure
    and application deployment scripts in sequence.

.PARAMETER ResourceGroup
    The name of the Azure resource group (required)

.PARAMETER Location
    The Azure region for deployment (required)

.PARAMETER BaseName
    Base name for resources (optional, defaults to 'expensemgmt')

.PARAMETER DeployGenAI
    Switch to deploy Azure OpenAI and AI Search resources

.EXAMPLE
    .\deploy-all.ps1 -ResourceGroup "rg-expensemgmt-20251207" -Location "uksouth"

.EXAMPLE
    .\deploy-all.ps1 -ResourceGroup "rg-expensemgmt-20251207" -Location "uksouth" -DeployGenAI
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
    [switch]$DeployGenAI
)

$ErrorActionPreference = "Stop"

Write-Host "============================================" -ForegroundColor Cyan
Write-Host "Expense Management - Full Deployment" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "This will deploy:" -ForegroundColor White
Write-Host "  1. Infrastructure (App Service, SQL, Monitoring" -ForegroundColor Gray
if ($DeployGenAI) {
    Write-Host "     + Azure OpenAI and AI Search)" -ForegroundColor Gray
}
else {
    Write-Host "     without GenAI)" -ForegroundColor Gray
}
Write-Host "  2. Application Code" -ForegroundColor Gray
Write-Host ""

# Validate scripts exist
$infraScript = Join-Path $PSScriptRoot "deploy-infra" "deploy.ps1"
$appScript = Join-Path $PSScriptRoot "deploy-app" "deploy.ps1"

if (-not (Test-Path $infraScript)) {
    Write-Error "Infrastructure deployment script not found: $infraScript"
    exit 1
}

if (-not (Test-Path $appScript)) {
    Write-Error "Application deployment script not found: $appScript"
    exit 1
}

# Step 1: Deploy infrastructure
Write-Host "============================================" -ForegroundColor Cyan
Write-Host "Step 1: Deploying Infrastructure" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""

$infraArgs = @{
    ResourceGroup = $ResourceGroup
    Location      = $Location
    BaseName      = $BaseName
}

if ($DeployGenAI) {
    $infraArgs["DeployGenAI"] = $true
}

try {
    & $infraScript @infraArgs
    
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Infrastructure deployment failed"
        exit 1
    }
}
catch {
    Write-Error "Infrastructure deployment failed: $_"
    Write-Host ""
    Write-Host "To retry infrastructure deployment only:" -ForegroundColor Yellow
    Write-Host "  .\deploy-infra\deploy.ps1 -ResourceGroup '$ResourceGroup' -Location '$Location'" -ForegroundColor Gray
    exit 1
}

# Step 2: Wait for resources to stabilize
Write-Host ""
Write-Host "Waiting for Azure resources to stabilize..." -ForegroundColor Yellow
Start-Sleep -Seconds 15
Write-Host "✓ Ready for application deployment" -ForegroundColor Green

# Step 3: Deploy application
Write-Host ""
Write-Host "============================================" -ForegroundColor Cyan
Write-Host "Step 2: Deploying Application" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""

try {
    & $appScript
    
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Application deployment failed"
        exit 1
    }
}
catch {
    Write-Error "Application deployment failed: $_"
    Write-Host ""
    Write-Host "Infrastructure is deployed. To retry application deployment only:" -ForegroundColor Yellow
    Write-Host "  .\deploy-app\deploy.ps1" -ForegroundColor Gray
    exit 1
}

# Final summary
Write-Host ""
Write-Host "============================================" -ForegroundColor Cyan
Write-Host "Full Deployment Complete!" -ForegroundColor Green
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "All resources have been deployed successfully." -ForegroundColor White
Write-Host ""

exit 0
