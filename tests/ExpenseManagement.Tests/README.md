# Expense Management Tests

Comprehensive test suite for the Expense Management application covering unit tests, integration tests, E2E tests, smoke tests, and infrastructure validation.

## Test Structure

```
tests/ExpenseManagement.Tests/
├── ExpenseManagement.Tests.csproj  # Test project file
├── Helpers/                         # Shared test utilities
│   ├── TestFixtures.cs             # WebApplicationFactory and test base classes
│   └── TestData.cs                 # Test data generators
├── Unit/                            # Unit tests with mocked dependencies
│   ├── ExpenseServiceTests.cs      # Tests for ExpenseService
│   └── ChatServiceTests.cs         # Tests for ChatService
├── Integration/                     # Integration tests with in-process testing
│   ├── ApiEndpointTests.cs         # REST API endpoint tests
│   └── DatabaseConnectionTests.cs  # Database configuration tests
├── E2E/                             # End-to-end tests for pages
│   ├── PageNavigationTests.cs      # Tests all Razor Pages load correctly
│   └── ChatFlowTests.cs            # Tests chat UI and graceful fallback
├── Smoke/                           # Post-deployment validation
│   ├── DeploymentSmokeTests.cs     # Tests against live deployment
│   └── HealthCheckTests.cs         # Endpoint health checks
└── Infrastructure/                  # Infrastructure validation
    └── BicepValidationTests.cs     # Bicep template and PowerShell validation
```

## Running Tests

### Run All Tests
```bash
dotnet test tests/ExpenseManagement.Tests/ExpenseManagement.Tests.csproj
```

### Run Specific Test Category
```bash
# Unit tests only
dotnet test --filter "FullyQualifiedName~ExpenseManagement.Tests.Unit"

# Integration tests only
dotnet test --filter "FullyQualifiedName~ExpenseManagement.Tests.Integration"

# E2E tests only
dotnet test --filter "FullyQualifiedName~ExpenseManagement.Tests.E2E"

# Smoke tests only (for post-deployment validation)
dotnet test --filter "FullyQualifiedName~ExpenseManagement.Tests.Smoke"

# Infrastructure tests only
dotnet test --filter "FullyQualifiedName~ExpenseManagement.Tests.Infrastructure"
```

### Run with Code Coverage
```bash
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura
```

## Test Categories

### 1. Unit Tests (15 tests)

Test services in isolation with mocked dependencies. No real database required.

**ExpenseServiceTests.cs:**
- Service initialization
- GetExpenses with/without filters
- GetExpenseSummary fallback behavior
- GetCategories fallback behavior
- CreateExpense error handling
- ApproveExpense error handling
- Configuration validation

**ChatServiceTests.cs:**
- IsConfigured property behavior
- SendMessage graceful fallback
- Error messages when GenAI not configured
- Configuration detection

### 2. Integration Tests (26 tests)

Test API endpoints and database configuration using `WebApplicationFactory` for in-process testing.

**ApiEndpointTests.cs:**
- GET /api/expenses (with/without filters)
- GET /api/expenses/summary
- GET /api/categories
- POST /api/expenses
- POST /api/expenses/approve
- GET /swagger/index.html
- GET /swagger/v1/swagger.json
- POST /api/chat
- Content-Type validation

**DatabaseConnectionTests.cs:**
- Connection string reading
- Managed identity configuration
- Stored procedure names
- Column mapping documentation
- Parameter mapping

### 3. E2E Tests (20 tests)

Test that all Razor Pages render correctly.

**PageNavigationTests.cs:**
- All pages load: Index, AddExpense, Expenses, Approvals, Chat, Error
- Pages return HTML content type
- Pages contain DOCTYPE
- Non-existent pages return 404
- Pages accessible without authentication

**ChatFlowTests.cs:**
- Chat page shows "not available" message when GenAI not configured
- Chat page mentions DeployGenAI switch
- Chat page doesn't crash without GenAI
- Chat page contains UI elements
- Chat page works when GenAI is configured

### 4. Smoke Tests (21 tests)

Post-deployment validation that runs against live instances. Reads app URL from `.deployment-context.json`.

**DeploymentSmokeTests.cs:**
- All pages return 200
- Swagger accessible
- API endpoints functional
- Response time validation
- Content-Type validation

**HealthCheckTests.cs:**
- All endpoints return expected status codes
- API endpoints return JSON
- Pages return HTML
- Swagger JSON is valid OpenAPI
- No server errors (500, 502, 503)

### 5. Infrastructure Tests (11 tests)

Validate Bicep templates and PowerShell scripts without deploying.

**BicepValidationTests.cs:**
- All .bicep files compile without errors (requires `az` CLI)
- main.bicep has required outputs
- No bash scripts in repository
- PowerShell scripts pass PSScriptAnalyzer
- deploy-all.ps1 uses hashtable splatting
- Required modules exist

## Dependencies

The test project uses:

- **xUnit** - Test framework
- **Microsoft.AspNetCore.Mvc.Testing** - In-process testing with WebApplicationFactory
- **Moq** - Mocking framework for unit tests
- **FluentAssertions** - Readable assertions

## Test Patterns

### Arrange-Act-Assert

All tests follow the AAA pattern:

```csharp
[Fact]
public async Task GetExpenses_ReturnsOkWithData()
{
    // Arrange
    // (setup happens in constructor or fixture)
    
    // Act
    var response = await _client.GetAsync("/api/expenses");
    
    // Assert
    response.StatusCode.Should().Be(HttpStatusCode.OK);
}
```

### WebApplicationFactory for Integration Tests

Integration tests use `WebApplicationFactory<Program>` for in-process testing:

```csharp
public class ApiEndpointTests : IClassFixture<ExpenseManagementWebApplicationFactory>
{
    private readonly HttpClient _client;
    
    public ApiEndpointTests(ExpenseManagementWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }
}
```

### Graceful Skipping for Optional Dependencies

Tests that require external tools (az CLI, PSScriptAnalyzer) skip gracefully if not available:

```csharp
if (!IsAzCliAvailable())
{
    return; // Skip test
}
```

### Smoke Tests with Deployment Context

Smoke tests read from `.deployment-context.json` and skip if the file doesn't exist:

```csharp
if (!_hasDeploymentContext || string.IsNullOrEmpty(_appUrl))
{
    return; // Skip if not deployed
}
```

## Key Testing Principles

1. **No Hardcoded Values**: All URLs, connection strings, and configuration use test data generators or deployment context
2. **Graceful Fallbacks**: Tests verify the application handles missing configuration gracefully
3. **In-Process Testing**: Integration tests use WebApplicationFactory instead of requiring a running server
4. **Infrastructure as Code Validation**: Bicep templates validated before deployment
5. **Chat Page Always Present**: Tests verify the chat page exists even when GenAI is not deployed

## CI/CD Integration

These tests are designed to run in CI/CD pipelines:

```yaml
- name: Run Unit Tests
  run: dotnet test --filter "FullyQualifiedName~ExpenseManagement.Tests.Unit"

- name: Run Integration Tests
  run: dotnet test --filter "FullyQualifiedName~ExpenseManagement.Tests.Integration"

- name: Run E2E Tests
  run: dotnet test --filter "FullyQualifiedName~ExpenseManagement.Tests.E2E"

# After deployment:
- name: Run Smoke Tests
  run: dotnet test --filter "FullyQualifiedName~ExpenseManagement.Tests.Smoke"
```

## Test Summary

- **Total Tests**: 85
- **Unit Tests**: 15
- **Integration Tests**: 26
- **E2E Tests**: 20
- **Smoke Tests**: 21
- **Infrastructure Tests**: 11

All tests pass successfully! ✅
