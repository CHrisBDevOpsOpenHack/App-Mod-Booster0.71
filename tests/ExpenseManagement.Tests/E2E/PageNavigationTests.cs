using ExpenseManagement.Tests.Helpers;
using FluentAssertions;
using System.Net;
using Xunit;

namespace ExpenseManagement.Tests.E2E;

/// <summary>
/// End-to-end tests for Razor Pages
/// Tests that pages load and render correctly
/// </summary>
public class PageNavigationTests : IClassFixture<ExpenseManagementWebApplicationFactory>
{
    private readonly HttpClient _client;

    public PageNavigationTests(ExpenseManagementWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task IndexPage_Loads()
    {
        // Act
        var response = await _client.GetAsync("/Index");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrEmpty();
        content.Should().Contain("Expense", "Index page should contain expense-related content");
    }

    [Fact]
    public async Task IndexPage_AsRoot_Loads()
    {
        // Act
        var response = await _client.GetAsync("/");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task AddExpensePage_Loads()
    {
        // Act
        var response = await _client.GetAsync("/AddExpense");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrEmpty();
        content.Should().Contain("Add", "AddExpense page should contain add-related content");
    }

    [Fact]
    public async Task ExpensesPage_Loads()
    {
        // Act
        var response = await _client.GetAsync("/Expenses");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrEmpty();
        content.Should().Contain("Expense", "Expenses page should list expenses");
    }

    [Fact]
    public async Task ApprovalsPage_Loads()
    {
        // Act
        var response = await _client.GetAsync("/Approvals");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrEmpty();
        content.Should().Contain("Approval", "Approvals page should contain approval-related content");
    }

    [Fact]
    public async Task ChatPage_Loads()
    {
        // Act
        var response = await _client.GetAsync("/Chat");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK, 
            "Chat page must always exist even when GenAI is not deployed");
        
        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrEmpty();
        content.Should().Contain("Chat", "Chat page should have chat-related content");
    }

    [Fact]
    public async Task ErrorPage_Loads()
    {
        // Act
        var response = await _client.GetAsync("/Error");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task AllPages_ReturnHtmlContentType()
    {
        // Arrange
        var pages = new[] { "/Index", "/AddExpense", "/Expenses", "/Approvals", "/Chat", "/Error" };

        foreach (var page in pages)
        {
            // Act
            var response = await _client.GetAsync(page);

            // Assert
            response.Content.Headers.ContentType?.MediaType.Should().Be("text/html",
                $"page {page} should return HTML");
        }
    }

    [Fact]
    public async Task AllPages_ContainDoctype()
    {
        // Arrange
        var pages = new[] { "/Index", "/AddExpense", "/Expenses", "/Approvals", "/Chat" };

        foreach (var page in pages)
        {
            // Act
            var response = await _client.GetAsync(page);
            var content = await response.Content.ReadAsStringAsync();

            // Assert
            content.Should().Contain("<!DOCTYPE", 
                $"page {page} should contain DOCTYPE declaration");
        }
    }

    [Fact]
    public async Task NonExistentPage_Returns404()
    {
        // Act
        var response = await _client.GetAsync("/NonExistentPage");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Pages_AreAccessibleWithoutAuthentication()
    {
        // This test verifies that pages don't require authentication
        // (as the current implementation doesn't have auth)
        
        // Arrange
        var pages = new[] { "/Index", "/AddExpense", "/Expenses", "/Approvals", "/Chat" };

        foreach (var page in pages)
        {
            // Act
            var response = await _client.GetAsync(page);

            // Assert
            response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized,
                $"page {page} should be accessible without authentication");
            response.StatusCode.Should().NotBe(HttpStatusCode.Forbidden,
                $"page {page} should not be forbidden");
        }
    }
}
