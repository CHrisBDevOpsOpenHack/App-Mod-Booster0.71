<#
.SYNOPSIS
    Deploys the Expense Management application to Azure App Service.

.DESCRIPTION
    This script automates deployment of the .NET application to Azure App Service.
    It reads deployment context from the infrastructure deployment or accepts parameters.

.PARAMETER ResourceGroup
    Name of the Azure resource group (optional if context file exists)

.PARAMETER WebAppName
    Name of the Azure Web App (optional if context file exists)

.PARAMETER SkipBuild
    Switch to skip the build step for redeployments

.PARAMETER ConfigureSettings
    Switch to configure app settings after deployment

.EXAMPLE
    .\deploy.ps1

.EXAMPLE
    .\deploy.ps1 -ResourceGroup "rg-expensemgmt" -WebAppName "app-expensemgmt-abc123"
#>

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

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Application Deployment Script" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Determine script and repo paths
$scriptDir = $PSScriptRoot
$repoRoot = Split-Path -Parent $scriptDir

# Try to load deployment context
$contextPath = Join-Path $repoRoot ".deployment-context.json"
$altContextPath = Join-Path $scriptDir "../.deployment-context.json"

$context = $null
if (Test-Path $contextPath) {
    Write-Host "Loading deployment context from: $contextPath" -ForegroundColor Yellow
    $context = Get-Content $contextPath | ConvertFrom-Json
} elseif (Test-Path $altContextPath) {
    Write-Host "Loading deployment context from: $altContextPath" -ForegroundColor Yellow
    $context = Get-Content $altContextPath | ConvertFrom-Json
}

# Use context values if parameters not provided
if ($context) {
    if (-not $ResourceGroup) { $ResourceGroup = $context.ResourceGroup }
    if (-not $WebAppName) { $WebAppName = $context.WebAppName }
}

# Validate required parameters
if (-not $ResourceGroup -or -not $WebAppName) {
    Write-Host "Error: ResourceGroup and WebAppName are required." -ForegroundColor Red
    Write-Host "Either provide them as parameters or run deploy-infra/deploy.ps1 first to create the context file." -ForegroundColor Red
    exit 1
}

Write-Host "Resource Group: $ResourceGroup"
Write-Host "Web App: $WebAppName"
Write-Host ""

# Check Azure CLI is installed
Write-Host "Checking Azure CLI..." -ForegroundColor Yellow
try {
    $azVersion = az version --output json 2>$null | ConvertFrom-Json
    Write-Host "Azure CLI version: $($azVersion.'azure-cli')" -ForegroundColor Green
} catch {
    Write-Host "Error: Azure CLI is not installed or not in PATH." -ForegroundColor Red
    exit 1
}

# Check user is logged in
Write-Host "Checking Azure login status..." -ForegroundColor Yellow
$account = az account show --output json 2>$null | ConvertFrom-Json
if (-not $account) {
    Write-Host "Error: Not logged in to Azure. Please run 'az login' first." -ForegroundColor Red
    exit 1
}
Write-Host "Logged in as: $($account.user.name)" -ForegroundColor Green

# Build the application
$projectPath = Join-Path $repoRoot "src/ExpenseManagement/ExpenseManagement.csproj"
$publishPath = Join-Path $repoRoot "src/ExpenseManagement/bin/Release/net8.0/publish"

if (-not $SkipBuild) {
    Write-Host ""
    Write-Host "Building application..." -ForegroundColor Yellow
    
    if (-not (Test-Path $projectPath)) {
        Write-Host "Error: Project file not found at $projectPath" -ForegroundColor Red
        exit 1
    }
    
    dotnet publish $projectPath -c Release -o $publishPath
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Error: Build failed." -ForegroundColor Red
        exit 1
    }
    
    Write-Host "Build completed successfully." -ForegroundColor Green
} else {
    Write-Host "Skipping build step." -ForegroundColor Yellow
}

# Create deployment zip
Write-Host ""
Write-Host "Creating deployment package..." -ForegroundColor Yellow

$zipPath = Join-Path $repoRoot "deploy.zip"
if (Test-Path $zipPath) {
    Remove-Item $zipPath -Force
}

# Create zip with files at root level
Push-Location $publishPath
Compress-Archive -Path "*" -DestinationPath $zipPath -Force
Pop-Location

Write-Host "Deployment package created: $zipPath" -ForegroundColor Green

# Deploy to Azure
Write-Host ""
Write-Host "Deploying to Azure App Service..." -ForegroundColor Yellow

az webapp deploy `
    --resource-group $ResourceGroup `
    --name $WebAppName `
    --src-path $zipPath `
    --type zip `
    --clean true `
    --restart true `
    --output none

if ($LASTEXITCODE -ne 0) {
    Write-Host "Error: Deployment failed." -ForegroundColor Red
    exit 1
}

Write-Host "Deployment completed successfully!" -ForegroundColor Green

# Clean up
Write-Host ""
Write-Host "Cleaning up temporary files..." -ForegroundColor Yellow
Remove-Item $zipPath -Force -ErrorAction SilentlyContinue

# Configure settings if requested
if ($ConfigureSettings -and $context) {
    Write-Host ""
    Write-Host "Configuring app settings..." -ForegroundColor Yellow
    
    $connectionString = "Server=tcp:$($context.SqlServerFqdn),1433;Initial Catalog=$($context.DatabaseName);Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;Authentication=Active Directory Managed Identity;User Id=$($context.ManagedIdentityClientId);"
    
    az webapp config appsettings set `
        --resource-group $ResourceGroup `
        --name $WebAppName `
        --settings "AZURE_CLIENT_ID=$($context.ManagedIdentityClientId)" "ManagedIdentityClientId=$($context.ManagedIdentityClientId)" `
        --output none
    
    az webapp config connection-string set `
        --resource-group $ResourceGroup `
        --name $WebAppName `
        --connection-string-type SQLAzure `
        --settings "DefaultConnection=$connectionString" `
        --output none
    
    Write-Host "App settings configured." -ForegroundColor Green
}

# Summary
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Deployment Complete!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Application URL: https://$WebAppName.azurewebsites.net/Index" -ForegroundColor Green
Write-Host "Swagger API Docs: https://$WebAppName.azurewebsites.net/swagger" -ForegroundColor Green
Write-Host ""
