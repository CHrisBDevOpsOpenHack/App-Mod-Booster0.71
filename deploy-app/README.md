# Application Deployment

This directory contains scripts for deploying the Expense Management application code to Azure App Service.

## Prerequisites

- [Azure CLI](https://aka.ms/azure-cli) installed
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) installed
- Infrastructure deployed via `deploy-infra/deploy.ps1`

## Quick Start

After running the infrastructure deployment, simply run:

```powershell
.\deploy.ps1
```

The script automatically reads the deployment context file created by the infrastructure deployment, so no parameters are needed!

## Manual Deployment

If you need to specify the target manually:

```powershell
.\deploy.ps1 -ResourceGroup "rg-expensemgmt-20251208" -WebAppName "app-expensemgmt-20251208"
```

## Options

### Skip Build

To deploy without rebuilding (useful for quick redeployments):

```powershell
.\deploy.ps1 -SkipBuild
```

### Configure Settings

To configure App Service settings after deployment:

```powershell
.\deploy.ps1 -ConfigureSettings
```

Note: Settings are automatically configured during infrastructure deployment, so this is rarely needed.

## What the Script Does

1. Reads deployment context from `.deployment-context.json` (if available)
2. Validates Azure CLI login
3. Builds the .NET application using `dotnet publish`
4. Creates a deployment package (zip file) with correct structure
5. Deploys to Azure App Service using `az webapp deploy`
6. Displays application URLs

## Application Structure

The script builds and deploys the ASP.NET Core application from `../src/ExpenseManagement/`.

## Deployment Package

The deployment package is created with the following structure:
- DLL files at root level (not in subdirectories)
- Uses `--clean true` to remove old files
- Uses `--restart true` to restart the app after deployment

## Accessing the Application

After deployment, access your application at:

- **Dashboard**: https://your-app-name.azurewebsites.net/Index
- **Chat**: https://your-app-name.azurewebsites.net/Chat
- **API Documentation**: https://your-app-name.azurewebsites.net/swagger

Note: The main page is `/Index`, not the root URL.

## Troubleshooting

### Build Failures

Ensure .NET 8 SDK is installed:
```powershell
dotnet --version
```

### Deployment Failures

Check that:
1. You're logged in to Azure CLI (`az login`)
2. The Web App exists (created by infrastructure deployment)
3. You have appropriate permissions

### Context File Not Found

If the deployment context file is missing, either:
1. Run `deploy-infra/deploy.ps1` first
2. Provide ResourceGroup and WebAppName parameters manually

## Next Steps

After application deployment:

1. Visit the dashboard at `/Index` to see expense data
2. Try the API at `/swagger`
3. Chat with the AI assistant at `/Chat` (if GenAI was deployed)
