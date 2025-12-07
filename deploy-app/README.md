# Application Deployment

This directory contains scripts for deploying the Expense Management application code to Azure App Service.

## 🚀 Quick Start

### Automatic Deployment (Recommended)

After running the infrastructure deployment, simply run:

```powershell
.\deploy.ps1
```

The script automatically reads the deployment context file (`.deployment-context.json`) created by the infrastructure deployment, so no parameters are needed.

### Manual Deployment

If you need to specify the resource group and web app manually:

```powershell
.\deploy.ps1 -ResourceGroup "rg-expensemgmt-20251207" -WebAppName "app-expensemgmt-abc123"
```

## 📋 Prerequisites

- **Azure CLI** - [Install Azure CLI](https://aka.ms/installazurecliwindows)
- **.NET 8 SDK** - [Download .NET 8](https://dot.net)
- **PowerShell 7+** - [Download PowerShell](https://aka.ms/powershell-release)
- **Logged in to Azure** - Run `az login` before deployment
- **Infrastructure already deployed** - Run `deploy-infra/deploy.ps1` first

## 🔧 Script Parameters

| Parameter | Required | Default | Description |
|-----------|----------|---------|-------------|
| `ResourceGroup` | No* | From context | Azure resource group name |
| `WebAppName` | No* | From context | App Service name |
| `SkipBuild` | No | false | Skip build step (use existing artifacts) |
| `ConfigureSettings` | No | false | Configure App Service settings after deployment |

\* Required if deployment context file doesn't exist

## 📦 What the Script Does

1. ✅ Loads deployment context from infrastructure deployment
2. ✅ Validates Azure CLI and authentication
3. ✅ Verifies the target App Service exists
4. ✅ Builds the .NET 8 application using `dotnet publish`
5. ✅ Creates a deployment zip package with files at root level
6. ✅ Deploys to Azure App Service using `az webapp deploy`
7. ✅ Cleans up temporary files
8. ✅ Displays application URLs

## 🏗️ Deployment Process

### Build Configuration

The script builds the application with:
- Configuration: **Release**
- Target Framework: **.NET 8.0**
- Output Path: `./bin/Release/net8.0/publish`

### Package Structure

The deployment package is created with files at the **root level** of the zip file. This is critical for Azure App Service to recognize the application correctly:

```
app-package.zip
├── ExpenseManagement.dll
├── ExpenseManagement.deps.json
├── appsettings.json
├── web.config
└── wwwroot/
    └── ...
```

**Not** in a subdirectory like:
```
app-package.zip
└── publish/
    ├── ExpenseManagement.dll
    └── ...
```

### Deployment Flags

The script uses these `az webapp deploy` flags:
- `--clean true` - Removes existing files before deployment
- `--restart true` - Restarts the app after deployment
- `--timeout 300` - 5-minute timeout for large packages

## 📝 Examples

### Standard Deployment

After infrastructure deployment:

```powershell
cd App-Mod-Booster0.71
.\deploy-app\deploy.ps1
```

### Skip Build (Faster Redeployment)

Useful when testing deployment without code changes:

```powershell
.\deploy-app\deploy.ps1 -SkipBuild
```

### Configure Settings After Deployment

```powershell
.\deploy-app\deploy.ps1 -ConfigureSettings
```

### Manual Specification

```powershell
.\deploy-app\deploy.ps1 `
  -ResourceGroup "rg-expensemgmt-20251207" `
  -WebAppName "app-expensemgmt-abc123"
```

## 🔗 Application URLs

After deployment, access your application at:

- **Main Application**: `https://<app-name>.azurewebsites.net/Index`
- **API Documentation**: `https://<app-name>.azurewebsites.net/swagger`
- **AI Chat** (if GenAI deployed): `https://<app-name>.azurewebsites.net/Chat`

**Note**: The application may take 1-2 minutes to fully start after deployment.

## 🔄 Redeployment

To redeploy after making code changes:

```powershell
.\deploy.ps1
```

The script automatically rebuilds and redeploys. Use `-SkipBuild` only when you want to redeploy existing build artifacts.

## 🐛 Troubleshooting

### Build Fails

**Symptom**: `dotnet publish` fails

**Solutions**:
1. Verify .NET 8 SDK is installed: `dotnet --version`
2. Clean and rebuild: `dotnet clean && dotnet build`
3. Check for compilation errors in the source code

### Deployment Fails

**Symptom**: `az webapp deploy` fails

**Solutions**:
1. Verify you're logged in to Azure: `az account show`
2. Check App Service exists: `az webapp show --name <app-name> --resource-group <rg-name>`
3. Ensure you have proper permissions on the App Service
4. Check deployment logs in Azure Portal

### Application Won't Start

**Symptom**: Deployment succeeds but application shows errors

**Solutions**:
1. Wait 1-2 minutes for the application to fully start
2. Check Application Insights logs for errors
3. Verify App Service settings are configured correctly:
   - `ConnectionStrings__DefaultConnection`
   - `AZURE_CLIENT_ID`
   - `ManagedIdentityClientId`
4. Review deployment logs in Azure Portal (Deployment Center)

### Deployment Package Issues

**Symptom**: "Could not find a part of the path" or similar errors

**Solutions**:
1. Ensure the publish directory exists
2. Run without `-SkipBuild` to create fresh artifacts
3. Check disk space is available for build and zip creation

### Connection String Not Working

**Symptom**: Database connection errors

**Solutions**:
1. The infrastructure deployment script should have configured the connection string
2. If needed, manually configure using the deployment context:
   ```powershell
   $context = Get-Content .deployment-context.json | ConvertFrom-Json
   $connStr = "Server=tcp:$($context.sqlServerFqdn),1433;Initial Catalog=$($context.sqlDatabaseName);Authentication=Active Directory Managed Identity;User Id=$($context.managedIdentityClientId);"
   
   az webapp config connection-string set `
     --name $context.webAppName `
     --resource-group $context.resourceGroup `
     --connection-string-type SQLAzure `
     --settings DefaultConnection="$connStr"
   ```

## 📊 CI/CD Integration

This deployment script is also used by the GitHub Actions workflow. See [../.github/CICD-SETUP.md](../.github/CICD-SETUP.md) for CI/CD configuration.

## 🔐 Security Notes

- The script does NOT deploy secrets or connection strings by default
- Connection strings are configured during infrastructure deployment
- Use `-ConfigureSettings` only if you need to reconfigure after deployment
- Never commit the `.deployment-context.json` file to version control (it's in `.gitignore`)

## 📖 Related Documentation

- [Infrastructure Deployment Guide](../deploy-infra/README.md)
- [Main README](../README.md)
- [Architecture Overview](../ARCHITECTURE.md)
- [CI/CD Setup Guide](../.github/CICD-SETUP.md)

## 🔄 Deployment Flow

The complete deployment flow is:

1. **Infrastructure Deployment** (`deploy-infra/deploy.ps1`)
   - Creates all Azure resources
   - Configures SQL Database
   - Sets up App Service settings
   - Saves deployment context

2. **Application Deployment** (`deploy-app/deploy.ps1`) ← You are here
   - Builds the application
   - Creates deployment package
   - Deploys to App Service

3. **Access Application**
   - Navigate to `/Index` to use the app
   - Navigate to `/swagger` for API docs
   - Navigate to `/Chat` for AI assistant (if GenAI deployed)

---

**Next Steps**: After deployment completes, access your application at the URLs shown in the script output.
