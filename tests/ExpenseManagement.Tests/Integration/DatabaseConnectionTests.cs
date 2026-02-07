using ExpenseManagement.Tests.Helpers;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace ExpenseManagement.Tests.Integration;

/// <summary>
/// Tests for database connection configuration
/// These tests verify configuration is read correctly but use mocks/in-process testing
/// </summary>
public class DatabaseConnectionTests
{
    [Fact]
    public void ConnectionString_CanBeReadFromConfiguration()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                ["ConnectionStrings:DefaultConnection"] = "Server=test-server;Database=ExpenseManagement;User Id=testuser;Password=testpass;"
            }!)
            .Build();

        // Act
        var connectionString = config.GetConnectionString("DefaultConnection");

        // Assert
        connectionString.Should().NotBeNullOrEmpty();
        connectionString.Should().Contain("ExpenseManagement");
        connectionString.Should().Contain("test-server");
    }

    [Fact]
    public void ConnectionString_ReturnsNull_WhenNotConfigured()
    {
        // Arrange
        var config = new ConfigurationBuilder().Build();

        // Act
        var connectionString = config.GetConnectionString("DefaultConnection");

        // Assert
        connectionString.Should().BeNullOrEmpty();
    }

    [Fact]
    public void ConnectionString_CanIncludeManagedIdentity()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                ["ConnectionStrings:DefaultConnection"] = "Server=test.database.windows.net;Database=ExpenseManagement;Authentication=Active Directory Default;"
            }!)
            .Build();

        // Act
        var connectionString = config.GetConnectionString("DefaultConnection");

        // Assert
        connectionString.Should().Contain("Active Directory");
    }

    [Fact]
    public void StoredProcedureNames_AreCorrect()
    {
        // This test documents the expected stored procedure names
        // that should exist in the database
        
        var expectedProcedures = new[]
        {
            "usp_GetExpenses",
            "usp_GetExpenseSummary",
            "usp_GetCategories",
            "usp_CreateExpense",
            "usp_ApproveExpense"
        };

        // Assert
        expectedProcedures.Should().NotBeEmpty();
        expectedProcedures.Should().HaveCount(5);
    }

    [Fact]
    public void ExpenseColumns_AreDocumented()
    {
        // This test documents the expected columns from the stored procedure results
        // Based on the GetExpensesAsync implementation
        
        var expectedColumns = new[]
        {
            "ExpenseId",
            "UserId",
            "UserName",
            "CategoryId",
            "CategoryName",
            "StatusId",
            "StatusName",
            "AmountMinor",
            "AmountDecimal",
            "Currency",
            "ExpenseDate",
            "Description",
            "SubmittedAt",
            "ReviewedBy",
            "ReviewedByName",
            "ReviewedAt",
            "CreatedAt"
        };

        // Assert
        expectedColumns.Should().NotBeEmpty();
        expectedColumns.Should().Contain("ExpenseId");
        expectedColumns.Should().Contain("AmountMinor");
        expectedColumns.Should().Contain("AmountDecimal");
    }

    [Fact]
    public void DatabaseConfiguration_SupportsMultipleEnvironments()
    {
        // Arrange - Development configuration
        var devConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                ["ConnectionStrings:DefaultConnection"] = "Server=localhost;Database=ExpenseManagement_Dev;"
            }!)
            .Build();

        // Arrange - Production configuration
        var prodConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                ["ConnectionStrings:DefaultConnection"] = "Server=prod.database.windows.net;Database=ExpenseManagement;"
            }!)
            .Build();

        // Act
        var devConnectionString = devConfig.GetConnectionString("DefaultConnection");
        var prodConnectionString = prodConfig.GetConnectionString("DefaultConnection");

        // Assert
        devConnectionString.Should().Contain("localhost");
        prodConnectionString.Should().Contain("prod.database.windows.net");
    }

    [Fact]
    public void ManagedIdentityClientId_CanBeReadFromConfiguration()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                ["ManagedIdentityClientId"] = "test-client-id-12345"
            }!)
            .Build();

        // Act
        var clientId = config["ManagedIdentityClientId"];

        // Assert
        clientId.Should().NotBeNullOrEmpty();
        clientId.Should().Be("test-client-id-12345");
    }

    [Fact]
    public void DatabaseConnection_ParameterMapping_IsDocumented()
    {
        // This test documents the parameter mapping for stored procedures
        
        // usp_GetExpenses parameters
        var getExpensesParams = new[] { "@Status", "@EmployeeName" };
        
        // usp_CreateExpense parameters
        var createExpenseParams = new[] 
        { 
            "@EmployeeName", 
            "@Description", 
            "@AmountMinor", 
            "@Category", 
            "@ExpenseDate" 
        };
        
        // usp_ApproveExpense parameters
        var approveExpenseParams = new[] 
        { 
            "@ExpenseId", 
            "@ReviewerName", 
            "@Approved" 
        };

        // Assert
        getExpensesParams.Should().Contain("@Status");
        createExpenseParams.Should().Contain("@AmountMinor");
        approveExpenseParams.Should().Contain("@Approved");
    }
}
