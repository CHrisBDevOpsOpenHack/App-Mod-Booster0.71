using ExpenseManagement.Models;
using Microsoft.Data.SqlClient;

namespace ExpenseManagement.Services;

public interface IExpenseService
{
    Task<List<Expense>> GetAllExpensesAsync();
    Task<Expense?> GetExpenseByIdAsync(int expenseId);
    Task<List<Expense>> GetExpensesByStatusAsync(string statusName);
    Task<List<Expense>> GetExpensesByUserAsync(int userId);
    Task<List<ExpenseSummary>> GetExpenseSummaryAsync();
    Task<int> CreateExpenseAsync(ExpenseCreateRequest request);
    Task<bool> SubmitExpenseAsync(int expenseId);
    Task<bool> ApproveExpenseAsync(int expenseId, int reviewedBy);
    Task<bool> RejectExpenseAsync(int expenseId, int reviewedBy);
    Task<bool> DeleteExpenseAsync(int expenseId);
    Task<List<Category>> GetCategoriesAsync();
    Task<List<User>> GetUsersAsync();
    Task<List<Status>> GetStatusesAsync();
    ErrorInfo? LastError { get; }
    bool IsConnected { get; }
}

public class ExpenseService : IExpenseService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<ExpenseService> _logger;
    private readonly string _connectionString;
    
    public ErrorInfo? LastError { get; private set; }
    public bool IsConnected { get; private set; } = true;

    public ExpenseService(IConfiguration configuration, ILogger<ExpenseService> logger)
    {
        _configuration = configuration;
        _logger = logger;
        _connectionString = _configuration.GetConnectionString("DefaultConnection") ?? "";
        
        if (string.IsNullOrEmpty(_connectionString))
        {
            IsConnected = false;
            LastError = new ErrorInfo
            {
                Message = "Database connection string is not configured.",
                Guidance = "Set the ConnectionStrings__DefaultConnection environment variable or configure it in appsettings.json."
            };
        }
    }

    private SqlConnection CreateConnection()
    {
        return new SqlConnection(_connectionString);
    }

    private void HandleError(Exception ex, string operation, [System.Runtime.CompilerServices.CallerFilePath] string? file = null, [System.Runtime.CompilerServices.CallerLineNumber] int lineNumber = 0)
    {
        IsConnected = false;
        var message = ex.Message;
        string? guidance = null;

        if (message.Contains("Unable to load the proper Managed Identity") || message.Contains("AZURE_CLIENT_ID"))
        {
            guidance = "The AZURE_CLIENT_ID environment variable is not set. Configure it in the App Service settings.";
        }
        else if (message.Contains("Login failed for user"))
        {
            guidance = "The managed identity database user may not have been created or lacks proper permissions.";
        }
        else if (message.Contains("connection string"))
        {
            guidance = "Check the ConnectionStrings__DefaultConnection app setting is configured correctly.";
        }

        LastError = new ErrorInfo
        {
            Message = $"{operation}: {message}",
            File = file != null ? Path.GetFileName(file) : null,
            LineNumber = lineNumber,
            Guidance = guidance
        };

        _logger.LogError(ex, "Error during {Operation}", operation);
    }

    public async Task<List<Expense>> GetAllExpensesAsync()
    {
        var expenses = new List<Expense>();
        
        try
        {
            using var connection = CreateConnection();
            await connection.OpenAsync();
            IsConnected = true;
            LastError = null;

            using var command = new SqlCommand("dbo.GetAllExpenses", connection)
            {
                CommandType = System.Data.CommandType.StoredProcedure
            };

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                expenses.Add(MapExpense(reader));
            }
        }
        catch (Exception ex)
        {
            HandleError(ex, "Failed to retrieve expenses");
            return GetDummyExpenses();
        }

        return expenses;
    }

    public async Task<Expense?> GetExpenseByIdAsync(int expenseId)
    {
        try
        {
            using var connection = CreateConnection();
            await connection.OpenAsync();
            IsConnected = true;
            LastError = null;

            using var command = new SqlCommand("dbo.GetExpenseById", connection)
            {
                CommandType = System.Data.CommandType.StoredProcedure
            };
            command.Parameters.AddWithValue("@ExpenseId", expenseId);

            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return MapExpense(reader);
            }
        }
        catch (Exception ex)
        {
            HandleError(ex, $"Failed to retrieve expense {expenseId}");
        }

        return null;
    }

    public async Task<List<Expense>> GetExpensesByStatusAsync(string statusName)
    {
        var expenses = new List<Expense>();
        
        try
        {
            using var connection = CreateConnection();
            await connection.OpenAsync();
            IsConnected = true;
            LastError = null;

            using var command = new SqlCommand("dbo.GetExpensesByStatus", connection)
            {
                CommandType = System.Data.CommandType.StoredProcedure
            };
            command.Parameters.AddWithValue("@StatusName", statusName);

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                expenses.Add(MapExpense(reader));
            }
        }
        catch (Exception ex)
        {
            HandleError(ex, $"Failed to retrieve expenses by status '{statusName}'");
            return GetDummyExpenses().Where(e => e.StatusName == statusName).ToList();
        }

        return expenses;
    }

    public async Task<List<Expense>> GetExpensesByUserAsync(int userId)
    {
        var expenses = new List<Expense>();
        
        try
        {
            using var connection = CreateConnection();
            await connection.OpenAsync();
            IsConnected = true;
            LastError = null;

            using var command = new SqlCommand("dbo.GetExpensesByUser", connection)
            {
                CommandType = System.Data.CommandType.StoredProcedure
            };
            command.Parameters.AddWithValue("@UserId", userId);

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                expenses.Add(MapExpense(reader));
            }
        }
        catch (Exception ex)
        {
            HandleError(ex, $"Failed to retrieve expenses for user {userId}");
        }

        return expenses;
    }

    public async Task<List<ExpenseSummary>> GetExpenseSummaryAsync()
    {
        var summaries = new List<ExpenseSummary>();
        
        try
        {
            using var connection = CreateConnection();
            await connection.OpenAsync();
            IsConnected = true;
            LastError = null;

            using var command = new SqlCommand("dbo.GetExpenseSummary", connection)
            {
                CommandType = System.Data.CommandType.StoredProcedure
            };

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                summaries.Add(new ExpenseSummary
                {
                    StatusName = reader.GetString(reader.GetOrdinal("StatusName")),
                    Count = reader.GetInt32(reader.GetOrdinal("ExpenseCount")),
                    TotalAmount = reader.GetDecimal(reader.GetOrdinal("TotalAmount"))
                });
            }
        }
        catch (Exception ex)
        {
            HandleError(ex, "Failed to retrieve expense summary");
            return GetDummySummary();
        }

        return summaries;
    }

    public async Task<int> CreateExpenseAsync(ExpenseCreateRequest request)
    {
        try
        {
            using var connection = CreateConnection();
            await connection.OpenAsync();
            IsConnected = true;
            LastError = null;

            using var command = new SqlCommand("dbo.CreateExpense", connection)
            {
                CommandType = System.Data.CommandType.StoredProcedure
            };
            command.Parameters.AddWithValue("@UserId", request.UserId);
            command.Parameters.AddWithValue("@CategoryId", request.CategoryId);
            command.Parameters.AddWithValue("@AmountMinor", (int)(request.Amount * 100));
            command.Parameters.AddWithValue("@Currency", request.Currency);
            command.Parameters.AddWithValue("@ExpenseDate", request.ExpenseDate);
            command.Parameters.AddWithValue("@Description", (object?)request.Description ?? DBNull.Value);
            command.Parameters.AddWithValue("@ReceiptFile", (object?)request.ReceiptFile ?? DBNull.Value);

            var result = await command.ExecuteScalarAsync();
            return Convert.ToInt32(result);
        }
        catch (Exception ex)
        {
            HandleError(ex, "Failed to create expense");
            return -1;
        }
    }

    public async Task<bool> SubmitExpenseAsync(int expenseId)
    {
        try
        {
            using var connection = CreateConnection();
            await connection.OpenAsync();
            IsConnected = true;
            LastError = null;

            using var command = new SqlCommand("dbo.SubmitExpense", connection)
            {
                CommandType = System.Data.CommandType.StoredProcedure
            };
            command.Parameters.AddWithValue("@ExpenseId", expenseId);

            var result = await command.ExecuteScalarAsync();
            return Convert.ToInt32(result) > 0;
        }
        catch (Exception ex)
        {
            HandleError(ex, $"Failed to submit expense {expenseId}");
            return false;
        }
    }

    public async Task<bool> ApproveExpenseAsync(int expenseId, int reviewedBy)
    {
        try
        {
            using var connection = CreateConnection();
            await connection.OpenAsync();
            IsConnected = true;
            LastError = null;

            using var command = new SqlCommand("dbo.ApproveExpense", connection)
            {
                CommandType = System.Data.CommandType.StoredProcedure
            };
            command.Parameters.AddWithValue("@ExpenseId", expenseId);
            command.Parameters.AddWithValue("@ReviewedBy", reviewedBy);

            var result = await command.ExecuteScalarAsync();
            return Convert.ToInt32(result) > 0;
        }
        catch (Exception ex)
        {
            HandleError(ex, $"Failed to approve expense {expenseId}");
            return false;
        }
    }

    public async Task<bool> RejectExpenseAsync(int expenseId, int reviewedBy)
    {
        try
        {
            using var connection = CreateConnection();
            await connection.OpenAsync();
            IsConnected = true;
            LastError = null;

            using var command = new SqlCommand("dbo.RejectExpense", connection)
            {
                CommandType = System.Data.CommandType.StoredProcedure
            };
            command.Parameters.AddWithValue("@ExpenseId", expenseId);
            command.Parameters.AddWithValue("@ReviewedBy", reviewedBy);

            var result = await command.ExecuteScalarAsync();
            return Convert.ToInt32(result) > 0;
        }
        catch (Exception ex)
        {
            HandleError(ex, $"Failed to reject expense {expenseId}");
            return false;
        }
    }

    public async Task<bool> DeleteExpenseAsync(int expenseId)
    {
        try
        {
            using var connection = CreateConnection();
            await connection.OpenAsync();
            IsConnected = true;
            LastError = null;

            using var command = new SqlCommand("dbo.DeleteExpense", connection)
            {
                CommandType = System.Data.CommandType.StoredProcedure
            };
            command.Parameters.AddWithValue("@ExpenseId", expenseId);

            var result = await command.ExecuteScalarAsync();
            return Convert.ToInt32(result) > 0;
        }
        catch (Exception ex)
        {
            HandleError(ex, $"Failed to delete expense {expenseId}");
            return false;
        }
    }

    public async Task<List<Category>> GetCategoriesAsync()
    {
        var categories = new List<Category>();
        
        try
        {
            using var connection = CreateConnection();
            await connection.OpenAsync();
            IsConnected = true;
            LastError = null;

            using var command = new SqlCommand("dbo.GetCategories", connection)
            {
                CommandType = System.Data.CommandType.StoredProcedure
            };

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                categories.Add(new Category
                {
                    CategoryId = reader.GetInt32(reader.GetOrdinal("CategoryId")),
                    CategoryName = reader.GetString(reader.GetOrdinal("CategoryName")),
                    IsActive = reader.GetBoolean(reader.GetOrdinal("IsActive"))
                });
            }
        }
        catch (Exception ex)
        {
            HandleError(ex, "Failed to retrieve categories");
            return GetDummyCategories();
        }

        return categories;
    }

    public async Task<List<User>> GetUsersAsync()
    {
        var users = new List<User>();
        
        try
        {
            using var connection = CreateConnection();
            await connection.OpenAsync();
            IsConnected = true;
            LastError = null;

            using var command = new SqlCommand("dbo.GetUsers", connection)
            {
                CommandType = System.Data.CommandType.StoredProcedure
            };

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                users.Add(new User
                {
                    UserId = reader.GetInt32(reader.GetOrdinal("UserId")),
                    UserName = reader.GetString(reader.GetOrdinal("UserName")),
                    Email = reader.GetString(reader.GetOrdinal("Email")),
                    RoleId = reader.GetInt32(reader.GetOrdinal("RoleId")),
                    RoleName = reader.GetString(reader.GetOrdinal("RoleName")),
                    ManagerId = reader.IsDBNull(reader.GetOrdinal("ManagerId")) ? null : reader.GetInt32(reader.GetOrdinal("ManagerId")),
                    ManagerName = reader.IsDBNull(reader.GetOrdinal("ManagerName")) ? null : reader.GetString(reader.GetOrdinal("ManagerName")),
                    IsActive = reader.GetBoolean(reader.GetOrdinal("IsActive")),
                    CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt"))
                });
            }
        }
        catch (Exception ex)
        {
            HandleError(ex, "Failed to retrieve users");
            return GetDummyUsers();
        }

        return users;
    }

    public async Task<List<Status>> GetStatusesAsync()
    {
        var statuses = new List<Status>();
        
        try
        {
            using var connection = CreateConnection();
            await connection.OpenAsync();
            IsConnected = true;
            LastError = null;

            using var command = new SqlCommand("dbo.GetStatuses", connection)
            {
                CommandType = System.Data.CommandType.StoredProcedure
            };

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                statuses.Add(new Status
                {
                    StatusId = reader.GetInt32(reader.GetOrdinal("StatusId")),
                    StatusName = reader.GetString(reader.GetOrdinal("StatusName"))
                });
            }
        }
        catch (Exception ex)
        {
            HandleError(ex, "Failed to retrieve statuses");
            return GetDummyStatuses();
        }

        return statuses;
    }

    private Expense MapExpense(SqlDataReader reader)
    {
        return new Expense
        {
            ExpenseId = reader.GetInt32(reader.GetOrdinal("ExpenseId")),
            UserId = reader.GetInt32(reader.GetOrdinal("UserId")),
            UserName = reader.GetString(reader.GetOrdinal("UserName")),
            UserEmail = reader.GetString(reader.GetOrdinal("UserEmail")),
            CategoryId = reader.GetInt32(reader.GetOrdinal("CategoryId")),
            CategoryName = reader.GetString(reader.GetOrdinal("CategoryName")),
            StatusId = reader.GetInt32(reader.GetOrdinal("StatusId")),
            StatusName = reader.GetString(reader.GetOrdinal("StatusName")),
            AmountMinor = reader.GetInt32(reader.GetOrdinal("AmountMinor")),
            Amount = reader.GetDecimal(reader.GetOrdinal("AmountDecimal")),
            Currency = reader.GetString(reader.GetOrdinal("Currency")),
            ExpenseDate = reader.GetDateTime(reader.GetOrdinal("ExpenseDate")),
            Description = reader.IsDBNull(reader.GetOrdinal("Description")) ? null : reader.GetString(reader.GetOrdinal("Description")),
            ReceiptFile = reader.IsDBNull(reader.GetOrdinal("ReceiptFile")) ? null : reader.GetString(reader.GetOrdinal("ReceiptFile")),
            SubmittedAt = reader.IsDBNull(reader.GetOrdinal("SubmittedAt")) ? null : reader.GetDateTime(reader.GetOrdinal("SubmittedAt")),
            ReviewedBy = reader.IsDBNull(reader.GetOrdinal("ReviewedBy")) ? null : reader.GetInt32(reader.GetOrdinal("ReviewedBy")),
            ReviewerName = reader.IsDBNull(reader.GetOrdinal("ReviewedByName")) ? null : reader.GetString(reader.GetOrdinal("ReviewedByName")),
            ReviewedAt = reader.IsDBNull(reader.GetOrdinal("ReviewedAt")) ? null : reader.GetDateTime(reader.GetOrdinal("ReviewedAt")),
            CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt"))
        };
    }

    // Dummy data methods for graceful fallback
    private List<Expense> GetDummyExpenses()
    {
        return new List<Expense>
        {
            new Expense { ExpenseId = 1, UserName = "Alice Example", CategoryName = "Travel", Amount = 123.00m, Currency = "GBP", StatusName = "Approved", ExpenseDate = DateTime.Now.AddDays(-10), Description = "Travel for meeting", CreatedAt = DateTime.Now.AddDays(-10) },
            new Expense { ExpenseId = 2, UserName = "Alice Example", CategoryName = "Supplies", Amount = 1.00m, Currency = "GBP", StatusName = "Approved", ExpenseDate = DateTime.Now.AddDays(-8), Description = "Office supplies", CreatedAt = DateTime.Now.AddDays(-8) },
            new Expense { ExpenseId = 3, UserName = "Bob Manager", CategoryName = "Travel", Amount = 234.00m, Currency = "GBP", StatusName = "Draft", ExpenseDate = DateTime.Now.AddDays(-5), Description = "Meeting", CreatedAt = DateTime.Now.AddDays(-5) },
            new Expense { ExpenseId = 4, UserName = "Alice Example", CategoryName = "Travel", Amount = 250.00m, Currency = "GBP", StatusName = "Submitted", ExpenseDate = DateTime.Now.AddDays(-3), Description = "Client dinner meeting", CreatedAt = DateTime.Now.AddDays(-3) }
        };
    }

    private List<ExpenseSummary> GetDummySummary()
    {
        return new List<ExpenseSummary>
        {
            new ExpenseSummary { StatusName = "Approved", Count = 6, TotalAmount = 519.24m },
            new ExpenseSummary { StatusName = "Draft", Count = 3, TotalAmount = 492.00m },
            new ExpenseSummary { StatusName = "Submitted", Count = 1, TotalAmount = 25.40m }
        };
    }

    private List<Category> GetDummyCategories()
    {
        return new List<Category>
        {
            new Category { CategoryId = 1, CategoryName = "Travel", IsActive = true },
            new Category { CategoryId = 2, CategoryName = "Meals", IsActive = true },
            new Category { CategoryId = 3, CategoryName = "Supplies", IsActive = true },
            new Category { CategoryId = 4, CategoryName = "Accommodation", IsActive = true },
            new Category { CategoryId = 5, CategoryName = "Other", IsActive = true }
        };
    }

    private List<User> GetDummyUsers()
    {
        return new List<User>
        {
            new User { UserId = 1, UserName = "Alice Example", Email = "alice@example.co.uk", RoleName = "Employee", IsActive = true },
            new User { UserId = 2, UserName = "Bob Manager", Email = "bob.manager@example.co.uk", RoleName = "Manager", IsActive = true }
        };
    }

    private List<Status> GetDummyStatuses()
    {
        return new List<Status>
        {
            new Status { StatusId = 1, StatusName = "Draft" },
            new Status { StatusId = 2, StatusName = "Submitted" },
            new Status { StatusId = 3, StatusName = "Approved" },
            new Status { StatusId = 4, StatusName = "Rejected" }
        };
    }
}
