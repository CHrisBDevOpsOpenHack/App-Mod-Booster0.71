using ExpenseManagement.Tests.Helpers;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using System.Net;
using Xunit;

namespace ExpenseManagement.Tests.E2E;

/// <summary>
/// End-to-end tests for Chat UI functionality
/// Tests graceful fallback behavior when GenAI is not configured
/// </summary>
public class ChatFlowTests : IClassFixture<ExpenseManagementWebApplicationFactory>
{
    private readonly HttpClient _client;

    public ChatFlowTests(ExpenseManagementWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task ChatPage_ShowsNotAvailableMessage_WhenGenAINotConfigured()
    {
        // Arrange - Factory uses default configuration without GenAI

        // Act
        var response = await _client.GetAsync("/Chat");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("not available", "Chat page should show 'not available' message when GenAI is not configured");
    }

    [Fact]
    public async Task ChatPage_MentionsDeployGenAI_WhenNotConfigured()
    {
        // Act
        var response = await _client.GetAsync("/Chat");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        content.Should().MatchRegex("(?i)(deploygenai|deploy.*genai|genai.*deploy)", 
            "Chat page should mention DeployGenAI to help users enable the feature");
    }

    [Fact]
    public async Task ChatPage_DoesNotCrash_WhenGenAINotConfigured()
    {
        // Act
        var response = await _client.GetAsync("/Chat");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK, 
            "Chat page should load successfully even when GenAI is not configured");
        
        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotMatchRegex("(?i)<h\\d[^>]*>.*error.*</h\\d>", 
            "Chat page should not show error headings, only graceful fallback");
    }

    [Fact]
    public async Task ChatPage_ContainsChatUIElements()
    {
        // Act
        var response = await _client.GetAsync("/Chat");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        // Even when not configured, the page should have chat UI structure
        content.Should().Contain("Chat", "Page should have Chat in title or heading");
    }

    [Fact]
    public async Task ChatPage_ReturnsHtml()
    {
        // Act
        var response = await _client.GetAsync("/Chat");

        // Assert
        response.Content.Headers.ContentType?.MediaType.Should().Be("text/html");
        
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("<!DOCTYPE", "Should be valid HTML");
        content.Should().Contain("<html", "Should have html tag");
    }

    [Fact]
    public async Task ChatPage_HasConsistentStyling()
    {
        // Act
        var response = await _client.GetAsync("/Chat");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        // Should include either inline styles or link to stylesheet
        content.Should().Match(c => c.Contains("<style") || c.Contains("<link"),
            "Page should have some styling");
    }

    [Fact]
    public async Task ChatPage_WithGenAIConfigured_LoadsSuccessfully()
    {
        // Arrange
        var factory = new ExpenseManagementWebApplicationFactory
        {
            TestConfiguration = new ConfigurationBuilder()
                .AddInMemoryCollection(TestData.GenerateConfigurationWithGenAI()!)
                .Build()
        };
        
        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/Chat");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ChatPage_IsAccessibleViaDirectNavigation()
    {
        // Act
        var response = await _client.GetAsync("/Chat");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        // Should not redirect or return 404
        response.RequestMessage?.RequestUri?.AbsolutePath.Should().EndWith("/Chat");
    }

    [Fact]
    public async Task ChatPage_DoesNotRequireAuthentication()
    {
        // Act
        var response = await _client.GetAsync("/Chat");

        // Assert
        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
        response.StatusCode.Should().NotBe(HttpStatusCode.Forbidden);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
