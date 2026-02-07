<#
.SYNOPSIS
    Deploys the Expense Management application to Azure App Service.

.DESCRIPTION
    This script automates application deployment by:
    - Reading .deployment-context.json (from infra deployment)
    - Building and publishing the .NET application
    - Creating a deployment zip package
    - Deploying to Azure App Service
    
    Works seamlessly after deploy-infra/deploy.ps1 with no parameters required.

.PARAMETER ResourceGroup
    Resource group name (optional). Overrides value from context file.

.PARAMETER WebAppName
    Web app name (optional). Overrides value from context file.

.EXAMPLE
    .\deploy.ps1
    
    Reads context file and deploys automatically.

.EXAMPLE
    .\deploy.ps1 -ResourceGroup "rg-expensemgmt-20260207" -WebAppName "app-expensemgmt-abc123"
    
    Explicitly specify resource group and web app name.
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory=$false)]
    [string]$ResourceGroup,
    
    [Parameter(Mandatory=$false)]
    [string]$WebAppName
)

$ErrorActionPreference = "Stop"

# Display header
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host " Application Deployment Script" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Determine script and repo paths
$scriptDir = $PSScriptRoot
$repoRoot = Split-Path -Parent $scriptDir

# Look for deployment context file in current dir or parent dir
$contextFile = $null
$contextFilePath1 = Join-Path $scriptDir ".deployment-context.json"
$contextFilePath2 = Join-Path $repoRoot ".deployment-context.json"

if (Test-Path $contextFilePath1) {
    $contextFile = $contextFilePath1
} elseif (Test-Path $contextFilePath2) {
    $contextFile = $contextFilePath2
}

# Load context if available
if ($contextFile) {
    Write-Host "Loading deployment context from: $contextFile" -ForegroundColor Yellow
    $context = Get-Content $contextFile | ConvertFrom-Json
    
    # Use context values if parameters not provided
    if ([string]::IsNullOrWhiteSpace($ResourceGroup)) {
        $ResourceGroup = $context.resourceGroup
    }
    if ([string]::IsNullOrWhiteSpace($WebAppName)) {
        $WebAppName = $context.webAppName
    }
    
    Write-Host "✓ Context loaded" -ForegroundColor Green
    Write-Host "  Resource Group: $ResourceGroup" -ForegroundColor Gray
    Write-Host "  Web App: $WebAppName" -ForegroundColor Gray
} else {
    Write-Host "No .deployment-context.json found" -ForegroundColor Yellow
    
    # Require parameters if no context file
    if ([string]::IsNullOrWhiteSpace($ResourceGroup) -or [string]::IsNullOrWhiteSpace($WebAppName)) {
        Write-Error "Either run deploy-infra/deploy.ps1 first to create .deployment-context.json, or provide -ResourceGroup and -WebAppName parameters"
        exit 1
    }
}

# Check Azure CLI
Write-Host ""
Write-Host "Checking prerequisites..." -ForegroundColor Yellow
try {
    $azVersion = az version --output json 2>$null | ConvertFrom-Json
    Write-Host "✓ Azure CLI version $($azVersion.'azure-cli')" -ForegroundColor Green
} catch {
    Write-Error "Azure CLI is not installed. Install from https://aka.ms/azure-cli"
    exit 1
}

# Check Azure CLI login
try {
    $accountInfo = az account show --output json 2>$null | ConvertFrom-Json
    Write-Host "✓ Authenticated to subscription: $($accountInfo.name)" -ForegroundColor Green
} catch {
    Write-Error "Not logged in to Azure. Run: az login"
    exit 1
}

# Check .NET SDK
try {
    $dotnetVersion = dotnet --version 2>&1
    Write-Host "✓ .NET SDK version $dotnetVersion" -ForegroundColor Green
} catch {
    Write-Error ".NET SDK is not installed. Install from https://dotnet.microsoft.com/download"
    exit 1
}

# Build and publish the application
Write-Host ""
Write-Host "Building and publishing application..." -ForegroundColor Yellow
$projectPath = Join-Path $repoRoot "src/ExpenseManagement/ExpenseManagement.csproj"

if (-not (Test-Path $projectPath)) {
    Write-Error "Project file not found: $projectPath"
    exit 1
}

$publishDir = Join-Path $repoRoot "publish-temp"

# Clean previous publish directory
if (Test-Path $publishDir) {
    Remove-Item -Path $publishDir -Recurse -Force -ErrorAction SilentlyContinue
}

# Publish the application
try {
    dotnet publish $projectPath `
        --configuration Release `
        --output $publishDir `
        --nologo `
        --verbosity minimal 2>&1 | Out-Null
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✓ Application published successfully" -ForegroundColor Green
    } else {
        Write-Error "Failed to publish application"
        exit 1
    }
} catch {
    Write-Error "Failed to publish application: $_"
    exit 1
}

# Create deployment zip package
Write-Host ""
Write-Host "Creating deployment package..." -ForegroundColor Yellow

$zipFile = Join-Path $repoRoot "deployment-package.zip"

# Remove existing zip if present
if (Test-Path $zipFile) {
    Remove-Item -Path $zipFile -Force -ErrorAction SilentlyContinue
}

# Create zip with files at root level (not in subdirectory)
try {
    $filesToZip = Get-ChildItem -Path $publishDir -File -Recurse
    
    # Use .NET compression (works cross-platform)
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $zip = [System.IO.Compression.ZipFile]::Open($zipFile, [System.IO.Compression.ZipArchiveMode]::Create)
    
    foreach ($file in $filesToZip) {
        $relativePath = $file.FullName.Substring($publishDir.Length + 1)
        $entry = $zip.CreateEntry($relativePath, [System.IO.Compression.CompressionLevel]::Optimal)
        $entryStream = $entry.Open()
        $fileStream = [System.IO.File]::OpenRead($file.FullName)
        $fileStream.CopyTo($entryStream)
        $fileStream.Close()
        $entryStream.Close()
    }
    
    $zip.Dispose()
    
    $zipSizeMB = [math]::Round((Get-Item $zipFile).Length / 1MB, 2)
    Write-Host "✓ Deployment package created: $zipSizeMB MB" -ForegroundColor Green
} catch {
    Write-Error "Failed to create deployment package: $_"
    exit 1
}

# Deploy to Azure App Service
Write-Host ""
Write-Host "Deploying to Azure App Service..." -ForegroundColor Yellow
Write-Host "(This may take 2-3 minutes...)" -ForegroundColor Yellow

try {
    $deployResult = az webapp deploy `
        --resource-group $ResourceGroup `
        --name $WebAppName `
        --src-path $zipFile `
        --type zip `
        --clean true `
        --restart true `
        --output json 2>&1
    
    # Check deployment result (az webapp deploy outputs warnings to stderr which are normal)
    if ($deployResult -match '"status":\s*"RuntimeSuccessful"' -or $deployResult -match 'Deployment has completed successfully') {
        Write-Host "✓ Deployment completed successfully!" -ForegroundColor Green
    } elseif ($LASTEXITCODE -ne 0) {
        # Even if exit code is non-zero, check if deployment actually succeeded
        if ($deployResult -match 'completed successfully' -or $deployResult -match 'RuntimeSuccessful') {
            Write-Host "✓ Deployment succeeded despite warnings" -ForegroundColor Green
        } else {
            Write-Warning "Deployment may have failed. Details:"
            Write-Host $deployResult
            Write-Error "Deployment failed. Check Azure Portal for details."
            exit 1
        }
    } else {
        Write-Host "✓ Deployment completed" -ForegroundColor Green
    }
} catch {
    Write-Error "Failed to deploy to Azure App Service: $_"
    exit 1
}

# Clean up temporary files
Write-Host ""
Write-Host "Cleaning up temporary files..." -ForegroundColor Yellow
Remove-Item -Path $publishDir -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -Path $zipFile -Force -ErrorAction SilentlyContinue
Write-Host "✓ Cleanup complete" -ForegroundColor Green

# Display application URLs
Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host " Application Deployment Complete" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
Write-Host "Resource Group:  $ResourceGroup" -ForegroundColor Cyan
Write-Host "Web App Name:    $WebAppName" -ForegroundColor Cyan
Write-Host ""
Write-Host "Application URLs:" -ForegroundColor Yellow
Write-Host "  Main App:      https://$WebAppName.azurewebsites.net/Index" -ForegroundColor Cyan
Write-Host "  Expenses:      https://$WebAppName.azurewebsites.net/Expenses" -ForegroundColor Cyan
Write-Host "  Approvals:     https://$WebAppName.azurewebsites.net/Approvals" -ForegroundColor Cyan
Write-Host "  Chat (if enabled): https://$WebAppName.azurewebsites.net/Chat" -ForegroundColor Cyan
Write-Host "  API Swagger:   https://$WebAppName.azurewebsites.net/swagger" -ForegroundColor Cyan
Write-Host ""
Write-Host "Tip: The application may take 30-60 seconds to warm up on first access." -ForegroundColor Gray
Write-Host ""
