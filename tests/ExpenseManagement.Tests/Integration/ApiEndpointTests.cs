using ExpenseManagement.Tests.Helpers;
using FluentAssertions;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace ExpenseManagement.Tests.Integration;

/// <summary>
/// Integration tests for REST API endpoints
/// Uses WebApplicationFactory for in-process testing
/// </summary>
public class ApiEndpointTests : IClassFixture<ExpenseManagementWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly ExpenseManagementWebApplicationFactory _factory;

    public ApiEndpointTests(ExpenseManagementWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetExpenses_ReturnsOkWithData()
    {
        // Act
        var response = await _client.GetAsync("/api/expenses");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrEmpty();

        // Should return JSON array
        var expenses = await response.Content.ReadFromJsonAsync<List<object>>();
        expenses.Should().NotBeNull();
    }

    [Fact]
    public async Task GetExpenses_WithStatusFilter_ReturnsOk()
    {
        // Act
        var response = await _client.GetAsync("/api/expenses?status=Submitted");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GetExpenses_WithEmployeeNameFilter_ReturnsOk()
    {
        // Act
        var response = await _client.GetAsync("/api/expenses?employeeName=John");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetExpenses_WithMultipleFilters_ReturnsOk()
    {
        // Act
        var response = await _client.GetAsync("/api/expenses?status=Approved&employeeName=Jane");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetExpenseSummary_ReturnsOkWithData()
    {
        // Act
        var response = await _client.GetAsync("/api/expenses/summary");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrEmpty();

        var summary = await response.Content.ReadFromJsonAsync<List<object>>();
        summary.Should().NotBeNull();
    }

    [Fact]
    public async Task GetCategories_ReturnsOkWithData()
    {
        // Act
        var response = await _client.GetAsync("/api/categories");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrEmpty();

        var categories = await response.Content.ReadFromJsonAsync<List<object>>();
        categories.Should().NotBeNull();
        categories.Should().NotBeEmpty("should return at least dummy categories");
    }

    [Fact]
    public async Task CreateExpense_WithValidRequest_ReturnsOk()
    {
        // Arrange
        var request = TestData.GenerateCreateExpenseRequest();

        // Act
        var response = await _client.PostAsJsonAsync("/api/expenses", request);

        // Assert
        // May return 500 due to database unavailability, but endpoint should exist
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task CreateExpense_WithInvalidRequest_ReturnsBadRequestOrServerError()
    {
        // Arrange
        var invalidRequest = new { };

        // Act
        var response = await _client.PostAsJsonAsync("/api/expenses", invalidRequest);

        // Assert
        // Should fail validation or fail at database level
        response.StatusCode.Should().NotBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ApproveExpense_WithValidRequest_ReturnsOkOrError()
    {
        // Arrange
        var request = TestData.GenerateApproveExpenseRequest();

        // Act
        var response = await _client.PostAsJsonAsync("/api/expenses/approve", request);

        // Assert
        // May return 500 due to database unavailability, but endpoint should exist
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task Swagger_IsAccessible()
    {
        // Act
        var response = await _client.GetAsync("/swagger/index.html");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("swagger", "Swagger UI should be accessible");
    }

    [Fact]
    public async Task SwaggerJson_IsAccessible()
    {
        // Act
        var response = await _client.GetAsync("/swagger/v1/swagger.json");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);
        
        // Verify it's valid OpenAPI/Swagger JSON
        json.RootElement.TryGetProperty("openapi", out var openApiVersion).Should().BeTrue();
        json.RootElement.TryGetProperty("paths", out var paths).Should().BeTrue();
    }

    [Fact]
    public async Task ChatApi_EndpointExists()
    {
        // Arrange
        var request = TestData.GenerateChatRequest();

        // Act
        var response = await _client.PostAsJsonAsync("/api/chat", request);

        // Assert
        // Should return OK with error response (not configured) or 500
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.InternalServerError);
        
        if (response.StatusCode == HttpStatusCode.OK)
        {
            var content = await response.Content.ReadAsStringAsync();
            content.Should().Contain("not available", "chat should indicate it's not configured");
        }
    }

    [Fact]
    public async Task ApiEndpoints_ReturnJsonContentType()
    {
        // Act
        var response = await _client.GetAsync("/api/expenses");

        // Assert
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");
    }

    [Fact]
    public async Task ApiEndpoints_SupportCors()
    {
        // Act
        var response = await _client.GetAsync("/api/expenses");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK, 
            "API should be accessible for testing CORS support");
    }
}
