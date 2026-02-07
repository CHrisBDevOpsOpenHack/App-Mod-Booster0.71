<#
.SYNOPSIS
    Unified deployment script - deploys both infrastructure and application.

.DESCRIPTION
    This script orchestrates the complete deployment by calling:
    1. deploy-infra/deploy.ps1 - Infrastructure deployment
    2. deploy-app/deploy.ps1 - Application deployment
    
    Provides a single command to deploy everything from scratch.

.PARAMETER ResourceGroup
    Name of the Azure resource group (required). Use fresh names with timestamps.

.PARAMETER Location
    Azure region for deployment (required). Example: 'uksouth', 'eastus'

.PARAMETER BaseName
    Base name for resource naming (optional). Defaults to 'expensemgmt'.

.PARAMETER DeployGenAI
    Switch to deploy GenAI resources (Azure OpenAI, AI Search).

.EXAMPLE
    .\deploy-all.ps1 -ResourceGroup "rg-expensemgmt-20260207" -Location "uksouth"
    
.EXAMPLE
    .\deploy-all.ps1 -ResourceGroup "rg-expensemgmt-20260207" -Location "uksouth" -DeployGenAI
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
Write-Host "========================================" -ForegroundColor Magenta
Write-Host " Unified Deployment - Full Stack" -ForegroundColor Magenta
Write-Host "========================================" -ForegroundColor Magenta
Write-Host ""
Write-Host "This will deploy:" -ForegroundColor Yellow
Write-Host "  1. Infrastructure (App Service, SQL, Monitoring)" -ForegroundColor Gray
if ($DeployGenAI) {
    Write-Host "  2. GenAI Resources (Azure OpenAI, AI Search)" -ForegroundColor Gray
}
Write-Host "  3. Application Code (.NET 8 Razor Pages)" -ForegroundColor Gray
Write-Host ""

# Validate deployment scripts exist
$scriptDir = $PSScriptRoot
$infraScript = Join-Path $scriptDir "deploy-infra/deploy.ps1"
$appScript = Join-Path $scriptDir "deploy-app/deploy.ps1"

if (-not (Test-Path $infraScript)) {
    Write-Error "Infrastructure deployment script not found: $infraScript"
    exit 1
}

if (-not (Test-Path $appScript)) {
    Write-Error "Application deployment script not found: $appScript"
    exit 1
}

Write-Host "✓ Deployment scripts validated" -ForegroundColor Green
Write-Host ""

# Step 1: Deploy infrastructure
Write-Host "================================================" -ForegroundColor Cyan
Write-Host " Step 1/2: Deploying Infrastructure" -ForegroundColor Cyan
Write-Host "================================================" -ForegroundColor Cyan
Write-Host ""

# Build infrastructure arguments using hashtable splatting
$infraArgs = @{
    ResourceGroup = $ResourceGroup
    Location      = $Location
    BaseName      = $BaseName
}

if ($DeployGenAI) {
    $infraArgs["DeployGenAI"] = $true
}

# Call infrastructure deployment script
& $infraScript @infraArgs

if ($LASTEXITCODE -ne 0) {
    Write-Error "Infrastructure deployment failed. Stopping deployment."
    exit 1
}

Write-Host ""
Write-Host "✓ Infrastructure deployment succeeded" -ForegroundColor Green

# Wait for resources to stabilize
Write-Host ""
Write-Host "Waiting for Azure resources to stabilize..." -ForegroundColor Yellow
Start-Sleep -Seconds 15
Write-Host "✓ Resources ready" -ForegroundColor Green

# Step 2: Deploy application
Write-Host ""
Write-Host "================================================" -ForegroundColor Cyan
Write-Host " Step 2/2: Deploying Application" -ForegroundColor Cyan
Write-Host "================================================" -ForegroundColor Cyan
Write-Host ""

# Call application deployment script (reads context file automatically)
& $appScript

if ($LASTEXITCODE -ne 0) {
    Write-Error "Application deployment failed. Infrastructure is deployed but app deployment needs retry."
    Write-Host ""
    Write-Host "To retry application deployment only:" -ForegroundColor Yellow
    Write-Host "  .\deploy-app\deploy.ps1" -ForegroundColor Gray
    exit 1
}

Write-Host ""
Write-Host "✓ Application deployment succeeded" -ForegroundColor Green

# Display final summary
Write-Host ""
Write-Host "========================================" -ForegroundColor Magenta
Write-Host " Deployment Complete!" -ForegroundColor Magenta
Write-Host "========================================" -ForegroundColor Magenta
Write-Host ""
Write-Host "Resource Group: $ResourceGroup" -ForegroundColor Cyan
Write-Host "Location:       $Location" -ForegroundColor Cyan
Write-Host ""
Write-Host "The application is now running in Azure!" -ForegroundColor Green
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "  • Visit the application URLs displayed above" -ForegroundColor Gray
Write-Host "  • Check Application Insights for telemetry" -ForegroundColor Gray
Write-Host "  • Review Azure Portal for resource details" -ForegroundColor Gray
Write-Host ""
