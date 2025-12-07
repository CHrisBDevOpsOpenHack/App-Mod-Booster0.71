![Header image](https://github.com/DougChisholm/App-Mod-Booster/blob/main/repo-header-booster.png)

# Expense Management System

A modern, cloud-native expense management application built with ASP.NET 8 and deployed on Azure. This application demonstrates best practices for Azure App Service, Azure SQL Database with managed identity, and optional Azure OpenAI integration.

## 🌟 Features

- **📝 Expense Management**: Create, submit, and track business expenses
- **✅ Approval Workflow**: Managers can approve or reject submitted expenses
- **🔍 Search & Filter**: Easily find expenses by description, category, or user
- **📊 Dashboard**: View expense statistics at a glance
- **🔐 Secure Authentication**: Uses Azure Managed Identity (no passwords or keys)
- **📚 REST API**: Complete REST API with Swagger documentation
- **🤖 AI Chat (Optional)**: Natural language interface powered by Azure OpenAI
- **📈 Monitoring**: Application Insights and Log Analytics integration

## 🏗️ Architecture

The application uses a modern, secure Azure architecture:

- **Azure App Service**: Hosts the ASP.NET 8 Razor Pages application
- **Azure SQL Database**: Stores expense data with Entra ID-only authentication
- **Managed Identity**: Passwordless authentication to all Azure services
- **Application Insights**: Telemetry and performance monitoring
- **Azure OpenAI** (optional): GPT-4o model for chat interface
- **Azure AI Search** (optional): RAG support for enhanced AI responses

See [ARCHITECTURE.md](./ARCHITECTURE.md) for detailed architecture diagrams and explanations.

## 🚀 Quick Start

### Prerequisites

- **Azure Subscription**: With permissions to create resources
- **Azure CLI**: [Install Azure CLI](https://aka.ms/installazurecliwindows)
- **.NET 8 SDK**: [Download .NET 8](https://dot.net)
- **PowerShell 7+**: [Download PowerShell](https://aka.ms/powershell-release)
- **sqlcmd (go-sqlcmd)**: Install with `winget install sqlcmd`

### Step 1: Clone the Repository

```powershell
git clone https://github.com/YourOrg/App-Mod-Booster0.7.git
cd App-Mod-Booster0.7
```

### Step 2: Login to Azure

```powershell
az login
az account set --subscription "Your Subscription Name"
```

### Step 3: Deploy Everything (Single Command)

The easiest way to deploy is with the unified deployment script:

```powershell
.\deploy-all.ps1 -ResourceGroup "rg-expensemgmt-20251206" -Location "uksouth"
```

**With GenAI (Optional)**:
```powershell
.\deploy-all.ps1 -ResourceGroup "rg-expensemgmt-20251206" -Location "uksouth" -DeployGenAI
```

This deploys infrastructure and application in one step. For more control, you can deploy them separately (see below).

### Alternative: Deploy Separately

#### Step 3a: Deploy Infrastructure

```powershell
.\deploy-infra\deploy.ps1 -ResourceGroup "rg-expensemgmt-20251206" -Location "uksouth"
```

This deploys:
- Azure App Service (Standard S1)
- Azure SQL Database (Basic tier) with sample data
- User-Assigned Managed Identity
- Application Insights & Log Analytics
- Azure OpenAI + AI Search (if -DeployGenAI specified)

#### Step 3b: Deploy Application

```powershell
.\deploy-app\deploy.ps1
```

The script automatically reads the deployment context from the infrastructure deployment.

### Step 4: Access the Application

Open your browser to:
- **Main App**: `https://your-app-name.azurewebsites.net/Index`
- **AI Chat**: `https://your-app-name.azurewebsites.net/Chat` (if GenAI deployed)
- **API Docs**: `https://your-app-name.azurewebsites.net/swagger`

## 📖 Documentation

- **[Infrastructure Deployment](./deploy-infra/README.md)**: Detailed infrastructure deployment guide
- **[Application Deployment](./deploy-app/README.md)**: Application deployment instructions
- **[Architecture](./ARCHITECTURE.md)**: System architecture and design
- **[CI/CD Setup](./.github/CICD-SETUP.md)**: GitHub Actions with OIDC authentication
- **[Database Schema](./Database-Schema/database_schema.sql)**: SQL schema and sample data
- **[Stored Procedures](./stored-procedures.sql)**: Database stored procedures

## 🎯 Usage

### Add an Expense

1. Navigate to **Add Expense**
2. Fill in the amount, date, category, and description
3. Click **Submit**

### View Expenses

1. Navigate to **View Expenses**
2. Use the filter box to search expenses
3. View expense details in the table

### Approve Expenses (Manager)

1. Navigate to **Approve Expenses**
2. See all submitted expenses pending approval
3. Click **Approve** or **Reject** for each expense

### AI Chat Assistant (Optional - requires GenAI deployment)

1. Navigate to **AI Chat**
2. Type natural language questions or commands like:
   - "Show me all submitted expenses"
   - "Create an expense for £50 for lunch today"
   - "What categories are available?"
   - "Approve expense #5"
3. The AI assistant will execute operations and respond with formatted results

**Note**: The chat feature requires Azure OpenAI to be deployed. Use the `-DeployGenAI` switch during infrastructure deployment to enable this feature.

### API Access

Access the REST API at `/api/expenses`, `/api/categories`, etc.

Full API documentation available at `/swagger`.

## 🔐 Security Features

### Zero Secrets Architecture

✅ **No passwords** - Managed identity for SQL authentication  
✅ **No API keys** - Managed identity for OpenAI access  
✅ **No connection strings with secrets** - All use managed identity  
✅ **No secrets in code** - Configuration via Azure App Service settings  
✅ **No secrets in CI/CD** - OIDC authentication for GitHub Actions

### Compliance

- **Entra ID-only authentication** for SQL Server
- **TLS 1.2+** encryption for all connections
- **HTTPS only** for web traffic
- **Managed identities** for all Azure service authentication
- **RBAC** for fine-grained access control

## 🛠️ Development

### Local Development

1. Install prerequisites (.NET 8 SDK, Azure CLI)
2. Login to Azure: `az login`
3. Update `appsettings.Development.json` with your SQL server details
4. Run the application:

```powershell
cd src/ExpenseManagement
dotnet run
```

Navigate to `https://localhost:5001/Index`

### Project Structure

```
App-Mod-Booster0.7/
├── .github/
│   ├── workflows/          # GitHub Actions workflows
│   └── CICD-SETUP.md       # CI/CD setup guide
├── deploy-infra/           # Infrastructure as Code
│   ├── modules/            # Bicep modules
│   ├── main.bicep          # Main orchestration template
│   ├── deploy.ps1          # Deployment script
│   └── README.md           # Infrastructure guide
├── deploy-app/             # Application deployment
│   ├── deploy.ps1          # App deployment script
│   └── README.md           # Deployment guide
├── src/ExpenseManagement/  # Application code
│   ├── Controllers/        # API controllers
│   ├── Models/             # Data models
│   ├── Pages/              # Razor Pages
│   ├── Services/           # Business logic
│   └── wwwroot/            # Static files
├── Database-Schema/        # SQL schema
├── Legacy-Screenshots/     # Original app screenshots
├── ARCHITECTURE.md         # Architecture documentation
├── stored-procedures.sql   # Database stored procedures
└── README.md               # This file
```

## 🔄 CI/CD

The project includes a GitHub Actions workflow for automated deployments.

### Setup CI/CD

1. Follow the instructions in [.github/CICD-SETUP.md](./.github/CICD-SETUP.md)
2. Create Azure Service Principal with OIDC
3. Configure GitHub repository variables
4. Trigger workflow from Actions tab

### Manual Workflow

Go to Actions → Deploy to Azure → Run workflow

## 🧪 Testing

### Validate Bicep Templates

```powershell
az deployment group validate `
  --resource-group "rg-test" `
  --template-file ./deploy-infra/main.bicep `
  --parameters location=uksouth baseName=expensemgmt adminObjectId="guid" adminUsername="user@domain.com"
```

### Build Application

```powershell
cd src/ExpenseManagement
dotnet build
```

### Run Tests (if available)

```powershell
dotnet test
```

## 📊 Monitoring

### Application Insights

View telemetry in Azure Portal:
1. Navigate to Application Insights resource
2. View **Live Metrics** for real-time monitoring
3. Check **Failures** for errors
4. Review **Performance** for slow requests

### Log Analytics

Query logs using KQL:

```kusto
// Recent errors
AppTraces
| where SeverityLevel >= 3
| order by TimeGenerated desc
| take 50
```

## 🤝 Contributing

This is a demonstration project. For production use:

1. Add comprehensive tests
2. Implement proper error handling
3. Add user authentication (Azure AD B2C)
4. Implement file upload for receipts
5. Add email notifications
6. Configure backup and DR

## 📝 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🎓 Learning Resources

- [Azure App Service Documentation](https://learn.microsoft.com/en-us/azure/app-service/)
- [Azure SQL Database Security](https://learn.microsoft.com/en-us/azure/azure-sql/database/security-best-practice)
- [Managed Identities](https://learn.microsoft.com/en-us/azure/active-directory/managed-identities-azure-resources/overview)
- [ASP.NET Core Documentation](https://learn.microsoft.com/en-us/aspnet/core/)

## 🐛 Troubleshooting

### Database Connection Issues

**Symptom**: "Unable to connect to database" error

**Solutions**:
1. Check `AZURE_CLIENT_ID` is set in App Service configuration
2. Verify managed identity has database permissions
3. Ensure connection string includes `User Id={managedIdentityClientId}`

### Application Won't Start

**Solutions**:
1. Check Application Insights for startup errors
2. Verify all App Service settings are configured
3. Check deployment logs in Azure Portal
4. Wait 1-2 minutes for app to fully start

### CI/CD Failures

**Solutions**:
1. Review workflow logs in GitHub Actions
2. Verify OIDC configuration is correct
3. Check Service Principal has required roles
4. See [.github/CICD-SETUP.md](./.github/CICD-SETUP.md) for detailed troubleshooting

## 📞 Support

For issues or questions:
- Review documentation in this repository
- Check Application Insights logs
- Review deployment script output
- Open an issue on GitHub

---

**Built with** ❤️ **using Azure best practices and modern .NET**
