# Application Deployment

This folder contains scripts to deploy the Expense Management application code to Azure App Service.

## Prerequisites

- **Azure CLI**: [Install Azure CLI](https://aka.ms/installazurecliwindows)
- **.NET 8 SDK**: [Download .NET 8](https://dot.net)
- **PowerShell 7+**: [Download PowerShell](https://aka.ms/powershell-release)
- **Infrastructure Deployed**: Run `deploy-infra/deploy.ps1` first

## Quick Start

### Automatic Deployment (Recommended)

After running the infrastructure deployment, simply run:

```powershell
.\deploy-app\deploy.ps1
```

The script automatically reads deployment context from `.deployment-context.json` created by the infrastructure script.

### Manual Deployment

If you need to specify parameters manually:

```powershell
.\deploy-app\deploy.ps1 -ResourceGroup "rg-expensemgmt-20241206" -WebAppName "app-expensemgmt-xyz123"
```

## What the Script Does

1. ✅ Reads deployment context from `.deployment-context.json` (if available)
2. ✅ Validates Azure CLI and .NET SDK installation
3. ✅ Checks Azure login status
4. ✅ Builds the .NET application with `dotnet publish`
5. ✅ Creates a deployment zip package with correct structure
6. ✅ Deploys to Azure App Service using `az webapp deploy`
7. ✅ Cleans up temporary files
8. ✅ Displays application URLs

## Script Parameters

| Parameter | Required | Default | Description |
|-----------|----------|---------|-------------|
| `ResourceGroup` | No* | From context | Azure resource group name |
| `WebAppName` | No* | From context | Azure Web App name |
| `SkipBuild` | No | false | Skip the build step (for redeployments) |

\* Required if `.deployment-context.json` doesn't exist

## Deployment Package Structure

The script creates a zip file with the DLL files at the root level (not in a subdirectory). This is critical for Azure App Service:

```
app-deployment.zip
├── ExpenseManagement.dll
├── appsettings.json
├── web.config
└── ... other files
```

## Application URLs

After deployment, access the application at:

- **Main Application**: `https://your-app-name.azurewebsites.net/Index`
- **API Documentation**: `https://your-app-name.azurewebsites.net/swagger`

## Redeployment

For quick redeployments without rebuilding:

```powershell
# Skip the build step if code hasn't changed
.\deploy-app\deploy.ps1 -SkipBuild
```

## Configuration

The application uses these configuration sources (in order of precedence):

1. **Azure App Service Configuration** (highest priority)
   - Set by `deploy-infra/deploy.ps1`
   - Connection strings, managed identity settings

2. **appsettings.json**
   - Default local development settings

3. **appsettings.Development.json**
   - Development-specific overrides

## Environment Variables (Set by Infrastructure Deployment)

The following settings are configured automatically during infrastructure deployment:

- `AZURE_CLIENT_ID` - Managed identity client ID
- `ManagedIdentityClientId` - Managed identity client ID (for GenAI)
- `ConnectionStrings__DefaultConnection` - SQL connection string
- `APPLICATIONINSIGHTS_CONNECTION_STRING` - Application Insights telemetry
- `OpenAI__Endpoint` - Azure OpenAI endpoint (if GenAI deployed)
- `OpenAI__DeploymentName` - Azure OpenAI model name (if GenAI deployed)
- `AzureSearch__Endpoint` - Azure AI Search endpoint (if GenAI deployed)

## Local Development

To run the application locally:

```powershell
cd src/ExpenseManagement
dotnet run
```

Then navigate to `https://localhost:5001/Index`

For local development with Azure SQL, configure `appsettings.Development.json` with your SQL server details and use `Authentication=Active Directory Default` in the connection string (requires `az login`).

## Troubleshooting

### Issue: "Project file not found"

**Solution**: Ensure you're running the script from the repository root directory.

### Issue: "Failed to build application"

**Solution**: Check .NET SDK installation with `dotnet --version`. Ensure it's version 8.0 or later.

### Issue: "Not logged in to Azure"

**Solution**: Run `az login` and try again.

### Issue: Application shows errors after deployment

**Solutions**:
1. Check Application Insights logs in Azure Portal
2. Verify connection string is configured correctly
3. Ensure managed identity has database permissions
4. Wait a minute for the app to fully start

### Issue: API returns 404

**Solution**: The API endpoints are under `/api/`. Check Swagger docs at `/swagger` for correct URLs.

## Continuous Integration

For automated deployments via GitHub Actions, see `.github/workflows/deploy.yml` and `.github/CICD-SETUP.md`.

## Next Steps

After deployment:

1. Visit the main application: `https://your-app-name.azurewebsites.net/Index`
2. Test the Add Expense feature
3. View expenses list
4. Test the approval workflow
5. Explore the API with Swagger

## Support

For issues or questions:
- Check the troubleshooting section above
- Review Application Insights logs in Azure Portal
- Check the infrastructure deployment README for configuration issues
