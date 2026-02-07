# Infrastructure Deployment

This folder contains the infrastructure deployment automation for the Expense Management application.

## Prerequisites

- **Azure CLI** - [Install Azure CLI](https://aka.ms/azure-cli)
- **sqlcmd (go-sqlcmd)** - Install with `winget install sqlcmd` (Windows) or download from [GitHub releases](https://github.com/microsoft/go-sqlcmd/releases)
- **PowerShell 7+** - Recommended for best compatibility
- **Azure subscription** - With Contributor role

## Quick Start

### 1. Login to Azure

```powershell
az login
```

### 2. Run the deployment script

**Basic deployment (without GenAI):**

```powershell
.\deploy-infra\deploy.ps1 -ResourceGroup "rg-expensemgmt-20260207" -Location "uksouth"
```

**With GenAI resources (Azure OpenAI, AI Search):**

```powershell
.\deploy-infra\deploy.ps1 -ResourceGroup "rg-expensemgmt-20260207" -Location "uksouth" -DeployGenAI
```

**Custom base name:**

```powershell
.\deploy-infra\deploy.ps1 -ResourceGroup "rg-expensemgmt-20260207" -Location "uksouth" -BaseName "myapp"
```

## What Gets Deployed

### Core Infrastructure (Always)

- **App Service** (Linux, .NET 8, Free tier)
- **Azure SQL Database** (Basic tier, Entra ID-only authentication)
- **User-Assigned Managed Identity** (zero secrets architecture)
- **Application Insights** + **Log Analytics Workspace** (monitoring)

### Optional GenAI Resources (`-DeployGenAI` switch)

- **Azure OpenAI** (GPT-4o model, Sweden Central)
- **Azure AI Search** (Basic tier)

## What the Script Does

1. ✓ Validates Azure CLI and sqlcmd are installed
2. ✓ Authenticates to Azure (checks `az login` status)
3. ✓ Retrieves current user credentials (Object ID, UPN)
4. ✓ Creates resource group (if needed)
5. ✓ Deploys Bicep infrastructure (App Service, SQL, Monitoring, etc.)
6. ✓ Waits for SQL Server to be ready
7. ✓ Adds your IP to SQL firewall (local mode only)
8. ✓ Imports database schema (`Database-Schema/database_schema.sql`)
9. ✓ Creates managed identity database user (SID-based, no Directory Reader required)
10. ✓ Imports stored procedures (`stored-procedures.sql`)
11. ✓ Configures App Service settings (connection string, managed identity, Application Insights)
12. ✓ If GenAI deployed: Configures OpenAI endpoint and model name
13. ✓ Creates `.deployment-context.json` for application deployment handoff

## Parameters

| Parameter | Required | Default | Description |
|-----------|----------|---------|-------------|
| `-ResourceGroup` | Yes | - | Resource group name. **Use fresh names with timestamps** (e.g., `rg-expensemgmt-20260207`) |
| `-Location` | Yes | - | Azure region (e.g., `uksouth`, `eastus`, `westeurope`) |
| `-BaseName` | No | `expensemgmt` | Base name for resource naming |
| `-DeployGenAI` | No | `false` | Switch to deploy GenAI resources (Azure OpenAI, AI Search) |

## Important Notes

### Resource Group Naming

**Always use fresh resource group names** with timestamps to avoid ARM caching issues:

```powershell
# ✅ Good
-ResourceGroup "rg-expensemgmt-20260207"

# ❌ Avoid
-ResourceGroup "rg-expensemgmt"  # Reusing names can cause deployment errors
```

### Authentication Methods

The script automatically detects the environment and uses the appropriate authentication:

| Environment | SQL Admin Type | sqlcmd Auth Method |
|-------------|----------------|-------------------|
| **Local (interactive)** | User | `ActiveDirectoryDefault` |
| **CI/CD (GitHub Actions)** | Service Principal | `ActiveDirectoryAzCli` |

### sqlcmd Path Issues

VS Code's integrated terminal may use a cached PATH pointing to legacy ODBC sqlcmd. If you encounter errors:

1. Restart VS Code completely, OR
2. Run the script from a standalone PowerShell terminal

### Azure Policy Timing

If your subscription has governance policies, Azure may create policy-related deployments that fail transiently. The script handles this automatically by:

1. Waiting for policy deployments to settle
2. Finding the successful main deployment
3. Continuing with database setup

## Output: Deployment Context File

The script creates `.deployment-context.json` at the repository root:

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
  "deployGenAI": true,
  "openAIEndpoint": "https://oai-expensemgmt-abc123.openai.azure.com/",
  "openAIModelName": "gpt-4o",
  "timestamp": "2026-02-07T10:00:00Z"
}
```

This file is read by `deploy-app/deploy.ps1` for seamless application deployment.

## Next Steps

After infrastructure deployment succeeds:

1. **Deploy the application:**
   ```powershell
   .\deploy-app\deploy.ps1
   ```

2. **Or use the unified script to do both:**
   ```powershell
   .\deploy-all.ps1 -ResourceGroup "rg-expensemgmt-20260207" -Location "uksouth" -DeployGenAI
   ```

## Troubleshooting

### "Azure CLI is not installed"

Install from: https://aka.ms/azure-cli

### "sqlcmd is not installed"

```powershell
winget install sqlcmd
```

### "Not logged in to Azure"

```powershell
az login
```

### "Failed to import database schema"

- Check firewall rules allow your IP
- Verify sqlcmd is the modern go-sqlcmd (not legacy ODBC sqlcmd)
- Restart VS Code if using integrated terminal

### "Infrastructure deployment failed"

Check Azure Portal deployment history:
```powershell
az deployment group list --resource-group "rg-expensemgmt-20260207" --output table
```

## Files in This Folder

```
deploy-infra/
├── deploy.ps1           ← Main deployment script (run this)
├── README.md            ← This file
├── main.bicep           ← Main Bicep orchestrator
├── main.bicepparam      ← Bicep parameters file
└── modules/             ← Bicep modules for each resource type
    ├── app-service.bicep
    ├── app-service-diagnostics.bicep
    ├── azure-sql.bicep
    ├── sql-diagnostics.bicep
    ├── managed-identity.bicep
    ├── monitoring.bicep
    └── genai.bicep
```

## Manual Deployment (Alternative)

If you prefer manual steps over the automated script:

```powershell
# 1. Create resource group
az group create --name "rg-expensemgmt-20260207" --location "uksouth"

# 2. Deploy Bicep (replace Object ID and UPN with your values)
az deployment group create `
    --resource-group "rg-expensemgmt-20260207" `
    --template-file "./deploy-infra/main.bicep" `
    --parameters location=uksouth baseName=expensemgmt sqlAdminObjectId=YOUR_OBJECT_ID sqlAdminLogin=YOUR_UPN adminPrincipalType=User deployGenAI=false

# 3. Import schema
sqlcmd -S "sql-expensemgmt-abc123.database.windows.net" -d "Northwind" "--authentication-method=ActiveDirectoryDefault" -i "./Database-Schema/database_schema.sql"

# 4. Create managed identity user (requires Client ID and SID conversion)
# See deploy.ps1 for SID-based user creation logic

# 5. Import stored procedures
sqlcmd -S "sql-expensemgmt-abc123.database.windows.net" -d "Northwind" "--authentication-method=ActiveDirectoryDefault" -i "./stored-procedures.sql"

# 6. Configure App Service settings
# See deploy.ps1 for connection string and settings configuration
```

## Related Documentation

- [Application Deployment](../deploy-app/README.md)
- [Unified Deployment](../deploy-all.ps1)
- [CI/CD Setup](.github/CICD-SETUP.md)
- [Bicep Best Practices](../prompts/prompt-030-bicep-best-practices)
