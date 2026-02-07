using ExpenseManagement.Models;

namespace ExpenseManagement.Tests.Helpers;

/// <summary>
/// Test data generators for creating sample test objects
/// </summary>
public static class TestData
{
    /// <summary>
    /// Generate a sample expense with customizable properties
    /// </summary>
    public static Expense GenerateExpense(
        int expenseId = 1,
        string userName = "Test User",
        string categoryName = "Travel",
        string statusName = "Submitted",
        decimal amount = 50.00m,
        string? description = null)
    {
        return new Expense
        {
            ExpenseId = expenseId,
            UserId = 1,
            UserName = userName,
            CategoryId = 1,
            CategoryName = categoryName,
            StatusId = 2,
            StatusName = statusName,
            AmountMinor = (int)(amount * 100),
            Amount = amount,
            Currency = "GBP",
            ExpenseDate = DateTime.Now.AddDays(-5),
            Description = description ?? $"Test expense {expenseId}",
            SubmittedAt = DateTime.Now.AddDays(-5),
            CreatedAt = DateTime.Now.AddDays(-5)
        };
    }

    /// <summary>
    /// Generate a list of sample expenses
    /// </summary>
    public static List<Expense> GenerateExpenses(int count = 5)
    {
        var expenses = new List<Expense>();
        var statuses = new[] { "Draft", "Submitted", "Approved", "Rejected" };
        var categories = new[] { "Travel", "Meals", "Supplies", "Accommodation", "Other" };

        for (int i = 1; i <= count; i++)
        {
            expenses.Add(GenerateExpense(
                expenseId: i,
                userName: $"User {i}",
                categoryName: categories[i % categories.Length],
                statusName: statuses[i % statuses.Length],
                amount: 10.00m * i,
                description: $"Test expense {i} description"
            ));
        }

        return expenses;
    }

    /// <summary>
    /// Generate a sample CreateExpenseRequest
    /// </summary>
    public static CreateExpenseRequest GenerateCreateExpenseRequest(
        string employeeName = "Test Employee",
        string description = "Test expense",
        decimal amount = 50.00m,
        string category = "Travel")
    {
        return new CreateExpenseRequest
        {
            EmployeeName = employeeName,
            Description = description,
            Amount = amount,
            Category = category,
            ExpenseDate = DateTime.Now.AddDays(-1)
        };
    }

    /// <summary>
    /// Generate a sample ApproveExpenseRequest
    /// </summary>
    public static ApproveExpenseRequest GenerateApproveExpenseRequest(
        int expenseId = 1,
        string reviewerName = "Test Reviewer",
        bool approved = true)
    {
        return new ApproveExpenseRequest
        {
            ExpenseId = expenseId,
            ReviewerName = reviewerName,
            Approved = approved
        };
    }

    /// <summary>
    /// Generate a list of expense categories
    /// </summary>
    public static List<ExpenseCategory> GenerateCategories()
    {
        return new List<ExpenseCategory>
        {
            new ExpenseCategory { CategoryId = 1, CategoryName = "Travel", IsActive = true },
            new ExpenseCategory { CategoryId = 2, CategoryName = "Meals", IsActive = true },
            new ExpenseCategory { CategoryId = 3, CategoryName = "Supplies", IsActive = true },
            new ExpenseCategory { CategoryId = 4, CategoryName = "Accommodation", IsActive = true },
            new ExpenseCategory { CategoryId = 5, CategoryName = "Other", IsActive = true }
        };
    }

    /// <summary>
    /// Generate a list of expense summaries
    /// </summary>
    public static List<ExpenseSummary> GenerateSummary()
    {
        return new List<ExpenseSummary>
        {
            new ExpenseSummary { StatusName = "Draft", Count = 3, TotalAmount = 150.00m },
            new ExpenseSummary { StatusName = "Submitted", Count = 5, TotalAmount = 250.00m },
            new ExpenseSummary { StatusName = "Approved", Count = 10, TotalAmount = 500.00m },
            new ExpenseSummary { StatusName = "Rejected", Count = 2, TotalAmount = 50.00m }
        };
    }

    /// <summary>
    /// Generate a test configuration dictionary with all settings
    /// </summary>
    public static Dictionary<string, string> GenerateTestConfiguration(
        string? connectionString = null,
        string? openAIEndpoint = null,
        string? openAIModelName = null,
        string? managedIdentityClientId = null)
    {
        return new Dictionary<string, string>
        {
            ["ConnectionStrings:DefaultConnection"] = connectionString ?? "Server=test;Database=test;",
            ["GenAISettings:OpenAIEndpoint"] = openAIEndpoint ?? "",
            ["GenAISettings:OpenAIModelName"] = openAIModelName ?? "gpt-4o",
            ["ManagedIdentityClientId"] = managedIdentityClientId ?? ""
        };
    }

    /// <summary>
    /// Generate a test configuration with GenAI enabled
    /// </summary>
    public static Dictionary<string, string> GenerateConfigurationWithGenAI()
    {
        return GenerateTestConfiguration(
            openAIEndpoint: "https://test-openai.openai.azure.com",
            openAIModelName: "gpt-4o",
            managedIdentityClientId: "test-client-id"
        );
    }

    /// <summary>
    /// Generate a test configuration with GenAI disabled
    /// </summary>
    public static Dictionary<string, string> GenerateConfigurationWithoutGenAI()
    {
        return GenerateTestConfiguration(
            openAIEndpoint: "",
            openAIModelName: "",
            managedIdentityClientId: ""
        );
    }

    /// <summary>
    /// Generate a sample chat request
    /// </summary>
    public static ChatRequest GenerateChatRequest(
        string message = "Show me all expenses",
        List<ChatMessage>? history = null)
    {
        return new ChatRequest
        {
            Message = message,
            History = history ?? new List<ChatMessage>()
        };
    }
}
