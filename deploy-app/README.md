# Application Deployment

This folder contains the application deployment automation for the Expense Management application.

## Prerequisites

- **Azure CLI** - [Install Azure CLI](https://aka.ms/azure-cli)
- **.NET 8 SDK** - [Install .NET SDK](https://dotnet.microsoft.com/download)
- **PowerShell 7+** - Recommended for best compatibility
- **Azure subscription** - With Contributor role
- **Infrastructure deployed** - Run `deploy-infra/deploy.ps1` first

## Quick Start

### 1. Deploy infrastructure first

```powershell
.\deploy-infra\deploy.ps1 -ResourceGroup "rg-expensemgmt-20260207" -Location "uksouth"
```

This creates `.deployment-context.json` with all required deployment information.

### 2. Deploy the application

**Automatic (reads context file):**

```powershell
.\deploy-app\deploy.ps1
```

**Manual (specify parameters):**

```powershell
.\deploy-app\deploy.ps1 -ResourceGroup "rg-expensemgmt-20260207" -WebAppName "app-expensemgmt-abc123"
```

## What the Script Does

1. ✓ Reads `.deployment-context.json` (created by infra deployment)
2. ✓ Validates Azure CLI and .NET SDK are installed
3. ✓ Builds and publishes the .NET 8 application
4. ✓ Creates deployment zip package (DLLs at root level)
5. ✓ Deploys to Azure App Service with `--clean` and `--restart` flags
6. ✓ Cleans up temporary files
7. ✓ Displays application URLs

## Parameters

| Parameter | Required | Default | Description |
|-----------|----------|---------|-------------|
| `-ResourceGroup` | No | From context | Resource group name (overrides context file) |
| `-WebAppName` | No | From context | Web app name (overrides context file) |

## Deployment Context File

The script looks for `.deployment-context.json` in two locations:

1. **Current directory** (./deploy-app/)
2. **Parent directory** (repository root)

This allows the script to work correctly whether called from:
- Repository root: `.\deploy-app\deploy.ps1` (reads from `../.deployment-context.json`)
- deploy-app folder: `.\deploy.ps1` (reads from `./.deployment-context.json`)

Example context file:

```json
{
  "resourceGroup": "rg-expensemgmt-20260207",
  "location": "uksouth",
  "webAppName": "app-expensemgmt-abc123",
  "sqlServerFqdn": "sql-expensemgmt-abc123.database.windows.net",
  "databaseName": "Northwind",
  "managedIdentityName": "id-expensemgmt-abc123",
  "managedIdentityClientId": "12345678-1234-1234-1234-123456789abc",
  "appInsightsConnectionString": "InstrumentationKey=...",
  "deployGenAI": false,
  "openAIEndpoint": "",
  "openAIModelName": "",
  "timestamp": "2026-02-07T10:00:00Z"
}
```

## Deployment Package Structure

The script creates a zip file with this structure:

```
deployment-package.zip
├── ExpenseManagement.dll        ← Application DLL (at root)
├── ExpenseManagement.deps.json
├── ExpenseManagement.runtimeconfig.json
├── appsettings.json
├── appsettings.Development.json
├── web.config
└── wwwroot/
    ├── css/
    ├── js/
    └── ...
```

**Critical:** DLL files must be at the root level, not in a subdirectory. Azure App Service expects this structure.

## Application URLs

After deployment, the application is available at:

| Page | URL | Description |
|------|-----|-------------|
| **Main** | `https://{webappname}.azurewebsites.net/Index` | Home page with navigation |
| **Expenses** | `https://{webappname}.azurewebsites.net/Expenses` | Submit and view expenses |
| **Approvals** | `https://{webappname}.azurewebsites.net/Approvals` | Manager approval page |
| **Chat** | `https://{webappname}.azurewebsites.net/Chat` | AI chat (if GenAI deployed) |
| **Swagger** | `https://{webappname}.azurewebsites.net/swagger` | API documentation |

**Note:** Use `/Index` not `/` for the main application page.

## Important Notes

### First Access Warm-Up

The application may take 30-60 seconds to warm up on first access. This is normal for Azure App Service Free tier.

### Deployment Warnings

`az webapp deploy` outputs progress messages to stderr (like "Warming up Kudu", "Starting the site") which are **normal**, not errors. The script handles this correctly by checking for success indicators in the output.

### Path Handling

The script uses `$PSScriptRoot` to build paths relative to the script location. This ensures it works correctly whether called from:

- Repository root: `.\deploy-app\deploy.ps1`
- deploy-app folder: `cd deploy-app; .\deploy.ps1`
- Unified script: `.\deploy-all.ps1`

### Temporary Files

The script creates temporary files during deployment:

- `publish-temp/` - .NET publish output directory
- `deployment-package.zip` - Deployment zip file

Both are automatically cleaned up after deployment. They are also in `.gitignore` to prevent accidental commits.

## Troubleshooting

### "Azure CLI is not installed"

Install from: https://aka.ms/azure-cli

### ".NET SDK is not installed"

Install .NET 8 SDK from: https://dotnet.microsoft.com/download

### "Not logged in to Azure"

```powershell
az login
```

### "No .deployment-context.json found"

Run infrastructure deployment first:

```powershell
.\deploy-infra\deploy.ps1 -ResourceGroup "rg-expensemgmt-20260207" -Location "uksouth"
```

Or specify parameters manually:

```powershell
.\deploy-app\deploy.ps1 -ResourceGroup "rg-expensemgmt-20260207" -WebAppName "app-expensemgmt-abc123"
```

### "Failed to publish application"

Check .NET SDK version:

```powershell
dotnet --version  # Should be 8.0.x or higher
```

Restore packages manually:

```powershell
cd src/ExpenseManagement
dotnet restore
dotnet build
```

### "Deployment failed"

Check App Service logs in Azure Portal:

```powershell
az webapp log tail --resource-group "rg-expensemgmt-20260207" --name "app-expensemgmt-abc123"
```

## Manual Deployment (Alternative)

If you prefer manual steps over the automated script:

```powershell
# 1. Build and publish
cd src/ExpenseManagement
dotnet publish --configuration Release --output ./publish

# 2. Create zip (DLLs at root level)
Compress-Archive -Path ./publish/* -DestinationPath deployment.zip

# 3. Deploy to App Service
az webapp deploy `
    --resource-group "rg-expensemgmt-20260207" `
    --name "app-expensemgmt-abc123" `
    --src-path deployment.zip `
    --type zip `
    --clean true `
    --restart true
```

## Files in This Folder

```
deploy-app/
├── deploy.ps1     ← Main deployment script (run this)
└── README.md      ← This file
```

## Related Documentation

- [Infrastructure Deployment](../deploy-infra/README.md)
- [Unified Deployment](../deploy-all.ps1)
- [CI/CD Setup](../.github/CICD-SETUP.md)
