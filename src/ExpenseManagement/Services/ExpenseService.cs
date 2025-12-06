using Microsoft.Data.SqlClient;
using System.Data;
using ExpenseManagement.Models;

namespace ExpenseManagement.Services;

public interface IExpenseService
{
    Task<IEnumerable<Expense>> GetAllExpensesAsync();
    Task<IEnumerable<Expense>> GetExpensesByStatusAsync(string statusName);
    Task<Expense?> GetExpenseByIdAsync(int expenseId);
    Task<Expense> CreateExpenseAsync(CreateExpenseRequest request);
    Task<Expense> UpdateExpenseAsync(UpdateExpenseRequest request);
    Task<Expense> SubmitExpenseAsync(int expenseId);
    Task<Expense> ApproveExpenseAsync(ApproveExpenseRequest request);
    Task<Expense> RejectExpenseAsync(ApproveExpenseRequest request);
    Task<IEnumerable<Expense>> SearchExpensesAsync(string filterText);
    Task<int> DeleteExpenseAsync(int expenseId);
    Task<IEnumerable<ExpenseCategory>> GetAllCategoriesAsync();
    Task<IEnumerable<User>> GetAllUsersAsync();
    Task<IEnumerable<ExpenseStatus>> GetAllStatusesAsync();
}

public class ExpenseService : IExpenseService
{
    private readonly string _connectionString;
    private readonly ILogger<ExpenseService> _logger;

    public ExpenseService(IConfiguration configuration, ILogger<ExpenseService> logger)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection") 
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
        _logger = logger;
    }

    private async Task<SqlConnection> GetConnectionAsync()
    {
        var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        return connection;
    }

    public async Task<IEnumerable<Expense>> GetAllExpensesAsync()
    {
        try
        {
            using var connection = await GetConnectionAsync();
            using var command = new SqlCommand("GetAllExpenses", connection)
            {
                CommandType = CommandType.StoredProcedure
            };

            var expenses = new List<Expense>();
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                expenses.Add(MapExpenseFromReader(reader));
            }

            return expenses;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving expenses from database");
            return GetDummyExpenses();
        }
    }

    public async Task<IEnumerable<Expense>> GetExpensesByStatusAsync(string statusName)
    {
        try
        {
            using var connection = await GetConnectionAsync();
            using var command = new SqlCommand("GetExpensesByStatus", connection)
            {
                CommandType = CommandType.StoredProcedure
            };
            command.Parameters.AddWithValue("@StatusName", statusName);

            var expenses = new List<Expense>();
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                expenses.Add(MapExpenseFromReader(reader));
            }

            return expenses;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving expenses by status from database");
            return GetDummyExpenses().Where(e => e.StatusName == statusName);
        }
    }

    public async Task<Expense?> GetExpenseByIdAsync(int expenseId)
    {
        try
        {
            using var connection = await GetConnectionAsync();
            using var command = new SqlCommand("GetExpenseById", connection)
            {
                CommandType = CommandType.StoredProcedure
            };
            command.Parameters.AddWithValue("@ExpenseId", expenseId);

            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return MapExpenseFromReader(reader);
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving expense by ID from database");
            return GetDummyExpenses().FirstOrDefault(e => e.ExpenseId == expenseId);
        }
    }

    public async Task<Expense> CreateExpenseAsync(CreateExpenseRequest request)
    {
        try
        {
            using var connection = await GetConnectionAsync();
            using var command = new SqlCommand("CreateExpense", connection)
            {
                CommandType = CommandType.StoredProcedure
            };

            command.Parameters.AddWithValue("@UserId", request.UserId);
            command.Parameters.AddWithValue("@CategoryId", request.CategoryId);
            command.Parameters.AddWithValue("@AmountMinor", (int)(request.Amount * 100));
            command.Parameters.AddWithValue("@Currency", request.Currency);
            command.Parameters.AddWithValue("@ExpenseDate", request.ExpenseDate);
            command.Parameters.AddWithValue("@Description", (object?)request.Description ?? DBNull.Value);
            command.Parameters.AddWithValue("@ReceiptFile", (object?)request.ReceiptFile ?? DBNull.Value);
            command.Parameters.AddWithValue("@StatusName", request.StatusName);

            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return MapExpenseFromReader(reader);
            }

            throw new InvalidOperationException("Failed to create expense");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating expense in database");
            throw;
        }
    }

    public async Task<Expense> UpdateExpenseAsync(UpdateExpenseRequest request)
    {
        try
        {
            using var connection = await GetConnectionAsync();
            using var command = new SqlCommand("UpdateExpense", connection)
            {
                CommandType = CommandType.StoredProcedure
            };

            command.Parameters.AddWithValue("@ExpenseId", request.ExpenseId);
            command.Parameters.AddWithValue("@CategoryId", request.CategoryId);
            command.Parameters.AddWithValue("@AmountMinor", (int)(request.Amount * 100));
            command.Parameters.AddWithValue("@Currency", request.Currency);
            command.Parameters.AddWithValue("@ExpenseDate", request.ExpenseDate);
            command.Parameters.AddWithValue("@Description", (object?)request.Description ?? DBNull.Value);
            command.Parameters.AddWithValue("@ReceiptFile", (object?)request.ReceiptFile ?? DBNull.Value);

            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return MapExpenseFromReader(reader);
            }

            throw new InvalidOperationException("Failed to update expense");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating expense in database");
            throw;
        }
    }

    public async Task<Expense> SubmitExpenseAsync(int expenseId)
    {
        try
        {
            using var connection = await GetConnectionAsync();
            using var command = new SqlCommand("SubmitExpense", connection)
            {
                CommandType = CommandType.StoredProcedure
            };
            command.Parameters.AddWithValue("@ExpenseId", expenseId);

            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return MapExpenseFromReader(reader);
            }

            throw new InvalidOperationException("Failed to submit expense");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting expense in database");
            throw;
        }
    }

    public async Task<Expense> ApproveExpenseAsync(ApproveExpenseRequest request)
    {
        try
        {
            using var connection = await GetConnectionAsync();
            using var command = new SqlCommand("ApproveExpense", connection)
            {
                CommandType = CommandType.StoredProcedure
            };
            command.Parameters.AddWithValue("@ExpenseId", request.ExpenseId);
            command.Parameters.AddWithValue("@ReviewedBy", request.ReviewedBy);

            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return MapExpenseFromReader(reader);
            }

            throw new InvalidOperationException("Failed to approve expense");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error approving expense in database");
            throw;
        }
    }

    public async Task<Expense> RejectExpenseAsync(ApproveExpenseRequest request)
    {
        try
        {
            using var connection = await GetConnectionAsync();
            using var command = new SqlCommand("RejectExpense", connection)
            {
                CommandType = CommandType.StoredProcedure
            };
            command.Parameters.AddWithValue("@ExpenseId", request.ExpenseId);
            command.Parameters.AddWithValue("@ReviewedBy", request.ReviewedBy);

            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return MapExpenseFromReader(reader);
            }

            throw new InvalidOperationException("Failed to reject expense");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rejecting expense in database");
            throw;
        }
    }

    public async Task<IEnumerable<Expense>> SearchExpensesAsync(string filterText)
    {
        try
        {
            using var connection = await GetConnectionAsync();
            using var command = new SqlCommand("SearchExpenses", connection)
            {
                CommandType = CommandType.StoredProcedure
            };
            command.Parameters.AddWithValue("@FilterText", filterText);

            var expenses = new List<Expense>();
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                expenses.Add(MapExpenseFromReader(reader));
            }

            return expenses;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching expenses in database");
            return GetDummyExpenses().Where(e => 
                e.Description?.Contains(filterText, StringComparison.OrdinalIgnoreCase) == true ||
                e.CategoryName?.Contains(filterText, StringComparison.OrdinalIgnoreCase) == true);
        }
    }

    public async Task<int> DeleteExpenseAsync(int expenseId)
    {
        try
        {
            using var connection = await GetConnectionAsync();
            using var command = new SqlCommand("DeleteExpense", connection)
            {
                CommandType = CommandType.StoredProcedure
            };
            command.Parameters.AddWithValue("@ExpenseId", expenseId);

            var result = await command.ExecuteScalarAsync();
            return Convert.ToInt32(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting expense from database");
            throw;
        }
    }

    public async Task<IEnumerable<ExpenseCategory>> GetAllCategoriesAsync()
    {
        try
        {
            using var connection = await GetConnectionAsync();
            using var command = new SqlCommand("GetAllCategories", connection)
            {
                CommandType = CommandType.StoredProcedure
            };

            var categories = new List<ExpenseCategory>();
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                categories.Add(new ExpenseCategory
                {
                    CategoryId = reader.GetInt32(0),
                    CategoryName = reader.GetString(1),
                    IsActive = reader.GetBoolean(2)
                });
            }

            return categories;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving categories from database");
            return GetDummyCategories();
        }
    }

    public async Task<IEnumerable<User>> GetAllUsersAsync()
    {
        try
        {
            using var connection = await GetConnectionAsync();
            using var command = new SqlCommand("GetAllUsers", connection)
            {
                CommandType = CommandType.StoredProcedure
            };

            var users = new List<User>();
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                users.Add(new User
                {
                    UserId = reader.GetInt32(0),
                    UserName = reader.GetString(1),
                    Email = reader.GetString(2),
                    RoleName = reader.GetString(3),
                    IsActive = reader.GetBoolean(4),
                    CreatedAt = reader.GetDateTime(5),
                    ManagerName = reader.IsDBNull(6) ? null : reader.GetString(6)
                });
            }

            return users;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving users from database");
            return GetDummyUsers();
        }
    }

    public async Task<IEnumerable<ExpenseStatus>> GetAllStatusesAsync()
    {
        try
        {
            using var connection = await GetConnectionAsync();
            using var command = new SqlCommand("GetAllStatuses", connection)
            {
                CommandType = CommandType.StoredProcedure
            };

            var statuses = new List<ExpenseStatus>();
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                statuses.Add(new ExpenseStatus
                {
                    StatusId = reader.GetInt32(0),
                    StatusName = reader.GetString(1)
                });
            }

            return statuses;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving statuses from database");
            return GetDummyStatuses();
        }
    }

    private static Expense MapExpenseFromReader(SqlDataReader reader)
    {
        return new Expense
        {
            ExpenseId = reader.GetInt32(reader.GetOrdinal("ExpenseId")),
            AmountMinor = reader.GetInt32(reader.GetOrdinal("AmountMinor")),
            Currency = reader.GetString(reader.GetOrdinal("Currency")),
            ExpenseDate = reader.GetDateTime(reader.GetOrdinal("ExpenseDate")),
            Description = reader.IsDBNull(reader.GetOrdinal("Description")) ? null : reader.GetString(reader.GetOrdinal("Description")),
            ReceiptFile = reader.IsDBNull(reader.GetOrdinal("ReceiptFile")) ? null : reader.GetString(reader.GetOrdinal("ReceiptFile")),
            SubmittedAt = reader.IsDBNull(reader.GetOrdinal("SubmittedAt")) ? null : reader.GetDateTime(reader.GetOrdinal("SubmittedAt")),
            ReviewedAt = reader.IsDBNull(reader.GetOrdinal("ReviewedAt")) ? null : reader.GetDateTime(reader.GetOrdinal("ReviewedAt")),
            CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
            UserName = reader.GetString(reader.GetOrdinal("UserName")),
            Email = reader.GetString(reader.GetOrdinal("Email")),
            CategoryName = reader.GetString(reader.GetOrdinal("CategoryName")),
            StatusName = reader.GetString(reader.GetOrdinal("StatusName")),
            ReviewerName = reader.IsDBNull(reader.GetOrdinal("ReviewerName")) ? null : reader.GetString(reader.GetOrdinal("ReviewerName"))
        };
    }

    // Dummy data methods for fallback when database is unavailable
    private static IEnumerable<Expense> GetDummyExpenses()
    {
        return new List<Expense>
        {
            new() { ExpenseId = 1, AmountMinor = 12000, Currency = "GBP", ExpenseDate = DateTime.Today.AddDays(-5), Description = "Travel to client site", CategoryName = "Travel", StatusName = "Submitted", UserName = "Alice Example", Email = "alice@example.co.uk" },
            new() { ExpenseId = 2, AmountMinor = 6900, Currency = "GBP", ExpenseDate = DateTime.Today.AddDays(-3), Description = "Team lunch", CategoryName = "Meals", StatusName = "Submitted", UserName = "Alice Example", Email = "alice@example.co.uk" },
            new() { ExpenseId = 3, AmountMinor = 9950, Currency = "GBP", ExpenseDate = DateTime.Today.AddDays(-10), Description = "Office supplies", CategoryName = "Supplies", StatusName = "Approved", UserName = "Alice Example", Email = "alice@example.co.uk", ReviewerName = "Bob Manager" }
        };
    }

    private static IEnumerable<ExpenseCategory> GetDummyCategories()
    {
        return new List<ExpenseCategory>
        {
            new() { CategoryId = 1, CategoryName = "Travel", IsActive = true },
            new() { CategoryId = 2, CategoryName = "Meals", IsActive = true },
            new() { CategoryId = 3, CategoryName = "Supplies", IsActive = true },
            new() { CategoryId = 4, CategoryName = "Accommodation", IsActive = true },
            new() { CategoryId = 5, CategoryName = "Other", IsActive = true }
        };
    }

    private static IEnumerable<User> GetDummyUsers()
    {
        return new List<User>
        {
            new() { UserId = 1, UserName = "Alice Example", Email = "alice@example.co.uk", RoleName = "Employee", IsActive = true, CreatedAt = DateTime.UtcNow, ManagerName = "Bob Manager" },
            new() { UserId = 2, UserName = "Bob Manager", Email = "bob.manager@example.co.uk", RoleName = "Manager", IsActive = true, CreatedAt = DateTime.UtcNow }
        };
    }

    private static IEnumerable<ExpenseStatus> GetDummyStatuses()
    {
        return new List<ExpenseStatus>
        {
            new() { StatusId = 1, StatusName = "Draft" },
            new() { StatusId = 2, StatusName = "Submitted" },
            new() { StatusId = 3, StatusName = "Approved" },
            new() { StatusId = 4, StatusName = "Rejected" }
        };
    }
}
