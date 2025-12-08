# Infrastructure Deployment

This folder contains all the Azure infrastructure deployment scripts and Bicep templates.

## Prerequisites

- Azure CLI installed and configured
- PowerShell 7+ recommended (PowerShell 5.1 works but with warnings)
- go-sqlcmd installed (`winget install sqlcmd` on Windows)
- Azure subscription with sufficient permissions

## Quick Start

```powershell
# Basic deployment
.\deploy.ps1 -ResourceGroup "rg-expensemgmt-20251208" -Location "uksouth"

# With GenAI resources (Azure OpenAI and AI Search)
.\deploy.ps1 -ResourceGroup "rg-expensemgmt-20251208" -Location "uksouth" -DeployGenAI
```

## Parameters

| Parameter | Required | Default | Description |
|-----------|----------|---------|-------------|
| ResourceGroup | Yes | - | Name of the Azure resource group |
| Location | Yes | - | Azure region (e.g., 'uksouth', 'eastus') |
| BaseName | No | 'expensemgmt' | Base name for resources |
| DeployGenAI | No | false | Deploy Azure OpenAI and AI Search |
| SkipDatabaseSetup | No | false | Skip database schema import |

## What Gets Deployed

### Base Infrastructure
- **Managed Identity**: User-assigned identity for secure authentication
- **App Service Plan**: Standard S1 tier
- **App Service**: .NET 8 web application
- **Azure SQL Server**: With Entra ID-only authentication
- **Azure SQL Database**: Northwind database (Basic tier)
- **Log Analytics Workspace**: Central logging
- **Application Insights**: Application telemetry

### With -DeployGenAI Flag
- **Azure OpenAI**: GPT-4o model in Sweden Central
- **Azure AI Search**: Basic tier for intelligent search

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                      Azure Resource Group                     │
│                                                               │
│  ┌─────────────┐    ┌──────────────┐    ┌─────────────────┐ │
│  │   App       │    │   Managed    │    │   Azure SQL     │ │
│  │   Service   │───▶│   Identity   │───▶│   Database      │ │
│  └─────────────┘    └──────────────┘    └─────────────────┘ │
│         │                   │                                 │
│         │                   │           ┌─────────────────┐ │
│         │                   └──────────▶│   Azure OpenAI  │ │
│         │                               │   (optional)    │ │
│         ▼                               └─────────────────┘ │
│  ┌─────────────┐                                             │
│  │ Application │                                             │
│  │  Insights   │                                             │
│  └─────────────┘                                             │
└─────────────────────────────────────────────────────────────┘
```

## Deployment Context

After successful deployment, a `.deployment-context.json` file is created at the repository root containing all the configuration values needed for application deployment.

## Troubleshooting

### SQL Server Connection Issues
- Ensure your IP is added to the SQL Server firewall
- The deployment script adds your IP automatically
- For CI/CD, Azure services are allowed by default

### sqlcmd Errors
- Install the modern go-sqlcmd: `winget install sqlcmd`
- Restart VS Code if using the integrated terminal
- The --authentication-method argument must be quoted in PowerShell

### Resource Group Reuse
- Always use a unique resource group name (include date/time)
- If deployment fails, delete the resource group before retrying
