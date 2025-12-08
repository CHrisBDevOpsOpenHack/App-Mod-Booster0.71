#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Unified deployment script for infrastructure and application.

.DESCRIPTION
    This script orchestrates both infrastructure and application deployment with a single command.

.PARAMETER ResourceGroup
    Name of the Azure resource group to create or use (required).

.PARAMETER Location
    Azure region for the resources (required, e.g., 'uksouth', 'eastus').

.PARAMETER BaseName
    Base name for resources. Defaults to 'expensemgmt'.

.PARAMETER DeployGenAI
    Switch to deploy GenAI resources (Azure OpenAI and AI Search).

.EXAMPLE
    .\deploy-all.ps1 -ResourceGroup "rg-expensemgmt-20251208" -Location "uksouth"
    
.EXAMPLE
    .\deploy-all.ps1 -ResourceGroup "rg-expensemgmt-20251208" -Location "uksouth" -DeployGenAI
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

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "Full Deployment" -ForegroundColor Cyan
Write-Host "Infrastructure + Application" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

# Validate deployment scripts exist
$infraScript = "./deploy-infra/deploy.ps1"
$appScript = "./deploy-app/deploy.ps1"

if (-not (Test-Path $infraScript)) {
    Write-Error "Infrastructure deployment script not found: $infraScript"
    exit 1
}

if (-not (Test-Path $appScript)) {
    Write-Error "Application deployment script not found: $appScript"
    exit 1
}

Write-Host "✓ Deployment scripts validated" -ForegroundColor Green

# Phase 1: Infrastructure Deployment
Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "PHASE 1: Infrastructure Deployment" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

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
        Write-Error "Infrastructure deployment failed with exit code $LASTEXITCODE"
        exit $LASTEXITCODE
    }
}
catch {
    Write-Error "Infrastructure deployment failed: $_"
    Write-Host "`nTo retry, run: .\deploy-infra\deploy.ps1 -ResourceGroup '$ResourceGroup' -Location '$Location'" -ForegroundColor Yellow
    exit 1
}

# Wait for Azure resources to stabilize
Write-Host "`nWaiting for Azure resources to stabilize..." -ForegroundColor Yellow
Start-Sleep -Seconds 15

# Phase 2: Application Deployment
Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "PHASE 2: Application Deployment" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

try {
    & $appScript
    
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Application deployment failed with exit code $LASTEXITCODE"
        exit $LASTEXITCODE
    }
}
catch {
    Write-Error "Application deployment failed: $_"
    Write-Host "`nTo retry, run: .\deploy-app\deploy.ps1" -ForegroundColor Yellow
    exit 1
}

# Load deployment context for final summary
$contextPath = "./.deployment-context.json"
if (Test-Path $contextPath) {
    $context = Get-Content $contextPath | ConvertFrom-Json
    $webAppUrl = "https://$($context.webAppName).azurewebsites.net"
    
    # Final Summary
    Write-Host "`n========================================" -ForegroundColor Cyan
    Write-Host "Deployment Complete!" -ForegroundColor Green
    Write-Host "========================================`n" -ForegroundColor Cyan
    Write-Host "Resource Group: $ResourceGroup" -ForegroundColor White
    Write-Host "Web App: $($context.webAppName)" -ForegroundColor White
    Write-Host "SQL Server: $($context.sqlServerFqdn)" -ForegroundColor White
    
    if ($context.genAIEnabled) {
        Write-Host "GenAI: Enabled ✓" -ForegroundColor Green
    }
    else {
        Write-Host "GenAI: Not deployed (use -DeployGenAI to enable)" -ForegroundColor Yellow
    }
    
    Write-Host "`nApplication URLs:" -ForegroundColor Cyan
    Write-Host "  Dashboard: $webAppUrl/Index" -ForegroundColor White
    Write-Host "  Chat: $webAppUrl/Chat" -ForegroundColor White
    Write-Host "  API Docs: $webAppUrl/swagger" -ForegroundColor White
    Write-Host ""
}
else {
    Write-Warning "Deployment context file not found. Cannot display full summary."
}

Write-Host "Deployment completed successfully!" -ForegroundColor Green
Write-Host ""
