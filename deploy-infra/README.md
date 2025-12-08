## Infrastructure Deployment

This folder provisions Azure resources for the Expense Management application.

### Prerequisites
- Azure CLI
- PowerShell 7+
- go-sqlcmd (`winget install sqlcmd`)

### Deploy

```powershell
.\deploy.ps1 -ResourceGroup "rg-expensemgmt-20251206" -Location "uksouth"
```

With GenAI:

```powershell
.\deploy.ps1 -ResourceGroup "rg-expensemgmt-20251206" -Location "uksouth" -DeployGenAI
```

Parameters:
- `ResourceGroup` (required): unique name for the resource group.
- `Location` (required): Azure region, e.g., `uksouth`.
- `BaseName` (optional): base name for resources (default `expensemgmt`).
- `DeployGenAI` (switch): deploy Azure OpenAI + AI Search.
- `SkipDatabaseSetup` (switch): skip schema + stored procedure import.

### What the script does
1. Detects whether it is running in CI or locally.
2. Retrieves the correct Entra ID admin (user or service principal).
3. Deploys `main.bicep` with App Service, SQL, managed identity, monitoring, and optional GenAI.
4. Imports `Database-Schema/database_schema.sql`.
5. Deploys `stored-procedures.sql`.
6. Creates a database user for the managed identity using SID-based creation.
7. Configures required App Service settings:
   - `ConnectionStrings__DefaultConnection`
   - `AZURE_CLIENT_ID`
   - `ManagedIdentityClientId`
   - `APPLICATIONINSIGHTS_CONNECTION_STRING`
   - `GenAISettings__OpenAIEndpoint` / `GenAISettings__OpenAIModelName` (when `-DeployGenAI` is used)
8. Saves `.deployment-context.json` at the repo root for app deployment.

### Validate Bicep

```powershell
az deployment group validate `
  --resource-group "rg-test" `
  --template-file ./main.bicep `
  --parameters location=uksouth baseName=expensemgmt adminObjectId="guid" adminUserPrincipalName="user@domain.com"
```
