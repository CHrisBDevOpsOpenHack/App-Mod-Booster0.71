using ExpenseManagement.Services;
using ExpenseManagement.Tests.Helpers;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ExpenseManagement.Tests.Unit;

/// <summary>
/// Unit tests for ChatService
/// These tests verify configuration detection and graceful fallback behavior
/// </summary>
public class ChatServiceTests
{
    private readonly Mock<ILogger<ChatService>> _mockLogger;
    private readonly Mock<ILogger<ExpenseService>> _mockExpenseLogger;

    public ChatServiceTests()
    {
        _mockLogger = new Mock<ILogger<ChatService>>();
        _mockExpenseLogger = new Mock<ILogger<ExpenseService>>();
    }

    [Fact]
    public void ChatService_IsConfigured_ReturnsFalse_WhenEndpointMissing()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(TestData.GenerateConfigurationWithoutGenAI()!)
            .Build();

        var expenseService = new ExpenseService(config, _mockExpenseLogger.Object);
        var chatService = new ChatService(config, _mockLogger.Object, expenseService);

        // Act
        var isConfigured = chatService.IsConfigured;

        // Assert
        isConfigured.Should().BeFalse("GenAI endpoint is not configured");
    }

    [Fact]
    public void ChatService_IsConfigured_ReturnsTrue_WhenEndpointPresent()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(TestData.GenerateConfigurationWithGenAI()!)
            .Build();

        var expenseService = new ExpenseService(config, _mockExpenseLogger.Object);
        var chatService = new ChatService(config, _mockLogger.Object, expenseService);

        // Act
        var isConfigured = chatService.IsConfigured;

        // Assert
        isConfigured.Should().BeTrue("GenAI endpoint is configured");
    }

    [Fact]
    public void ChatService_IsConfigured_ReturnsFalse_WhenEndpointEmpty()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                ["GenAISettings:OpenAIEndpoint"] = "",
                ["GenAISettings:OpenAIModelName"] = "gpt-4o"
            }!)
            .Build();

        var expenseService = new ExpenseService(
            new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    ["ConnectionStrings:DefaultConnection"] = "Server=test;Database=test;"
                }!)
                .Build(),
            _mockExpenseLogger.Object
        );

        var chatService = new ChatService(config, _mockLogger.Object, expenseService);

        // Act
        var isConfigured = chatService.IsConfigured;

        // Assert
        isConfigured.Should().BeFalse("GenAI endpoint is empty");
    }

    [Fact]
    public async Task SendMessageAsync_WhenNotConfigured_ReturnsErrorResponse()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(TestData.GenerateConfigurationWithoutGenAI()!)
            .Build();

        var expenseService = new ExpenseService(config, _mockExpenseLogger.Object);
        var chatService = new ChatService(config, _mockLogger.Object, expenseService);

        // Act
        var response = await chatService.SendMessageAsync("Hello", new List<ExpenseManagement.Models.ChatMessage>());

        // Assert
        response.Should().NotBeNull();
        response.Success.Should().BeFalse("chat is not configured");
        response.Error.Should().NotBeNullOrEmpty();
        response.Error.Should().Contain("not available");
        response.Error.Should().Contain("DeployGenAI", "error message should mention how to enable GenAI");
    }

    [Fact]
    public async Task SendMessageAsync_WhenNotConfigured_SuggestsDeploymentSwitch()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(TestData.GenerateConfigurationWithoutGenAI()!)
            .Build();

        var expenseService = new ExpenseService(config, _mockExpenseLogger.Object);
        var chatService = new ChatService(config, _mockLogger.Object, expenseService);

        // Act
        var response = await chatService.SendMessageAsync("Test message", new List<ExpenseManagement.Models.ChatMessage>());

        // Assert
        response.Error.Should().Contain("-DeployGenAI", 
            "error message should mention the deployment switch to help users enable GenAI");
    }

    [Fact]
    public void ChatService_Constructor_ShouldInitialize()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(TestData.GenerateConfigurationWithoutGenAI()!)
            .Build();

        var expenseService = new ExpenseService(config, _mockExpenseLogger.Object);

        // Act
        var chatService = new ChatService(config, _mockLogger.Object, expenseService);

        // Assert
        chatService.Should().NotBeNull();
    }

    [Fact]
    public async Task SendMessageAsync_WithEmptyMessage_WhenNotConfigured_ReturnsError()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(TestData.GenerateConfigurationWithoutGenAI()!)
            .Build();

        var expenseService = new ExpenseService(config, _mockExpenseLogger.Object);
        var chatService = new ChatService(config, _mockLogger.Object, expenseService);

        // Act
        var response = await chatService.SendMessageAsync("", new List<ExpenseManagement.Models.ChatMessage>());

        // Assert
        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task SendMessageAsync_WithNullHistory_WhenNotConfigured_ReturnsError()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(TestData.GenerateConfigurationWithoutGenAI()!)
            .Build();

        var expenseService = new ExpenseService(config, _mockExpenseLogger.Object);
        var chatService = new ChatService(config, _mockLogger.Object, expenseService);

        // Act
        var response = await chatService.SendMessageAsync("Test", new List<ExpenseManagement.Models.ChatMessage>());

        // Assert
        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNullOrEmpty();
    }
}
