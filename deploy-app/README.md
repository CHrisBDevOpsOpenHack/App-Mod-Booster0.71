# Application Deployment

This folder contains the script to deploy the Expense Management application to Azure App Service.

## Prerequisites

- Azure CLI installed and configured
- .NET 8 SDK installed
- Infrastructure already deployed (via `deploy-infra/deploy.ps1`)
- PowerShell 7+ recommended

## Quick Start

After running the infrastructure deployment, simply run:

```powershell
.\deploy.ps1
```

The script automatically reads the `.deployment-context.json` file created by the infrastructure deployment.

## Parameters

| Parameter | Required | Default | Description |
|-----------|----------|---------|-------------|
| ResourceGroup | No | From context | Name of the Azure resource group |
| WebAppName | No | From context | Name of the Azure Web App |
| SkipBuild | No | false | Skip the dotnet build step |
| ConfigureSettings | No | false | Reconfigure app settings after deployment |

## Examples

```powershell
# Standard deployment (uses context file)
.\deploy.ps1

# Redeploy without rebuilding
.\deploy.ps1 -SkipBuild

# Override values from context
.\deploy.ps1 -ResourceGroup "rg-custom" -WebAppName "app-custom"

# Reconfigure settings
.\deploy.ps1 -ConfigureSettings
```

## What the Script Does

1. Reads deployment context from `.deployment-context.json`
2. Builds the .NET application using `dotnet publish`
3. Creates a zip package with published files at the root level
4. Deploys to Azure App Service using `az webapp deploy`
5. Cleans up temporary files

## Application URLs

After deployment:

- **Main Application**: `https://<webapp-name>.azurewebsites.net/Index`
- **Swagger API Docs**: `https://<webapp-name>.azurewebsites.net/swagger`

## Troubleshooting

### Build Errors
- Ensure .NET 8 SDK is installed: `dotnet --version`
- Check project file exists at `src/ExpenseManagement/ExpenseManagement.csproj`

### Deployment Errors
- Verify Azure CLI is logged in: `az account show`
- Check the Web App exists in the resource group
- Ensure the deployment context file exists

### Context File Not Found
- Run `deploy-infra/deploy.ps1` first to create the context file
- Or provide `-ResourceGroup` and `-WebAppName` parameters manually
