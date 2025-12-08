# Azure Architecture Diagram

This document describes the Azure services deployed by the Expense Management application.

## Architecture Overview

```
┌──────────────────────────────────────────────────────────────────────────────┐
│                            Azure Resource Group                               │
│                                                                               │
│  ┌────────────────────────────────────────────────────────────────────────┐  │
│  │                         Core Infrastructure                            │  │
│  │                                                                        │  │
│  │   ┌─────────────────┐                    ┌─────────────────────────┐  │  │
│  │   │  User-Assigned  │                    │      Azure SQL          │  │  │
│  │   │    Managed      │◄──── Entra ID ────►│      Database           │  │  │
│  │   │    Identity     │     Auth           │    (Northwind)          │  │  │
│  │   └────────┬────────┘                    └─────────────────────────┘  │  │
│  │            │                                         ▲                 │  │
│  │            │ Assigned to                             │ SQL Connection  │  │
│  │            ▼                                         │                 │  │
│  │   ┌─────────────────┐                               │                 │  │
│  │   │   Azure App     │───────────────────────────────┘                 │  │
│  │   │    Service      │                                                  │  │
│  │   │   (.NET 8.0)    │                                                  │  │
│  │   │                 │───────────────┐                                  │  │
│  │   └─────────────────┘               │                                  │  │
│  │            │                        │                                  │  │
│  └────────────│────────────────────────│──────────────────────────────────┘  │
│               │                        │                                      │
│  ┌────────────│────────────────────────│──────────────────────────────────┐  │
│  │            │      Monitoring        │                                  │  │
│  │            ▼                        ▼                                  │  │
│  │   ┌─────────────────┐    ┌─────────────────┐                          │  │
│  │   │  Application    │    │  Log Analytics  │                          │  │
│  │   │   Insights      │───►│   Workspace     │                          │  │
│  │   │  (Telemetry)    │    │   (Logs)        │                          │  │
│  │   └─────────────────┘    └─────────────────┘                          │  │
│  │                                                                        │  │
│  └────────────────────────────────────────────────────────────────────────┘  │
│                                                                               │
│  ┌────────────────────────────────────────────────────────────────────────┐  │
│  │              GenAI Resources (Optional - with -DeployGenAI)            │  │
│  │                                                                        │  │
│  │   ┌─────────────────┐                    ┌─────────────────────────┐  │  │
│  │   │  Azure OpenAI   │                    │    Azure AI Search     │  │  │
│  │   │   (GPT-4o)      │                    │                         │  │  │
│  │   │ Sweden Central  │                    │                         │  │  │
│  │   └────────┬────────┘                    └─────────────────────────┘  │  │
│  │            │                                                           │  │
│  │            │ Cognitive Services OpenAI User Role                       │  │
│  │            │ (Managed Identity has access)                             │  │
│  │            ▼                                                           │  │
│  │   ┌─────────────────┐                                                  │  │
│  │   │   Chat UI       │ Uses function calling to interact with           │  │
│  │   │   (App Service) │ the expense database through APIs                │  │
│  │   └─────────────────┘                                                  │  │
│  │                                                                        │  │
│  └────────────────────────────────────────────────────────────────────────┘  │
│                                                                               │
└──────────────────────────────────────────────────────────────────────────────┘
```

## Data Flow

1. **User Access**: Users access the application through the Azure App Service
2. **Authentication**: The App Service uses a User-Assigned Managed Identity for all Azure service authentication
3. **Database**: SQL queries go through stored procedures to Azure SQL Database using Entra ID authentication
4. **Telemetry**: Application logs and metrics flow to Application Insights and Log Analytics
5. **AI Chat** (optional): When GenAI is deployed, the Chat page connects to Azure OpenAI using the Managed Identity

## Security Features

- **Entra ID Only Authentication**: SQL Server is configured for Azure AD-only auth (no SQL passwords)
- **Managed Identity**: All service-to-service communication uses managed identity
- **HTTPS Only**: App Service enforces HTTPS
- **Minimum TLS 1.2**: All services require TLS 1.2 or higher

## Resource SKUs

| Resource | SKU | Purpose |
|----------|-----|---------|
| App Service Plan | Standard S1 | Production-ready, always-on |
| Azure SQL | Basic | Development/testing |
| Log Analytics | Per-GB | Pay for what you use |
| Application Insights | Standard | Integrated with Log Analytics |
| Azure OpenAI | S0 | Standard pricing |
| Azure AI Search | Basic | Basic search capabilities |
