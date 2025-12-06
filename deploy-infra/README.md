# Infrastructure Deployment

This folder contains the infrastructure-as-code for deploying the Expense Management application to Azure.

## Prerequisites

- **Azure CLI**: [Install Azure CLI](https://aka.ms/installazurecliwindows)
- **PowerShell 7+**: [Download PowerShell](https://aka.ms/powershell-release) (recommended)
- **sqlcmd (go-sqlcmd)**: Install with `winget install sqlcmd` or [download from GitHub](https://github.com/microsoft/go-sqlcmd/releases)
- **Azure Subscription**: With permissions to create resources

## Quick Start

### 1. Login to Azure

```powershell
az login
```

Set your subscription if you have multiple:

```powershell
az account set --subscription "Your Subscription Name"
```

### 2. Deploy Infrastructure

Deploy the complete infrastructure with one command:

```powershell
.\deploy-infra\deploy.ps1 -ResourceGroup "rg-expensemgmt-20241206" -Location "uksouth"
```

### 3. Deploy with GenAI (Optional)

To include Azure OpenAI and AI Search for the chat interface:

```powershell
.\deploy-infra\deploy.ps1 -ResourceGroup "rg-expensemgmt-20241206" -Location "uksouth" -DeployGenAI
```

## What Gets Deployed

### Core Infrastructure

- **App Service** (Standard S1, Linux, .NET 8)
- **Azure SQL Database** (Basic tier, Entra ID-only authentication)
- **User-Assigned Managed Identity** (for secure authentication)
- **Log Analytics Workspace** (centralized logging)
- **Application Insights** (application telemetry)

### Optional GenAI Resources (with -DeployGenAI)

- **Azure OpenAI** (GPT-4o model in Sweden Central)
- **Azure AI Search** (Basic tier)

## Script Parameters

| Parameter | Required | Default | Description |
|-----------|----------|---------|-------------|
| `ResourceGroup` | Yes | - | Name of the Azure resource group |
| `Location` | Yes | - | Azure region (e.g., 'uksouth', 'eastus') |
| `BaseName` | No | 'expensemgmt' | Base name for resources |
| `DeployGenAI` | No | false | Deploy Azure OpenAI and AI Search |
| `SkipDatabaseSetup` | No | false | Skip database schema import |

## What the Script Does

1. ✅ Validates Azure CLI and login status
2. ✅ Retrieves current user credentials (or Service Principal in CI/CD)
3. ✅ Creates resource group if needed
4. ✅ Deploys all infrastructure using Bicep templates
5. ✅ Waits for SQL Server to become ready
6. ✅ Adds your IP address to SQL Server firewall (interactive mode only)
7. ✅ Imports database schema from `Database-Schema/database_schema.sql`
8. ✅ Creates managed identity database user with proper permissions
9. ✅ Imports stored procedures from `stored-procedures.sql`
10. ✅ Configures App Service with connection string and settings
11. ✅ Configures GenAI settings (if deployed)
12. ✅ Saves deployment context to `.deployment-context.json`

## Deployment Context File

The script creates a `.deployment-context.json` file at the repository root containing:

```json
{
  "resourceGroup": "rg-expensemgmt-20241206",
  "location": "uksouth",
  "webAppName": "app-expensemgmt-xyz123",
  "sqlServerFqdn": "sql-expensemgmt-xyz123.database.windows.net",
  "databaseName": "Northwind",
  "managedIdentityClientId": "guid-here",
  "deployedGenAI": false,
  "deploymentDate": "2024-12-06 15:30:00"
}
```

This file is used by the application deployment script (`deploy-app/deploy.ps1`) for seamless deployment.

## Resource Naming

Resources are named using this pattern:

- App Service Plan: `asp-{baseName}-{uniqueSuffix}`
- App Service: `app-{baseName}-{uniqueSuffix}`
- SQL Server: `sql-{baseName}-{uniqueSuffix}`
- Managed Identity: `mid-{baseName}-{timestamp}`
- Log Analytics: `law-{baseName}-{uniqueSuffix}`
- Application Insights: `appi-{baseName}-{uniqueSuffix}`
- Azure OpenAI: `aoai-{baseName}-{uniqueSuffix}`
- Azure AI Search: `srch-{baseName}-{uniqueSuffix}`

All names are lowercase to comply with Azure naming requirements.

## Best Practices

### ✅ Use Unique Resource Group Names

Always include a date or timestamp in your resource group name:

```powershell
# Good
-ResourceGroup "rg-expensemgmt-20241206"

# Avoid reusing names
-ResourceGroup "rg-expensemgmt"  # Can cause ARM caching issues
```

### ✅ Fresh Deployments

If a deployment fails, delete the entire resource group and start fresh:

```powershell
az group delete --name "rg-expensemgmt-20241206" --yes
```

### ✅ Check sqlcmd Version

Ensure you're using the modern go-sqlcmd, not the legacy ODBC version:

```powershell
sqlcmd --version
# Should show: sqlcmd (go-sqlcmd) version X.Y.Z
```

If you see errors about unrecognized arguments, restart VS Code or run from a standalone PowerShell terminal.

## Security Features

### Entra ID-Only Authentication

The SQL Server uses Entra ID (Azure AD) authentication only - no SQL passwords:

- ✅ Complies with security policies
- ✅ Uses your Azure credentials for admin access
- ✅ Managed identity for application access

### Managed Identity

The application uses a user-assigned managed identity to:

- ✅ Connect to Azure SQL Database
- ✅ Access Azure OpenAI (if deployed)
- ✅ Access Azure AI Search (if deployed)

No connection strings, passwords, or API keys are stored in the application.

## Troubleshooting

### Issue: "Not logged in to Azure"

**Solution**: Run `az login` and try again.

### Issue: "sqlcmd: command not found"

**Solution**: Install sqlcmd with `winget install sqlcmd`

### Issue: sqlcmd errors about unrecognized arguments

**Solution**: VS Code may be using the legacy ODBC sqlcmd. Either:
- Restart VS Code completely
- Run from a standalone PowerShell terminal

### Issue: "Could not retrieve the Log Analytics workspace from ARM"

**Solution**: This is caused by reusing a resource group. Delete the resource group and use a fresh name.

### Issue: Firewall blocking database access

**Solution**: The script automatically adds your IP. If it fails, manually add it:

```powershell
az sql server firewall-rule create `
  --resource-group "rg-expensemgmt-20241206" `
  --server "sql-expensemgmt-xyz123" `
  --name "MyIP" `
  --start-ip-address "YOUR_IP" `
  --end-ip-address "YOUR_IP"
```

## Bicep Templates

The infrastructure is defined in Bicep templates:

- **main.bicep**: Main orchestration template
- **main.bicepparam**: Parameter file
- **modules/managed-identity.bicep**: User-assigned managed identity
- **modules/app-service.bicep**: App Service and App Service Plan
- **modules/azure-sql.bicep**: SQL Server and database
- **modules/monitoring.bicep**: Log Analytics and Application Insights
- **modules/genai.bicep**: Azure OpenAI and AI Search

### Validate Templates

Before deploying, you can validate the Bicep templates:

```powershell
az deployment group validate `
  --resource-group "rg-expensemgmt-20241206" `
  --template-file ./deploy-infra/main.bicep `
  --parameters location=uksouth baseName=expensemgmt adminObjectId="guid" adminUsername="user@domain.com"
```

## CI/CD Support

The deployment script supports both local and CI/CD environments:

- **Local**: Uses your Azure CLI credentials (`az login`)
- **CI/CD**: Uses OIDC authentication with Service Principal

See `.github/CICD-SETUP.md` for CI/CD configuration.

## Next Steps

After infrastructure deployment completes:

1. Deploy the application code: `.\deploy-app\deploy.ps1`
2. Access the application at: `https://app-expensemgmt-xyz123.azurewebsites.net/Index`
3. View Application Insights in the Azure Portal for monitoring

## Support

For issues or questions:
- Check the troubleshooting section above
- Review `prompt-023-deployment-order-considerations` for common problems
- Check Azure Portal for resource status and logs
