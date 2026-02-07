using FluentAssertions;
using System.Net;
using System.Text.Json;
using Xunit;

namespace ExpenseManagement.Tests.Smoke;

/// <summary>
/// Post-deployment smoke tests that run against a live deployed instance
/// Reads app URL from .deployment-context.json
/// </summary>
public class DeploymentSmokeTests
{
    private readonly HttpClient _httpClient;
    private readonly string? _appUrl;
    private readonly bool _hasDeploymentContext;

    public DeploymentSmokeTests()
    {
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        // Try to read deployment context from current directory or parent
        _appUrl = TryGetAppUrl(out _hasDeploymentContext);
    }

    private string? TryGetAppUrl(out bool hasContext)
    {
        var contextPaths = new[]
        {
            ".deployment-context.json",
            "../.deployment-context.json",
            "../../.deployment-context.json"
        };

        foreach (var path in contextPaths)
        {
            if (File.Exists(path))
            {
                try
                {
                    var json = File.ReadAllText(path);
                    var doc = JsonDocument.Parse(json);
                    
                    if (doc.RootElement.TryGetProperty("appServiceUrl", out var urlElement))
                    {
                        var url = urlElement.GetString();
                        if (!string.IsNullOrEmpty(url))
                        {
                            hasContext = true;
                            return url.TrimEnd('/');
                        }
                    }
                }
                catch
                {
                    // Ignore parse errors
                }
            }
        }

        hasContext = false;
        return null;
    }

    [Fact]
    public void DeploymentContext_ShouldExistForSmokeTests()
    {
        // This test documents that smoke tests require deployment context
        // It's expected to be skipped if not deployed
        
        if (!_hasDeploymentContext)
        {
            // Skip this test if no deployment context exists
            return;
        }

        _appUrl.Should().NotBeNullOrEmpty("deployment context should provide app URL");
        _appUrl.Should().StartWith("http", "app URL should be a valid HTTP(S) URL");
    }

    [Fact]
    public async Task LiveApp_IndexPage_Returns200()
    {
        // Skip if no deployment context
        if (!_hasDeploymentContext || string.IsNullOrEmpty(_appUrl))
        {
            return;
        }

        // Act
        var response = await _httpClient.GetAsync($"{_appUrl}/Index");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK, 
            "deployed Index page should return 200");
    }

    [Fact]
    public async Task LiveApp_RootPath_Returns200()
    {
        // Skip if no deployment context
        if (!_hasDeploymentContext || string.IsNullOrEmpty(_appUrl))
        {
            return;
        }

        // Act
        var response = await _httpClient.GetAsync(_appUrl);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK, 
            "deployed root should return 200");
    }

    [Fact]
    public async Task LiveApp_SwaggerEndpoint_Returns200()
    {
        // Skip if no deployment context
        if (!_hasDeploymentContext || string.IsNullOrEmpty(_appUrl))
        {
            return;
        }

        // Act
        var response = await _httpClient.GetAsync($"{_appUrl}/swagger/index.html");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK, 
            "Swagger documentation should be accessible");
        
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("swagger", "Swagger UI should be rendered");
    }

    [Fact]
    public async Task LiveApp_ChatPage_Returns200()
    {
        // Skip if no deployment context
        if (!_hasDeploymentContext || string.IsNullOrEmpty(_appUrl))
        {
            return;
        }

        // Act
        var response = await _httpClient.GetAsync($"{_appUrl}/Chat");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK, 
            "Chat page should be accessible even if GenAI is not deployed");
    }

    [Fact]
    public async Task LiveApp_ExpensesApiEndpoint_Returns200()
    {
        // Skip if no deployment context
        if (!_hasDeploymentContext || string.IsNullOrEmpty(_appUrl))
        {
            return;
        }

        // Act
        var response = await _httpClient.GetAsync($"{_appUrl}/api/expenses");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK, 
            "API endpoint should return 200");
        
        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task LiveApp_AddExpensePage_Returns200()
    {
        // Skip if no deployment context
        if (!_hasDeploymentContext || string.IsNullOrEmpty(_appUrl))
        {
            return;
        }

        // Act
        var response = await _httpClient.GetAsync($"{_appUrl}/AddExpense");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task LiveApp_ExpensesPage_Returns200()
    {
        // Skip if no deployment context
        if (!_hasDeploymentContext || string.IsNullOrEmpty(_appUrl))
        {
            return;
        }

        // Act
        var response = await _httpClient.GetAsync($"{_appUrl}/Expenses");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task LiveApp_ApprovalsPage_Returns200()
    {
        // Skip if no deployment context
        if (!_hasDeploymentContext || string.IsNullOrEmpty(_appUrl))
        {
            return;
        }

        // Act
        var response = await _httpClient.GetAsync($"{_appUrl}/Approvals");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task LiveApp_RespondsWithinTimeout()
    {
        // Skip if no deployment context
        if (!_hasDeploymentContext || string.IsNullOrEmpty(_appUrl))
        {
            return;
        }

        // Act
        var startTime = DateTime.UtcNow;
        var response = await _httpClient.GetAsync($"{_appUrl}/Index");
        var elapsed = DateTime.UtcNow - startTime;

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        elapsed.TotalSeconds.Should().BeLessThan(30, 
            "application should respond within timeout");
    }

    [Fact]
    public async Task LiveApp_ReturnsHtmlForPages()
    {
        // Skip if no deployment context
        if (!_hasDeploymentContext || string.IsNullOrEmpty(_appUrl))
        {
            return;
        }

        // Act
        var response = await _httpClient.GetAsync($"{_appUrl}/Index");

        // Assert
        response.Content.Headers.ContentType?.MediaType.Should().Be("text/html");
    }
}
