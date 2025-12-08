<#
.SYNOPSIS
    Unified deployment script that deploys both infrastructure and application.

.DESCRIPTION
    This script orchestrates the complete deployment process by calling the
    infrastructure and application deployment scripts in sequence.

.PARAMETER ResourceGroup
    Name of the Azure resource group (required)

.PARAMETER Location
    Azure region for deployment (required)

.PARAMETER BaseName
    Base name for resources (optional, defaults to 'expensemgmt')

.PARAMETER DeployGenAI
    Switch to deploy Azure OpenAI and AI Search resources

.EXAMPLE
    .\deploy-all.ps1 -ResourceGroup "rg-expensemgmt-20251208" -Location "uksouth"

.EXAMPLE
    .\deploy-all.ps1 -ResourceGroup "rg-expensemgmt-20251208" -Location "uksouth" -DeployGenAI
#>

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

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Full Deployment - Infrastructure & App" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Resource Group: $ResourceGroup"
Write-Host "Location: $Location"
Write-Host "Base Name: $BaseName"
Write-Host "Deploy GenAI: $DeployGenAI"
Write-Host ""

$scriptDir = $PSScriptRoot
$infraScript = Join-Path $scriptDir "deploy-infra/deploy.ps1"
$appScript = Join-Path $scriptDir "deploy-app/deploy.ps1"

# Validate scripts exist
if (-not (Test-Path $infraScript)) {
    Write-Host "Error: Infrastructure script not found at $infraScript" -ForegroundColor Red
    exit 1
}

if (-not (Test-Path $appScript)) {
    Write-Host "Error: Application script not found at $appScript" -ForegroundColor Red
    exit 1
}

# Run infrastructure deployment
Write-Host ""
Write-Host "========================================" -ForegroundColor Yellow
Write-Host "Phase 1: Infrastructure Deployment" -ForegroundColor Yellow
Write-Host "========================================" -ForegroundColor Yellow
Write-Host ""

$infraArgs = @{
    ResourceGroup = $ResourceGroup
    Location = $Location
    BaseName = $BaseName
}

if ($DeployGenAI) {
    $infraArgs["DeployGenAI"] = $true
}

& $infraScript @infraArgs

if ($LASTEXITCODE -ne 0) {
    Write-Host ""
    Write-Host "Error: Infrastructure deployment failed." -ForegroundColor Red
    Write-Host "To retry, run: .\deploy-infra\deploy.ps1 -ResourceGroup `"$ResourceGroup`" -Location `"$Location`"" -ForegroundColor Yellow
    exit 1
}

Write-Host ""
Write-Host "Infrastructure deployment completed!" -ForegroundColor Green

# Wait for resources to stabilize
Write-Host ""
Write-Host "Waiting for Azure resources to stabilize..." -ForegroundColor Yellow
Start-Sleep -Seconds 15

# Run application deployment
Write-Host ""
Write-Host "========================================" -ForegroundColor Yellow
Write-Host "Phase 2: Application Deployment" -ForegroundColor Yellow
Write-Host "========================================" -ForegroundColor Yellow
Write-Host ""

& $appScript

if ($LASTEXITCODE -ne 0) {
    Write-Host ""
    Write-Host "Error: Application deployment failed." -ForegroundColor Red
    Write-Host "To retry, run: .\deploy-app\deploy.ps1" -ForegroundColor Yellow
    exit 1
}

# Final summary
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Full Deployment Complete!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Read context for URL
$contextPath = Join-Path $scriptDir ".deployment-context.json"
if (Test-Path $contextPath) {
    $context = Get-Content $contextPath | ConvertFrom-Json
    Write-Host "Application URL: https://$($context.WebAppName).azurewebsites.net/Index" -ForegroundColor Green
    Write-Host "Swagger API Docs: https://$($context.WebAppName).azurewebsites.net/swagger" -ForegroundColor Green
}

Write-Host ""
