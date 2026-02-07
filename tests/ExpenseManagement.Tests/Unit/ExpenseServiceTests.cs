using ExpenseManagement.Models;
using ExpenseManagement.Services;
using ExpenseManagement.Tests.Helpers;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ExpenseManagement.Tests.Unit;

/// <summary>
/// Unit tests for ExpenseService
/// These tests use mocked dependencies and don't require a real database
/// </summary>
public class ExpenseServiceTests
{
    private readonly Mock<ILogger<ExpenseService>> _mockLogger;
    private readonly IConfiguration _testConfiguration;

    public ExpenseServiceTests()
    {
        _mockLogger = new Mock<ILogger<ExpenseService>>();
        
        // Create test configuration with a mock connection string
        var configValues = TestData.GenerateTestConfiguration(
            connectionString: "Server=test-server;Database=test-db;User Id=test;Password=test123;"
        );
        
        _testConfiguration = new ConfigurationBuilder()
            .AddInMemoryCollection(configValues!)
            .Build();
    }

    [Fact]
    public void ExpenseService_Constructor_ShouldInitialize()
    {
        // Arrange & Act
        var service = new ExpenseService(_testConfiguration, _mockLogger.Object);

        // Assert
        service.Should().NotBeNull();
    }

    [Fact]
    public async Task GetExpensesAsync_WhenDatabaseUnavailable_ReturnsDummyData()
    {
        // Arrange
        // Use invalid connection string to simulate database unavailability
        var invalidConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                ["ConnectionStrings:DefaultConnection"] = "Server=invalid;Database=invalid;"
            }!)
            .Build();

        var service = new ExpenseService(invalidConfig, _mockLogger.Object);

        // Act
        var result = await service.GetExpensesAsync();

        // Assert
        result.Should().NotBeNull();
        result.Should().NotBeEmpty("service should return dummy data when database is unavailable");
        result.Should().HaveCountGreaterThan(0);
        result.First().UserName.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GetExpensesAsync_WithStatusFilter_ShouldAcceptParameter()
    {
        // Arrange
        var invalidConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                ["ConnectionStrings:DefaultConnection"] = "Server=invalid;Database=invalid;"
            }!)
            .Build();

        var service = new ExpenseService(invalidConfig, _mockLogger.Object);

        // Act
        var result = await service.GetExpensesAsync(status: "Submitted");

        // Assert - Falls back to dummy data but doesn't throw
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task GetExpensesAsync_WithEmployeeNameFilter_ShouldAcceptParameter()
    {
        // Arrange
        var invalidConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                ["ConnectionStrings:DefaultConnection"] = "Server=invalid;Database=invalid;"
            }!)
            .Build();

        var service = new ExpenseService(invalidConfig, _mockLogger.Object);

        // Act
        var result = await service.GetExpensesAsync(employeeName: "John Doe");

        // Assert - Falls back to dummy data but doesn't throw
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task GetExpenseSummaryAsync_WhenDatabaseUnavailable_ReturnsDummyData()
    {
        // Arrange
        var invalidConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                ["ConnectionStrings:DefaultConnection"] = "Server=invalid;Database=invalid;"
            }!)
            .Build();

        var service = new ExpenseService(invalidConfig, _mockLogger.Object);

        // Act
        var result = await service.GetExpenseSummaryAsync();

        // Assert
        result.Should().NotBeNull();
        result.Should().NotBeEmpty("service should return dummy summary when database is unavailable");
        result.Should().HaveCountGreaterThan(0);
        result.First().StatusName.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GetCategoriesAsync_WhenDatabaseUnavailable_ReturnsDummyData()
    {
        // Arrange
        var invalidConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                ["ConnectionStrings:DefaultConnection"] = "Server=invalid;Database=invalid;"
            }!)
            .Build();

        var service = new ExpenseService(invalidConfig, _mockLogger.Object);

        // Act
        var result = await service.GetCategoriesAsync();

        // Assert
        result.Should().NotBeNull();
        result.Should().NotBeEmpty("service should return dummy categories when database is unavailable");
        result.Should().HaveCountGreaterThan(0);
        result.First().CategoryName.Should().NotBeNullOrEmpty();
        result.Should().Contain(c => c.CategoryName == "Travel");
        result.Should().Contain(c => c.CategoryName == "Meals");
    }

    [Fact]
    public async Task CreateExpenseAsync_WithInvalidConnection_ThrowsException()
    {
        // Arrange
        var invalidConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                ["ConnectionStrings:DefaultConnection"] = "Server=invalid;Database=invalid;"
            }!)
            .Build();

        var service = new ExpenseService(invalidConfig, _mockLogger.Object);
        var request = TestData.GenerateCreateExpenseRequest();

        // Act
        Func<Task> act = async () => await service.CreateExpenseAsync(request);

        // Assert
        await act.Should().ThrowAsync<Exception>("creating expense should fail when database is unavailable");
    }

    [Fact]
    public async Task ApproveExpenseAsync_WithInvalidConnection_ThrowsException()
    {
        // Arrange
        var invalidConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                ["ConnectionStrings:DefaultConnection"] = "Server=invalid;Database=invalid;"
            }!)
            .Build();

        var service = new ExpenseService(invalidConfig, _mockLogger.Object);
        var request = TestData.GenerateApproveExpenseRequest();

        // Act
        Func<Task> act = async () => await service.ApproveExpenseAsync(request);

        // Assert
        await act.Should().ThrowAsync<Exception>("approving expense should fail when database is unavailable");
    }

    [Fact]
    public async Task ExpenseService_WithMissingConnectionString_FallsBackToDummyData()
    {
        // Arrange
        var emptyConfig = new ConfigurationBuilder().Build();
        var service = new ExpenseService(emptyConfig, _mockLogger.Object);

        // Act
        var result = await service.GetExpensesAsync();

        // Assert
        result.Should().NotBeNull("service should fall back to dummy data when connection string is missing");
        result.Should().NotBeEmpty("service should return dummy expenses");
    }
}
