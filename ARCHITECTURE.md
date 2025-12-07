# Expense Management System - Architecture

## Overview

This is a modern, cloud-native expense management application built on Azure using ASP.NET 8 with a focus on security and maintainability.

## Architecture Diagram

```
┌─────────────────────────────────────────────────────────────────┐
│                         Azure Cloud                              │
│                                                                   │
│  ┌────────────────┐                                             │
│  │  Azure App     │                                             │
│  │  Service       │──────────────────┐                          │
│  │  (Linux/       │                  │                          │
│  │   .NET 8)      │                  │                          │
│  └────────┬───────┘                  │                          │
│           │                          │                          │
│           │ User-Assigned            │                          │
│           │ Managed Identity         │                          │
│           │                          │                          │
│  ┌────────▼────────┐      ┌──────────▼──────────┐              │
│  │  Azure SQL      │      │  Application        │              │
│  │  Database       │      │  Insights           │              │
│  │  (Entra ID      │      │  + Log Analytics    │              │
│  │   Auth Only)    │      └─────────────────────┘              │
│  └─────────────────┘                                            │
│                                                                  │
│  ┌─────────────────────────────────────┐  (Optional with       │
│  │   Azure OpenAI (Sweden Central)     │   -DeployGenAI)       │
│  │   + Azure AI Search                 │                       │
│  └─────────────────────────────────────┘                       │
└─────────────────────────────────────────────────────────────────┘
```

## Key Components

### 1. Azure App Service
- **Runtime**: .NET 8 on Linux
- **Tier**: Standard S1 (no cold starts)
- **Authentication**: User-Assigned Managed Identity
- **Features**:
  - ASP.NET Razor Pages for UI
  - REST API with Swagger documentation
  - AI Chat interface (when GenAI deployed)

### 2. Azure SQL Database
- **Tier**: Basic (cost-effective for dev/test)
- **Security**: Entra ID-only authentication (no SQL passwords)
- **Access**: Managed Identity with db_datareader, db_datawriter, EXECUTE permissions
- **Data Access**: All operations through stored procedures

### 3. Monitoring Stack
- **Application Insights**: Application telemetry and performance monitoring
- **Log Analytics**: Centralized log storage and querying
- **Diagnostics**: Configured for App Service and SQL Database

### 4. GenAI Resources (Optional)
- **Azure OpenAI**: GPT-4o model in Sweden Central for better quota
- **Azure AI Search**: RAG support for enhanced AI responses
- **Authentication**: Managed Identity with "Cognitive Services OpenAI User" role

## Security Architecture

### Zero Secrets Design

✅ **No passwords anywhere:**
- SQL uses Entra ID authentication
- Azure OpenAI uses Managed Identity
- GitHub Actions uses OIDC
- No connection strings with secrets

### Managed Identity Flow

```
App Service → Managed Identity → Azure SQL
                ↓
            Azure OpenAI
                ↓
            AI Search
```

### Connection String Format

```
Server=tcp:{server}.database.windows.net,1433;
Initial Catalog=Northwind;
Encrypt=True;
Connection Timeout=30;
Authentication=Active Directory Managed Identity;
User Id={managed-identity-client-id};
```

## Data Flow

### 1. User Request
Browser → App Service → Razor Page

### 2. Data Access
Razor Page → Service Layer → Stored Procedure → SQL Database

### 3. AI Chat (Optional)
User Message → Chat Service → Azure OpenAI → Function Calling → API → Database

## Deployment Architecture

### Two-Phase Deployment

**Phase 1: Infrastructure (`deploy-infra/deploy.ps1`)**
- Creates all Azure resources
- Imports database schema
- Configures managed identity permissions
- Sets App Service configuration
- Saves deployment context

**Phase 2: Application (`deploy-app/deploy.ps1`)**
- Builds .NET application
- Creates deployment package
- Deploys to App Service
- Reads deployment context automatically

**Unified: (`deploy-all.ps1`)**
- Orchestrates both phases
- Single command deployment

## Technology Stack

- **Backend**: ASP.NET 8, C#
- **Frontend**: Razor Pages, Bootstrap 5, jQuery
- **Database**: Azure SQL Database
- **API**: REST with Swagger/OpenAPI
- **AI**: Azure OpenAI GPT-4o
- **IaC**: Bicep
- **CI/CD**: GitHub Actions with OIDC
- **Monitoring**: Application Insights, Log Analytics

## Best Practices Implemented

### Azure Architecture
- Managed Identities for all authentication
- Least privilege access (RBAC)
- Entra ID-only authentication
- Regional placement (UK South for main resources, Sweden Central for OpenAI)

### Development
- Stored procedures for all data access
- API-first design
- Swagger documentation
- Error handling with graceful degradation

### DevOps
- Infrastructure as Code (Bicep)
- Automated deployment scripts
- CI/CD with OIDC (no secrets)
- Deployment context file for seamless handoff

## Cost Optimization

- **SQL Basic tier**: £4/month
- **App Service S1**: ~£50/month
- **Application Insights**: Pay-as-you-go
- **Azure OpenAI** (optional): Pay-per-token

Total estimated cost: ~£55-80/month (without GenAI)

## Scalability

- App Service can scale horizontally
- SQL Database can upgrade to higher tiers
- Application Insights auto-scales
- OpenAI has flexible token limits

## References

- [Azure App Service Best Practices](https://learn.microsoft.com/en-us/azure/app-service/app-service-best-practices)
- [Azure SQL Security](https://learn.microsoft.com/en-us/azure/azure-sql/database/security-best-practice)
- [Managed Identities](https://learn.microsoft.com/en-us/azure/active-directory/managed-identities-azure-resources/overview)
