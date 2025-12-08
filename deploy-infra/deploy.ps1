param(
    [Parameter(Mandatory = $true)]
    [string]$ResourceGroup,
    [Parameter(Mandatory = $true)]
    [string]$Location,
    [string]$BaseName = "expensemgmt",
    [switch]$DeployGenAI,
    [switch]$SkipDatabaseSetup
)

$scriptRoot = $PSScriptRoot
$repoRoot = Split-Path -Parent $scriptRoot
$IsCI = $env:GITHUB_ACTIONS -eq "true" -or $env:TF_BUILD -eq "true" -or $env:CI -eq "true"

Write-Host "=== Expense Management Infrastructure Deployment ==="
Write-Host "Resource Group: $ResourceGroup"
Write-Host "Location     : $Location"
Write-Host "Base Name    : $BaseName"
Write-Host "CI Mode      : $IsCI"

function Assert-Command {
    param(
        [string]$Name
    )
    if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
        throw "$Name is not installed or not available on PATH."
    }
}

Assert-Command -Name "az"
Assert-Command -Name "sqlcmd"

try {
    az account show 1>$null 2>$null
} catch {
    throw "Azure CLI is not logged in. Run az login first."
}

if ($IsCI) {
    if (-not $env:AZURE_CLIENT_ID) {
        throw "AZURE_CLIENT_ID is required in CI/CD mode."
    }
    $sp = az ad sp show --id $env:AZURE_CLIENT_ID --output json 2>$null | ConvertFrom-Json
    if (-not $sp) {
        throw "Unable to resolve service principal for AZURE_CLIENT_ID."
    }
    $adminObjectId = $sp.id
    $adminUpn = $sp.displayName
    $adminPrincipalType = "Application"
} else {
    $signedIn = az ad signed-in-user show --output json 2>$null | ConvertFrom-Json
    $adminObjectId = $signedIn.id
    $adminUpn = $signedIn.userPrincipalName
    $adminPrincipalType = "User"
}

Write-Host "Using admin principal: $adminUpn ($adminPrincipalType)"

Write-Host "Creating/validating resource group..."
az group create --name $ResourceGroup --location $Location 1>$null

Write-Host "Deploying Bicep templates..."
$deploymentOutput = az deployment group create `
    --resource-group $ResourceGroup `
    --template-file (Join-Path $scriptRoot "main.bicep") `
    --parameters `
    location=$Location `
    baseName=$BaseName `
    adminObjectId=$adminObjectId `
    adminUserPrincipalName=$adminUpn `
    adminPrincipalType=$adminPrincipalType `
    deployGenAI=$($DeployGenAI.IsPresent.ToString().ToLower()) `
    --output json 2>$null
$deployment = $deploymentOutput | ConvertFrom-Json
$outputs = $deployment.properties.outputs

$webAppName = $outputs.webAppName.value
$sqlServerName = $outputs.sqlServerName.value
$sqlServerFqdn = $outputs.sqlServerFqdn.value
$databaseName = $outputs.databaseName.value
$managedIdentityClientId = $outputs.managedIdentityClientId.value
$managedIdentityPrincipalId = $outputs.managedIdentityPrincipalId.value
$appInsightsConnectionString = $outputs.appInsightsConnectionString.value
$openAIEndpoint = $outputs.openAIEndpoint.value
$openAIModelName = $outputs.openAIModelName.value
$searchEndpoint = $outputs.searchEndpoint.value

if (-not $SkipDatabaseSetup) {
    Write-Host "Waiting for SQL Server to become ready..."
    Start-Sleep -Seconds 30

    $clientIp = (Invoke-RestMethod -Uri "https://api.ipify.org") -as [string]
    if (-not [string]::IsNullOrWhiteSpace($clientIp)) {
        Write-Host "Adding firewall rule for client IP $clientIp"
        az sql server firewall-rule create `
            --resource-group $ResourceGroup `
            --server $sqlServerName `
            --name "ClientIPAddress" `
            --start-ip-address $clientIp `
            --end-ip-address $clientIp 1>$null
    }

    $authMethod = if ($IsCI) { "ActiveDirectoryAzCli" } else { "ActiveDirectoryDefault" }
    $schemaPath = Join-Path $repoRoot "Database-Schema/database_schema.sql"
    Write-Host "Importing database schema..."
    sqlcmd -S $sqlServerFqdn -d $databaseName "--authentication-method=$authMethod" -i $schemaPath

    $managedIdentityName = $webAppName
    $guidBytes = [System.Guid]::Parse($managedIdentityClientId).ToByteArray()
    $sidHex = "0x" + [System.BitConverter]::ToString($guidBytes).Replace("-", "")

    $userSql = @"
IF EXISTS (SELECT * FROM sys.database_principals WHERE name = '$managedIdentityName')
    DROP USER [$managedIdentityName];

CREATE USER [$managedIdentityName] WITH SID = $sidHex, TYPE = E;

ALTER ROLE db_datareader ADD MEMBER [$managedIdentityName];
ALTER ROLE db_datawriter ADD MEMBER [$managedIdentityName];
GRANT EXECUTE TO [$managedIdentityName];
"@

    $tempUserFile = [System.IO.Path]::GetTempFileName() + ".sql"
    $userSql | Out-File -FilePath $tempUserFile -Encoding UTF8
    Write-Host "Creating managed identity database user..."
    sqlcmd -S $sqlServerFqdn -d $databaseName "--authentication-method=$authMethod" -i $tempUserFile
    Remove-Item -Path $tempUserFile -Force -ErrorAction SilentlyContinue

    $storedProcPath = Join-Path $repoRoot "stored-procedures.sql"
    if (Test-Path $storedProcPath) {
        Write-Host "Deploying stored procedures..."
        sqlcmd -S $sqlServerFqdn -d $databaseName "--authentication-method=$authMethod" -i $storedProcPath
    } else {
        Write-Warning "stored-procedures.sql not found. Skipping stored procedure deployment."
    }
}

Write-Host "Configuring App Service settings..."
$connectionString = "Server=tcp:$sqlServerFqdn,1433;Initial Catalog=$databaseName;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;Authentication=Active Directory Managed Identity;User Id=$managedIdentityClientId;"
$appSettings = @(
    "AZURE_CLIENT_ID=$managedIdentityClientId",
    "ManagedIdentityClientId=$managedIdentityClientId",
    "ConnectionStrings__DefaultConnection=$connectionString",
    "APPLICATIONINSIGHTS_CONNECTION_STRING=$appInsightsConnectionString"
)

if ($DeployGenAI.IsPresent -and $openAIEndpoint) {
    $appSettings += "GenAISettings__OpenAIEndpoint=$openAIEndpoint"
    $appSettings += "GenAISettings__OpenAIModelName=$openAIModelName"
    if ($searchEndpoint) {
        $appSettings += "GenAISettings__SearchEndpoint=$searchEndpoint"
    }
}

az webapp config appsettings set `
    --name $webAppName `
    --resource-group $ResourceGroup `
    --settings $appSettings 1>$null

$context = @{
    resourceGroup = $ResourceGroup
    location = $Location
    baseName = $BaseName
    webAppName = $webAppName
    sqlServerFqdn = $sqlServerFqdn
    databaseName = $databaseName
    managedIdentityClientId = $managedIdentityClientId
    managedIdentityPrincipalId = $managedIdentityPrincipalId
    appInsightsConnectionString = $appInsightsConnectionString
    connectionString = $connectionString
    openAIEndpoint = $openAIEndpoint
    openAIModelName = $openAIModelName
    searchEndpoint = $searchEndpoint
}

$contextPath = Join-Path $repoRoot ".deployment-context.json"
$context | ConvertTo-Json | Out-File -FilePath $contextPath -Encoding UTF8

Write-Host "Deployment complete."
Write-Host "Web App      : https://$webAppName.azurewebsites.net/Index"
Write-Host "Swagger      : https://$webAppName.azurewebsites.net/swagger"
if ($DeployGenAI) {
    Write-Host "Chat         : https://$webAppName.azurewebsites.net/Chat"
}
