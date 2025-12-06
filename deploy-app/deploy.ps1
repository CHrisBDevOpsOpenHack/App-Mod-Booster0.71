#Requires -Version 7.0

<#
.SYNOPSIS
    Deploys the Expense Management application code to Azure App Service.

.DESCRIPTION
    This script builds the .NET application and deploys it to Azure App Service.
    It reads deployment context from .deployment-context.json if available.

.PARAMETER ResourceGroup
    The name of the Azure resource group (overrides context file).

.PARAMETER WebAppName
    The name of the Azure Web App (overrides context file).

.PARAMETER SkipBuild
    Skip the dotnet build and publish steps.

.EXAMPLE
    .\deploy.ps1

.EXAMPLE
    .\deploy.ps1 -ResourceGroup "rg-expensemgmt-20241206" -WebAppName "app-expensemgmt-xyz123"
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

# Set error action preference
$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Expense Management Application Deployment" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Check for deployment context file
$contextFile = "./.deployment-context.json"
$contextData = $null

if (Test-Path $contextFile) {
    Write-Host "Loading deployment context from $contextFile..." -ForegroundColor Yellow
    $contextData = Get-Content $contextFile | ConvertFrom-Json
    
    if ([string]::IsNullOrEmpty($ResourceGroup)) {
        $ResourceGroup = $contextData.resourceGroup
        Write-Host "✓ Using resource group from context: $ResourceGroup" -ForegroundColor Green
    }
    
    if ([string]::IsNullOrEmpty($WebAppName)) {
        $WebAppName = $contextData.webAppName
        Write-Host "✓ Using web app name from context: $WebAppName" -ForegroundColor Green
    }
}

# Validate required parameters
if ([string]::IsNullOrEmpty($ResourceGroup) -or [string]::IsNullOrEmpty($WebAppName)) {
    Write-Error "ResourceGroup and WebAppName are required. Either provide them as parameters or ensure .deployment-context.json exists."
    exit 1
}

# Check Azure CLI
Write-Host ""
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
try {
    $account = az account show --output json 2>$null | ConvertFrom-Json
    if ($null -eq $account) {
        throw "Not logged in"
    }
    Write-Host "✓ Logged in to Azure as: $($account.user.name)" -ForegroundColor Green
}
catch {
    Write-Error "Not logged in to Azure. Please run 'az login' first."
    exit 1
}

# Check .NET SDK
try {
    $dotnetVersion = dotnet --version
    Write-Host "✓ .NET SDK version: $dotnetVersion" -ForegroundColor Green
}
catch {
    Write-Error ".NET SDK is not installed. Please install .NET 8 SDK from: https://dot.net"
    exit 1
}

# Build and publish the application
if (-not $SkipBuild) {
    Write-Host ""
    Write-Host "Building application..." -ForegroundColor Yellow
    
    $projectPath = "./src/ExpenseManagement/ExpenseManagement.csproj"
    
    if (-not (Test-Path $projectPath)) {
        Write-Error "Project file not found: $projectPath"
        exit 1
    }
    
    try {
        dotnet publish $projectPath -c Release -o ./publish --no-self-contained
        Write-Host "✓ Application built successfully" -ForegroundColor Green
    }
    catch {
        Write-Error "Failed to build application: $_"
        exit 1
    }
}
else {
    Write-Host ""
    Write-Host "Skipping build (SkipBuild flag set)" -ForegroundColor Yellow
}

# Create deployment package
Write-Host ""
Write-Host "Creating deployment package..." -ForegroundColor Yellow

$publishDir = "./publish"
$zipFile = "./app-deployment.zip"

if (-not (Test-Path $publishDir)) {
    Write-Error "Publish directory not found: $publishDir. Run without -SkipBuild first."
    exit 1
}

# Remove old zip if exists
if (Test-Path $zipFile) {
    Remove-Item $zipFile -Force
}

# Create zip file with files at root level (not in subdirectory)
try {
    Compress-Archive -Path "$publishDir/*" -DestinationPath $zipFile -CompressionLevel Optimal
    Write-Host "✓ Deployment package created: $zipFile" -ForegroundColor Green
}
catch {
    Write-Error "Failed to create deployment package: $_"
    exit 1
}

# Deploy to Azure App Service
Write-Host ""
Write-Host "Deploying to Azure App Service..." -ForegroundColor Yellow

try {
    az webapp deploy `
        --resource-group $ResourceGroup `
        --name $WebAppName `
        --src-path $zipFile `
        --type zip `
        --clean true `
        --restart true `
        --output none
    
    Write-Host "✓ Application deployed successfully" -ForegroundColor Green
}
catch {
    Write-Error "Failed to deploy application: $_"
    exit 1
}

# Clean up
Write-Host ""
Write-Host "Cleaning up..." -ForegroundColor Yellow
Remove-Item $zipFile -Force
Write-Host "✓ Deployment package removed" -ForegroundColor Green

# Get web app URL
Write-Host ""
Write-Host "Retrieving application URL..." -ForegroundColor Yellow
$webAppDetails = az webapp show --resource-group $ResourceGroup --name $WebAppName --output json | ConvertFrom-Json
$appUrl = "https://$($webAppDetails.defaultHostDomainName)"

# Summary
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Deployment Complete!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Resource Group: $ResourceGroup" -ForegroundColor White
Write-Host "Web App Name: $WebAppName" -ForegroundColor White
Write-Host ""
Write-Host "Application URLs:" -ForegroundColor Yellow
Write-Host "  Main App: $appUrl/Index" -ForegroundColor White
Write-Host "  API Docs: $appUrl/swagger" -ForegroundColor White
Write-Host ""
Write-Host "Note: It may take a minute for the application to start." -ForegroundColor Gray
Write-Host ""
