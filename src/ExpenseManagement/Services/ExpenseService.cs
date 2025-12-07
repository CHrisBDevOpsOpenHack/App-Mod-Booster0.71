using Microsoft.Data.SqlClient;
using ExpenseManagement.Models;

namespace ExpenseManagement.Services;

public class ExpenseService
{
    private readonly string _connectionString;
    private readonly ILogger<ExpenseService> _logger;

    public ExpenseService(IConfiguration configuration, ILogger<ExpenseService> logger)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("DefaultConnection connection string is not configured");
        _logger = logger;
    }

    public bool IsDatabaseAvailable { get; private set; } = true;
    public string? LastError { get; private set; }

    private async Task<SqlConnection> GetConnectionAsync()
    {
        try
        {
            var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();
            IsDatabaseAvailable = true;
            LastError = null;
            return connection;
        }
        catch (Exception ex)
        {
            IsDatabaseAvailable = false;
            LastError = ex.Message;
            _logger.LogError(ex, "Failed to connect to database");
            throw;
        }
    }

    public async Task<List<Expense>> GetAllExpensesAsync()
    {
        var expenses = new List<Expense>();
        
        try
        {
            using var connection = await GetConnectionAsync();
            using var command = new SqlCommand("[dbo].[GetAllExpenses]", connection)
            {
                CommandType = System.Data.CommandType.StoredProcedure
            };

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                expenses.Add(MapExpenseFromReader(reader));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all expenses");
            throw;
        }

        return expenses;
    }

    public async Task<List<Expense>> GetExpensesByStatusAsync(string statusName)
    {
        var expenses = new List<Expense>();
        
        try
        {
            using var connection = await GetConnectionAsync();
            using var command = new SqlCommand("[dbo].[GetExpensesByStatus]", connection)
            {
                CommandType = System.Data.CommandType.StoredProcedure
            };
            command.Parameters.AddWithValue("@StatusName", statusName);

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                expenses.Add(MapExpenseFromReader(reader));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting expenses by status: {Status}", statusName);
            throw;
        }

        return expenses;
    }

    public async Task<List<Expense>> GetExpensesByUserAsync(int userId)
    {
        var expenses = new List<Expense>();
        
        try
        {
            using var connection = await GetConnectionAsync();
            using var command = new SqlCommand("[dbo].[GetExpensesByUser]", connection)
            {
                CommandType = System.Data.CommandType.StoredProcedure
            };
            command.Parameters.AddWithValue("@UserId", userId);

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                expenses.Add(MapExpenseFromReader(reader));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting expenses by user: {UserId}", userId);
            throw;
        }

        return expenses;
    }

    public async Task<Expense?> GetExpenseByIdAsync(int expenseId)
    {
        try
        {
            using var connection = await GetConnectionAsync();
            using var command = new SqlCommand("[dbo].[GetExpenseById]", connection)
            {
                CommandType = System.Data.CommandType.StoredProcedure
            };
            command.Parameters.AddWithValue("@ExpenseId", expenseId);

            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return MapExpenseFromReader(reader);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting expense by ID: {ExpenseId}", expenseId);
            throw;
        }

        return null;
    }

    public async Task<int> CreateExpenseAsync(CreateExpenseRequest request)
    {
        try
        {
            using var connection = await GetConnectionAsync();
            using var command = new SqlCommand("[dbo].[CreateExpense]", connection)
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
            _logger.LogError(ex, "Error creating expense");
            throw;
        }
    }

    public async Task SubmitExpenseAsync(int expenseId)
    {
        try
        {
            using var connection = await GetConnectionAsync();
            using var command = new SqlCommand("[dbo].[SubmitExpense]", connection)
            {
                CommandType = System.Data.CommandType.StoredProcedure
            };
            command.Parameters.AddWithValue("@ExpenseId", expenseId);

            await command.ExecuteNonQueryAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting expense: {ExpenseId}", expenseId);
            throw;
        }
    }

    public async Task ApproveExpenseAsync(int expenseId, int reviewerId)
    {
        try
        {
            using var connection = await GetConnectionAsync();
            using var command = new SqlCommand("[dbo].[ApproveExpense]", connection)
            {
                CommandType = System.Data.CommandType.StoredProcedure
            };
            command.Parameters.AddWithValue("@ExpenseId", expenseId);
            command.Parameters.AddWithValue("@ReviewerId", reviewerId);

            await command.ExecuteNonQueryAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error approving expense: {ExpenseId}", expenseId);
            throw;
        }
    }

    public async Task RejectExpenseAsync(int expenseId, int reviewerId)
    {
        try
        {
            using var connection = await GetConnectionAsync();
            using var command = new SqlCommand("[dbo].[RejectExpense]", connection)
            {
                CommandType = System.Data.CommandType.StoredProcedure
            };
            command.Parameters.AddWithValue("@ExpenseId", expenseId);
            command.Parameters.AddWithValue("@ReviewerId", reviewerId);

            await command.ExecuteNonQueryAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rejecting expense: {ExpenseId}", expenseId);
            throw;
        }
    }

    public async Task UpdateExpenseAsync(int expenseId, UpdateExpenseRequest request)
    {
        try
        {
            using var connection = await GetConnectionAsync();
            using var command = new SqlCommand("[dbo].[UpdateExpense]", connection)
            {
                CommandType = System.Data.CommandType.StoredProcedure
            };
            
            command.Parameters.AddWithValue("@ExpenseId", expenseId);
            command.Parameters.AddWithValue("@CategoryId", request.CategoryId);
            command.Parameters.AddWithValue("@AmountMinor", (int)(request.Amount * 100));
            command.Parameters.AddWithValue("@Currency", request.Currency);
            command.Parameters.AddWithValue("@ExpenseDate", request.ExpenseDate);
            command.Parameters.AddWithValue("@Description", (object?)request.Description ?? DBNull.Value);
            command.Parameters.AddWithValue("@ReceiptFile", (object?)request.ReceiptFile ?? DBNull.Value);

            await command.ExecuteNonQueryAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating expense: {ExpenseId}", expenseId);
            throw;
        }
    }

    public async Task DeleteExpenseAsync(int expenseId)
    {
        try
        {
            using var connection = await GetConnectionAsync();
            using var command = new SqlCommand("[dbo].[DeleteExpense]", connection)
            {
                CommandType = System.Data.CommandType.StoredProcedure
            };
            command.Parameters.AddWithValue("@ExpenseId", expenseId);

            await command.ExecuteNonQueryAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting expense: {ExpenseId}", expenseId);
            throw;
        }
    }

    public async Task<List<ExpenseCategory>> GetAllCategoriesAsync()
    {
        var categories = new List<ExpenseCategory>();
        
        try
        {
            using var connection = await GetConnectionAsync();
            using var command = new SqlCommand("[dbo].[GetAllCategories]", connection)
            {
                CommandType = System.Data.CommandType.StoredProcedure
            };

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
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting categories");
            throw;
        }

        return categories;
    }

    public async Task<List<User>> GetAllUsersAsync()
    {
        var users = new List<User>();
        
        try
        {
            using var connection = await GetConnectionAsync();
            using var command = new SqlCommand("[dbo].[GetAllUsers]", connection)
            {
                CommandType = System.Data.CommandType.StoredProcedure
            };

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                users.Add(new User
                {
                    UserId = reader.GetInt32(0),
                    UserName = reader.GetString(1),
                    Email = reader.GetString(2),
                    RoleId = reader.GetInt32(3),
                    RoleName = reader.GetString(4),
                    ManagerId = reader.IsDBNull(5) ? null : reader.GetInt32(5),
                    ManagerName = reader.IsDBNull(6) ? null : reader.GetString(6),
                    IsActive = reader.GetBoolean(7),
                    CreatedAt = reader.GetDateTime(8)
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting users");
            throw;
        }

        return users;
    }

    public async Task<List<ExpenseStatus>> GetAllStatusesAsync()
    {
        var statuses = new List<ExpenseStatus>();
        
        try
        {
            using var connection = await GetConnectionAsync();
            using var command = new SqlCommand("[dbo].[GetAllStatuses]", connection)
            {
                CommandType = System.Data.CommandType.StoredProcedure
            };

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                statuses.Add(new ExpenseStatus
                {
                    StatusId = reader.GetInt32(0),
                    StatusName = reader.GetString(1)
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting statuses");
            throw;
        }

        return statuses;
    }

    public async Task<List<ExpenseSummary>> GetExpenseSummaryAsync()
    {
        var summary = new List<ExpenseSummary>();
        
        try
        {
            using var connection = await GetConnectionAsync();
            using var command = new SqlCommand("[dbo].[GetExpenseSummary]", connection)
            {
                CommandType = System.Data.CommandType.StoredProcedure
            };

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                summary.Add(new ExpenseSummary
                {
                    StatusName = reader.GetString(0),
                    Count = reader.GetInt32(1),
                    TotalAmountMinor = reader.GetInt32(2),
                    TotalAmount = reader.GetDecimal(3)
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting expense summary");
            throw;
        }

        return summary;
    }

    private static Expense MapExpenseFromReader(SqlDataReader reader)
    {
        return new Expense
        {
            ExpenseId = reader.GetInt32(reader.GetOrdinal("ExpenseId")),
            UserId = reader.GetInt32(reader.GetOrdinal("UserId")),
            UserName = reader.GetString(reader.GetOrdinal("UserName")),
            Email = reader.GetString(reader.GetOrdinal("Email")),
            CategoryId = reader.GetInt32(reader.GetOrdinal("CategoryId")),
            CategoryName = reader.GetString(reader.GetOrdinal("CategoryName")),
            StatusId = reader.GetInt32(reader.GetOrdinal("StatusId")),
            StatusName = reader.GetString(reader.GetOrdinal("StatusName")),
            AmountMinor = reader.GetInt32(reader.GetOrdinal("AmountMinor")),
            Amount = reader.GetDecimal(reader.GetOrdinal("Amount")),
            Currency = reader.GetString(reader.GetOrdinal("Currency")),
            ExpenseDate = reader.GetDateTime(reader.GetOrdinal("ExpenseDate")),
            Description = reader.IsDBNull(reader.GetOrdinal("Description")) ? null : reader.GetString(reader.GetOrdinal("Description")),
            ReceiptFile = reader.IsDBNull(reader.GetOrdinal("ReceiptFile")) ? null : reader.GetString(reader.GetOrdinal("ReceiptFile")),
            SubmittedAt = reader.IsDBNull(reader.GetOrdinal("SubmittedAt")) ? null : reader.GetDateTime(reader.GetOrdinal("SubmittedAt")),
            ReviewedBy = reader.IsDBNull(reader.GetOrdinal("ReviewedBy")) ? null : reader.GetInt32(reader.GetOrdinal("ReviewedBy")),
            ReviewerName = reader.IsDBNull(reader.GetOrdinal("ReviewerName")) ? null : reader.GetString(reader.GetOrdinal("ReviewerName")),
            ReviewedAt = reader.IsDBNull(reader.GetOrdinal("ReviewedAt")) ? null : reader.GetDateTime(reader.GetOrdinal("ReviewedAt")),
            CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt"))
        };
    }
}
