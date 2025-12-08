param(
    [Parameter(Mandatory = $true)]
    [string]$ResourceGroup,
    [Parameter(Mandatory = $true)]
    [string]$Location,
    [string]$BaseName = "expensemgmt",
    [switch]$DeployGenAI
)

Write-Host "=== Unified Deployment: Infrastructure + Application ==="

$infraScript = Join-Path $PSScriptRoot "deploy-infra/deploy.ps1"
$appScript = Join-Path $PSScriptRoot "deploy-app/deploy.ps1"

if (-not (Test-Path $infraScript)) { throw "Infrastructure script not found at $infraScript" }
if (-not (Test-Path $appScript)) { throw "Application script not found at $appScript" }

$infraArgs = @{
    ResourceGroup = $ResourceGroup
    Location      = $Location
    BaseName      = $BaseName
}
if ($DeployGenAI) {
    $infraArgs["DeployGenAI"] = $true
}

Write-Host "Running infrastructure deployment..."
& $infraScript @infraArgs
if ($LASTEXITCODE -ne 0) {
    throw "Infrastructure deployment failed. Check deploy-infra/deploy.ps1 output."
}

Write-Host "Waiting for resources to stabilize..."
Start-Sleep -Seconds 15

Write-Host "Running application deployment..."
& $appScript
if ($LASTEXITCODE -ne 0) {
    throw "Application deployment failed. Check deploy-app/deploy.ps1 output."
}

Write-Host "Deployment completed successfully."
