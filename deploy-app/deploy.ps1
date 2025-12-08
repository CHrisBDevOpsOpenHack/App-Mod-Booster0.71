param(
    [string]$ResourceGroup,
    [string]$WebAppName,
    [switch]$SkipBuild,
    [switch]$ConfigureSettings
)

$scriptRoot = $PSScriptRoot
$repoRoot = Split-Path -Parent $scriptRoot
$contextPaths = @(
    (Join-Path $repoRoot ".deployment-context.json"),
    (Join-Path $scriptRoot ".deployment-context.json")
)

$context = $null
foreach ($path in $contextPaths) {
    if (Test-Path $path) {
        $context = Get-Content $path | ConvertFrom-Json
        break
    }
}

if (-not $context -and (-not $ResourceGroup -or -not $WebAppName)) {
    throw "Deployment context not found. Provide -ResourceGroup and -WebAppName or run deploy-infra first."
}

if (-not $ResourceGroup) { $ResourceGroup = $context.resourceGroup }
if (-not $WebAppName) { $WebAppName = $context.webAppName }

Write-Host "=== Application Deployment ==="
Write-Host "Resource Group: $ResourceGroup"
Write-Host "Web App Name : $WebAppName"

function Assert-Command {
    param([string]$Name)
    if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
        throw "$Name is required but not found on PATH."
    }
}

Assert-Command -Name "az"
Assert-Command -Name "dotnet"

try {
    az account show 1>$null 2>$null
} catch {
    throw "Azure CLI is not logged in. Run az login first."
}

$projectPath = Join-Path $repoRoot "src/ExpenseManagement/ExpenseManagement.csproj"
$publishDir = Join-Path $scriptRoot "publish"
$zipPath = Join-Path $scriptRoot "app.zip"

if (Test-Path $publishDir) { Remove-Item -Recurse -Force $publishDir }
if (Test-Path $zipPath) { Remove-Item -Force $zipPath }

if (-not $SkipBuild) {
    Write-Host "Building application..."
    dotnet publish $projectPath -c Release -o $publishDir
}

Write-Host "Packaging artifacts..."
Compress-Archive -Path (Join-Path $publishDir "*") -DestinationPath $zipPath

if ($ConfigureSettings -and $context) {
    Write-Host "Applying application settings from context..."
    $settings = @(
        "AZURE_CLIENT_ID=$($context.managedIdentityClientId)",
        "ManagedIdentityClientId=$($context.managedIdentityClientId)",
        "ConnectionStrings__DefaultConnection=$($context.connectionString)",
        "APPLICATIONINSIGHTS_CONNECTION_STRING=$($context.appInsightsConnectionString)"
    )
    if ($context.openAIEndpoint) {
        $settings += "GenAISettings__OpenAIEndpoint=$($context.openAIEndpoint)"
        $settings += "GenAISettings__OpenAIModelName=$($context.openAIModelName)"
    }
    az webapp config appsettings set --name $WebAppName --resource-group $ResourceGroup --settings $settings 1>$null
}

Write-Host "Deploying to Azure App Service..."
az webapp deploy `
    --resource-group $ResourceGroup `
    --name $WebAppName `
    --type zip `
    --src-path $zipPath `
    --clean `
    --restart true 1>$null

Remove-Item -Recurse -Force $publishDir -ErrorAction SilentlyContinue
Remove-Item -Force $zipPath -ErrorAction SilentlyContinue

Write-Host "Deployment complete."
Write-Host "Main App : https://$WebAppName.azurewebsites.net/Index"
Write-Host "Swagger  : https://$WebAppName.azurewebsites.net/swagger"
Write-Host "Chat     : https://$WebAppName.azurewebsites.net/Chat"
