#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Unified deployment script that deploys both infrastructure and application.

.DESCRIPTION
    This script orchestrates the complete deployment process by calling both the
    infrastructure deployment script and the application deployment script in sequence.
    
    This provides a single-command deployment experience while maintaining the
    modularity of separate infrastructure and application deployment scripts.

.PARAMETER ResourceGroup
    The name of the Azure resource group to create/use. Should be unique (include date/time).

.PARAMETER Location
    The Azure region where resources will be deployed (e.g., 'uksouth', 'eastus').

.PARAMETER BaseName
    The base name for all resources. Defaults to 'expensemgmt'.

.PARAMETER DeployGenAI
    Switch to deploy GenAI resources (Azure OpenAI and AI Search).

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
    [string]$BaseName = 'expensemgmt',
    
    [Parameter(Mandatory = $false)]
    [switch]$DeployGenAI
)

$ErrorActionPreference = 'Stop'

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Expense Management - Full Deployment  " -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "This script will deploy:" -ForegroundColor White
Write-Host "  1. Infrastructure (Azure resources)" -ForegroundColor White
Write-Host "  2. Application (code deployment)" -ForegroundColor White
Write-Host ""

# Verify both deployment scripts exist
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

Write-Host "✓ Both deployment scripts found" -ForegroundColor Green
Write-Host ""

# ========================================
# Phase 1: Infrastructure Deployment
# ========================================

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Phase 1: Infrastructure Deployment    " -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Build parameters for infrastructure script using hashtable
$infraArgs = @{
    ResourceGroup = $ResourceGroup
    Location = $Location
    BaseName = $BaseName
}

# Add DeployGenAI switch if specified
if ($DeployGenAI) {
    $infraArgs["DeployGenAI"] = $true
}

# Call infrastructure deployment script
Write-Host "Calling: $infraScript" -ForegroundColor Yellow
Write-Host ""

try {
    & $infraScript @infraArgs
    
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Infrastructure deployment failed with exit code: $LASTEXITCODE"
        exit $LASTEXITCODE
    }
}
catch {
    Write-Error "Infrastructure deployment failed: $_"
    Write-Host ""
    Write-Host "To retry infrastructure deployment only:" -ForegroundColor Yellow
    Write-Host "  .\deploy-infra\deploy.ps1 -ResourceGroup '$ResourceGroup' -Location '$Location'" -ForegroundColor White
    exit 1
}

Write-Host ""
Write-Host "✓ Infrastructure deployment completed successfully" -ForegroundColor Green
Write-Host ""

# ========================================
# Wait for Azure resources to stabilize
# ========================================

Write-Host "Waiting for Azure resources to stabilize..." -ForegroundColor Cyan
Write-Host "This ensures App Service settings are fully applied before code deployment." -ForegroundColor White
Start-Sleep -Seconds 15
Write-Host "✓ Ready for application deployment" -ForegroundColor Green
Write-Host ""

# ========================================
# Phase 2: Application Deployment
# ========================================

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Phase 2: Application Deployment       " -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Application deployment script reads from deployment context file
# No parameters needed
Write-Host "Calling: $appScript" -ForegroundColor Yellow
Write-Host ""

try {
    & $appScript
    
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Application deployment failed with exit code: $LASTEXITCODE"
        Write-Host ""
        Write-Host "Infrastructure deployed successfully, but application deployment failed." -ForegroundColor Yellow
        Write-Host "To retry application deployment only:" -ForegroundColor Yellow
        Write-Host "  .\deploy-app\deploy.ps1" -ForegroundColor White
        exit $LASTEXITCODE
    }
}
catch {
    Write-Error "Application deployment failed: $_"
    Write-Host ""
    Write-Host "Infrastructure deployed successfully, but application deployment failed." -ForegroundColor Yellow
    Write-Host "To retry application deployment only:" -ForegroundColor Yellow
    Write-Host "  .\deploy-app\deploy.ps1" -ForegroundColor White
    exit 1
}

Write-Host ""
Write-Host "✓ Application deployment completed successfully" -ForegroundColor Green
Write-Host ""

# ========================================
# Final Summary
# ========================================

Write-Host "========================================" -ForegroundColor Green
Write-Host "  Complete Deployment Successful! 🎉    " -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""

# Load deployment context for final summary
$contextFile = "./.deployment-context.json"
if (Test-Path $contextFile) {
    $context = Get-Content $contextFile | ConvertFrom-Json
    $appUrl = "https://$($context.webAppName).azurewebsites.net"
    
    Write-Host "Your application is ready!" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Access your application at:" -ForegroundColor White
    Write-Host "  Main App: $appUrl/Index" -ForegroundColor Yellow
    Write-Host "  API Docs: $appUrl/swagger" -ForegroundColor Yellow
    
    if ($DeployGenAI -and $context.openAIEndpoint) {
        Write-Host "  AI Chat:  $appUrl/Chat" -ForegroundColor Yellow
    }
    
    Write-Host ""
    Write-Host "Resource Group: $($context.resourceGroup)" -ForegroundColor White
    Write-Host "Deployment Date: $($context.deploymentDate)" -ForegroundColor White
    Write-Host ""
    Write-Host "Note: The application may take 1-2 minutes to fully start." -ForegroundColor Gray
} else {
    Write-Host "Deployment completed successfully!" -ForegroundColor White
    Write-Host "Check the output above for application URLs." -ForegroundColor White
}

Write-Host ""

exit 0
