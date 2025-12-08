# Expense Management - Azure Architecture

## Architecture Diagram

```
┌─────────────────────────────────────────────────────────────────────────┐
│                                                                         │
│                          User / Browser                                 │
│                                                                         │
└────────────────────────────┬────────────────────────────────────────────┘
                             │
                             │ HTTPS
                             ▼
┌─────────────────────────────────────────────────────────────────────────┐
│                                                                         │
│                     Azure App Service (S1)                              │
│                      .NET 8 Web Application                             │
│                                                                         │
│  ┌──────────────────┐  ┌──────────────────┐  ┌──────────────────┐     │
│  │  Razor Pages     │  │  API Controllers │  │   Chat Service   │     │
│  │                  │  │                  │  │   (with GenAI)   │     │
│  │  - Index         │  │  - Expenses API  │  │                  │     │
│  │  - AddExpense    │  │  - Categories    │  │  - Function      │     │
│  │  - Chat          │  │  - Users         │  │    Calling       │     │
│  │                  │  │  - Chat API      │  │                  │     │
│  └──────────────────┘  └──────────────────┘  └──────────────────┘     │
│                                                                         │
└──┬─────────────────┬────────────────────┬─────────────────┬───────────┘
   │                 │                    │                 │
   │ Managed         │ Managed            │ Logs &          │ Managed
   │ Identity        │ Identity           │ Metrics         │ Identity
   │ Auth            │ Auth               │                 │ Auth
   ▼                 ▼                    ▼                 ▼
┌──────────────┐  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐
│              │  │              │  │              │  │              │
│  Azure SQL   │  │ Application  │  │     Log      │  │ Azure OpenAI │
│  Database    │  │   Insights   │  │  Analytics   │  │  (GPT-4o)    │
│              │  │              │  │  Workspace   │  │              │
│  - Northwind │  │  - Telemetry │  │              │  │ (Optional)   │
│  - Stored    │  │  - APM       │  │  - Central   │  │              │
│    Procedures│  │  - Alerts    │  │    Logging   │  │              │
│              │  │              │  │              │  │              │
│  Entra ID    │  └──────────────┘  └──────────────┘  └──────┬───────┘
│  Only Auth   │                                             │
│              │                                             │
└──────────────┘                                             │
                                                             │
                                                             ▼
                                                    ┌──────────────┐
                                                    │              │
                                                    │ Azure AI     │
                                                    │ Search       │
                                                    │              │
                                                    │ (Optional)   │
                                                    │              │
                                                    └──────────────┘

┌─────────────────────────────────────────────────────────────────────────┐
│                                                                         │
│                    User-Assigned Managed Identity                       │
│                                                                         │
│  - Authenticates App Service to Azure SQL                              │
│  - Authenticates App Service to Azure OpenAI                           │
│  - Authenticates App Service to Azure AI Search                        │
│  - No secrets or connection strings required                           │
│                                                                         │
└─────────────────────────────────────────────────────────────────────────┘
```

## Component Descriptions

### Core Infrastructure (Always Deployed)

1. **Azure App Service (Standard S1)**
   - Hosts the .NET 8 web application
   - Linux-based with always-on enabled
   - HTTPS-only, TLS 1.2+ enforced
   - Configured with managed identity for authentication

2. **Azure SQL Database (Basic Tier)**
   - Database name: `Northwind`
   - Entra ID-only authentication (no SQL passwords)
   - Contains expense management schema
   - All access through stored procedures

3. **User-Assigned Managed Identity**
   - Single identity shared across services
   - Eliminates need for secrets/passwords
   - Grants database access (read, write, execute)
   - Grants OpenAI access (if deployed)

4. **Application Insights**
   - Real-time application monitoring
   - Request tracking and performance metrics
   - Exception and error logging
   - Custom telemetry for business metrics

5. **Log Analytics Workspace**
   - Centralized logging for all resources
   - Query across App Service, SQL, and application logs
   - Diagnostic data for troubleshooting
   - 30-day retention configured

### Optional GenAI Features (With -DeployGenAI)

6. **Azure OpenAI (GPT-4o)**
   - Deployed in Sweden Central (better quota availability)
   - Model: GPT-4o with capacity 8
   - Used for AI chat assistant
   - Function calling for database operations

7. **Azure AI Search (Basic Tier)**
   - Enhanced search capabilities
   - Integration with OpenAI for semantic search
   - Managed identity access

## Authentication Flow

### Application to Database

```
1. User → App Service (HTTPS request)
2. App Service → Uses Managed Identity credentials
3. Managed Identity → Authenticates to Azure SQL via Entra ID
4. Azure SQL → Verifies identity and grants access
5. App → Executes stored procedures
6. SQL → Returns results
7. App → Renders response to user
```

### Application to OpenAI (Chat Feature)

```
1. User → Chat page (sends message)
2. Chat Service → Uses Managed Identity credentials
3. Managed Identity → Authenticates to Azure OpenAI
4. Azure OpenAI → Processes request with GPT-4o
5. AI → May call functions to access database
6. Chat Service → Executes database queries via API
7. Chat Service → Returns formatted response
8. User → Receives AI-generated response
```

## Security Features

✅ **Managed Identities** - No passwords or connection strings
✅ **Entra ID Authentication** - Azure AD-only for SQL Server
✅ **HTTPS Only** - All traffic encrypted in transit
✅ **TLS 1.2+** - Modern encryption standards
✅ **Least Privilege** - Role-based access control
✅ **Stored Procedures** - SQL injection prevention
✅ **Network Security** - Azure service firewall rules
✅ **Audit Logging** - All access logged and monitored

## Data Flow

### Creating an Expense

```
1. User fills form on AddExpense page
2. Form posts to AddExpense.cshtml.cs page model
3. Page model calls ExpenseService.CreateExpenseAsync()
4. ExpenseService executes stored procedure 'CreateExpense'
5. Stored procedure inserts record into Expenses table
6. Success response returned to user
```

### AI Chat Query

```
1. User types "Show me all submitted expenses"
2. Chat page sends message to ChatController API
3. ChatController calls ChatService.SendMessageAsync()
4. ChatService sends to Azure OpenAI with function definitions
5. AI decides to call 'get_expenses_by_status' function
6. ChatService executes function (calls ExpenseService)
7. ExpenseService queries database via stored procedure
8. Results returned to AI
9. AI formats response in natural language
10. Formatted response displayed to user
```

## Monitoring and Observability

### Application Insights Captures

- HTTP requests (status, duration, dependencies)
- Exceptions and errors with stack traces
- Custom events and metrics
- Database query performance
- OpenAI API calls and latency

### Log Analytics Queries

```kusto
// Failed requests in last hour
requests
| where timestamp > ago(1h)
| where success == false
| project timestamp, name, resultCode, duration

// SQL database errors
AzureDiagnostics
| where Category == "Errors"
| project TimeGenerated, Message, ErrorNumber

// Application exceptions
exceptions
| where timestamp > ago(24h)
| summarize count() by type
```

## Deployment Architecture

The application follows a two-phase deployment:

### Phase 1: Infrastructure (deploy-infra/)
- Creates all Azure resources
- Configures managed identity permissions
- Sets up database schema and stored procedures
- Configures App Service settings
- Saves context file for Phase 2

### Phase 2: Application (deploy-app/)
- Builds .NET application
- Creates deployment package
- Deploys to App Service
- Reads configuration from Phase 1 context

## Scalability Considerations

Current configuration is suitable for:
- Development and testing
- Small to medium production workloads
- ~1,000 concurrent users

To scale up:
1. Upgrade App Service plan (P1V2 or higher)
2. Upgrade SQL Database tier (Standard S2+)
3. Add Azure Front Door for CDN/load balancing
4. Consider App Service autoscaling
5. Upgrade OpenAI model capacity

## Cost Estimation (UK South)

**Monthly costs (approximate):**

- App Service S1: ~£55/month
- SQL Database Basic: ~£4/month
- Application Insights: ~£2/month (first 5GB free)
- Log Analytics: ~£2/month (first 5GB free)

**With GenAI:**
- Azure OpenAI (GPT-4o, capacity 8): ~£400/month (usage-based)
- AI Search Basic: ~£60/month

**Total: ~£65/month (without GenAI), ~£525/month (with GenAI)**

## Backup and Disaster Recovery

- SQL Database: Automated backups (7-day retention in Basic tier)
- App Service: Source code in Git, infrastructure as code in Bicep
- Configuration: Stored in deployment-context.json and committed to repo
- Recovery Time Objective (RTO): ~30 minutes (full redeploy)
- Recovery Point Objective (RPO): ~5 minutes (SQL backup frequency)

## Regional Considerations

**App Service and SQL**: Deployed in specified region (default: UK South)

**Azure OpenAI**: Always deployed in Sweden Central
- Reason: Better quota availability for proof-of-concept
- Latency: Acceptable (<100ms) for chat interactions
- Data residency: Consider regulatory requirements

## Best Practices Implemented

✅ Infrastructure as Code (Bicep)
✅ Separation of infrastructure and application deployment
✅ Managed identities instead of secrets
✅ Stored procedures for database access
✅ Centralized logging and monitoring
✅ HTTPS and modern TLS everywhere
✅ Graceful degradation (dummy data on errors)
✅ Clear error messages for troubleshooting
✅ Comprehensive documentation
✅ CI/CD ready with GitHub Actions

## Resources

- [Azure App Service](https://learn.microsoft.com/azure/app-service/)
- [Azure SQL Database](https://learn.microsoft.com/azure/azure-sql/database/)
- [Azure OpenAI Service](https://learn.microsoft.com/azure/ai-services/openai/)
- [Managed Identities](https://learn.microsoft.com/azure/active-directory/managed-identities-azure-resources/)
- [Application Insights](https://learn.microsoft.com/azure/azure-monitor/app/app-insights-overview)
