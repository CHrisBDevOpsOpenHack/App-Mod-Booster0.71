## Architecture

```mermaid
flowchart LR
    subgraph Azure
        AppService["Azure App Service (Razor Pages + API)"]
        SQL["Azure SQL Database (Northwind)"]
        MI["User-Assigned Managed Identity"]
        AI["Application Insights"]
        LAW["Log Analytics Workspace"]
        OpenAI["Azure OpenAI (GPT-4o)"]
        Search["Azure AI Search"]
    end

    AppService -->|Managed Identity| SQL
    AppService -->|Telemetry| AI --> LAW
    SQL -->|Diagnostics| LAW
    AppService -->|Managed Identity| OpenAI
    OpenAI --> Search
```

The deployment scripts create the App Service, SQL Database, managed identity, monitoring, and optional GenAI resources. All service-to-service calls use the managed identity—no secrets are stored in code or configuration.
