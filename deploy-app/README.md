## Application Deployment

This script publishes the ASP.NET 8 Razor Pages app and deploys it to Azure App Service.

### Prerequisites
- Azure CLI
- PowerShell 7+
- .NET 8 SDK

### Usage

After running infrastructure deployment, simply run:

```powershell
.\deploy.ps1
```

The script reads `.deployment-context.json` from the repo root (or this folder) for the resource group and web app name.

Optional parameters:
- `-ResourceGroup`: override the resource group from the context file.
- `-WebAppName`: override the web app name from the context file.
- `-SkipBuild`: reuse an existing publish folder.
- `-ConfigureSettings`: re-apply app settings from the context file.

### What it does
1. Builds and publishes the app to a temporary folder.
2. Creates a ZIP package with DLLs at the root level.
3. Deploys using `az webapp deploy --clean --restart`.
4. Cleans up temporary files.
5. Prints the application URLs:
   - `https://<webapp>.azurewebsites.net/Index`
   - `https://<webapp>.azurewebsites.net/swagger`
   - `https://<webapp>.azurewebsites.net/Chat`
