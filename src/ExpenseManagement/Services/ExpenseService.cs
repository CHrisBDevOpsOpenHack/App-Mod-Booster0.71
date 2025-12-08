using System.Diagnostics;
using ExpenseManagement.Models;
using Microsoft.Data.SqlClient;

namespace ExpenseManagement.Services;

public class ExpenseService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<ExpenseService> _logger;

    public ExpenseService(IConfiguration configuration, ILogger<ExpenseService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<OperationResult<IReadOnlyList<ExpenseRecord>>> GetExpensesAsync(string? status = null, int? userId = null, string? searchTerm = null)
    {
        try
        {
            await using var connection = CreateConnection();
            await connection.OpenAsync();
            await using var command = new SqlCommand("dbo.GetExpenses", connection)
            {
                CommandType = System.Data.CommandType.StoredProcedure
            };

            command.Parameters.AddWithValue("@StatusName", (object?)status ?? DBNull.Value);
            command.Parameters.AddWithValue("@UserId", (object?)userId ?? DBNull.Value);
            command.Parameters.AddWithValue("@SearchTerm", (object?)searchTerm ?? DBNull.Value);

            var results = new List<ExpenseRecord>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                results.Add(new ExpenseRecord
                {
                    ExpenseId = reader.GetInt32(reader.GetOrdinal("ExpenseId")),
                    UserId = reader.GetInt32(reader.GetOrdinal("UserId")),
                    UserName = reader.GetString(reader.GetOrdinal("UserName")),
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
                    ReviewedByName = reader.IsDBNull(reader.GetOrdinal("ReviewedByName")) ? null : reader.GetString(reader.GetOrdinal("ReviewedByName")),
                    ReviewedAt = reader.IsDBNull(reader.GetOrdinal("ReviewedAt")) ? null : reader.GetDateTime(reader.GetOrdinal("ReviewedAt")),
                    CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt"))
                });
            }

            return OperationResult<IReadOnlyList<ExpenseRecord>>.FromSuccess(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve expenses");
            return OperationResult<IReadOnlyList<ExpenseRecord>>.FromError(BuildError(ex), DummyExpenses());
        }
    }

    public async Task<OperationResult<IReadOnlyList<ExpenseSummary>>> GetExpenseSummaryAsync()
    {
        try
        {
            await using var connection = CreateConnection();
            await connection.OpenAsync();
            await using var command = new SqlCommand("dbo.GetExpenseSummary", connection)
            {
                CommandType = System.Data.CommandType.StoredProcedure
            };

            var results = new List<ExpenseSummary>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                results.Add(new ExpenseSummary
                {
                    StatusName = reader.GetString(reader.GetOrdinal("StatusName")),
                    Count = reader.GetInt32(reader.GetOrdinal("ExpenseCount")),
                    TotalAmount = reader.GetDecimal(reader.GetOrdinal("TotalAmount"))
                });
            }

            return OperationResult<IReadOnlyList<ExpenseSummary>>.FromSuccess(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve expense summary");
            return OperationResult<IReadOnlyList<ExpenseSummary>>.FromError(BuildError(ex), DummySummary());
        }
    }

    public async Task<OperationResult<IReadOnlyList<ExpenseCategory>>> GetCategoriesAsync()
    {
        try
        {
            await using var connection = CreateConnection();
            await connection.OpenAsync();
            await using var command = new SqlCommand("dbo.GetExpenseCategories", connection)
            {
                CommandType = System.Data.CommandType.StoredProcedure
            };

            var categories = new List<ExpenseCategory>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                categories.Add(new ExpenseCategory
                {
                    CategoryId = reader.GetInt32(reader.GetOrdinal("CategoryId")),
                    CategoryName = reader.GetString(reader.GetOrdinal("CategoryName")),
                    IsActive = reader.GetBoolean(reader.GetOrdinal("IsActive"))
                });
            }

            return OperationResult<IReadOnlyList<ExpenseCategory>>.FromSuccess(categories);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve categories");
            return OperationResult<IReadOnlyList<ExpenseCategory>>.FromError(BuildError(ex), DummyCategories());
        }
    }

    public async Task<OperationResult<IReadOnlyList<ExpenseStatusModel>>> GetStatusesAsync()
    {
        try
        {
            await using var connection = CreateConnection();
            await connection.OpenAsync();
            await using var command = new SqlCommand("dbo.GetExpenseStatuses", connection)
            {
                CommandType = System.Data.CommandType.StoredProcedure
            };

            var statuses = new List<ExpenseStatusModel>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                statuses.Add(new ExpenseStatusModel
                {
                    StatusId = reader.GetInt32(reader.GetOrdinal("StatusId")),
                    StatusName = reader.GetString(reader.GetOrdinal("StatusName"))
                });
            }

            return OperationResult<IReadOnlyList<ExpenseStatusModel>>.FromSuccess(statuses);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve statuses");
            return OperationResult<IReadOnlyList<ExpenseStatusModel>>.FromError(BuildError(ex), DummyStatuses());
        }
    }

    public async Task<OperationResult<int>> CreateExpenseAsync(CreateExpenseRequest request)
    {
        try
        {
            var amountMinor = Convert.ToInt32(Math.Round(request.Amount * 100, MidpointRounding.AwayFromZero));
            await using var connection = CreateConnection();
            await connection.OpenAsync();
            await using var command = new SqlCommand("dbo.CreateExpense", connection)
            {
                CommandType = System.Data.CommandType.StoredProcedure
            };

            command.Parameters.AddWithValue("@UserId", request.UserId);
            command.Parameters.AddWithValue("@CategoryId", request.CategoryId);
            command.Parameters.AddWithValue("@AmountMinor", amountMinor);
            command.Parameters.AddWithValue("@Currency", request.Currency);
            command.Parameters.AddWithValue("@ExpenseDate", request.ExpenseDate);
            command.Parameters.AddWithValue("@Description", (object?)request.Description ?? DBNull.Value);
            command.Parameters.AddWithValue("@ReceiptFile", (object?)request.ReceiptFile ?? DBNull.Value);

            var result = await command.ExecuteScalarAsync();
            var newId = result is null ? 0 : Convert.ToInt32(result);
            return OperationResult<int>.FromSuccess(newId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create expense");
            return OperationResult<int>.FromError(BuildError(ex), 0);
        }
    }

    public async Task<OperationResult<bool>> UpdateExpenseStatusAsync(int expenseId, UpdateExpenseStatusRequest request)
    {
        try
        {
            await using var connection = CreateConnection();
            await connection.OpenAsync();
            await using var command = new SqlCommand("dbo.UpdateExpenseStatus", connection)
            {
                CommandType = System.Data.CommandType.StoredProcedure
            };

            command.Parameters.AddWithValue("@ExpenseId", expenseId);
            command.Parameters.AddWithValue("@StatusName", request.StatusName);
            command.Parameters.AddWithValue("@ReviewerId", (object?)request.ReviewerId ?? DBNull.Value);

            await command.ExecuteNonQueryAsync();
            return OperationResult<bool>.FromSuccess(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update expense status");
            return OperationResult<bool>.FromError(BuildError(ex), false);
        }
    }

    private SqlConnection CreateConnection()
    {
        var connectionString = _configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("DefaultConnection is not configured.");
        }
        return new SqlConnection(connectionString);
    }

    private ErrorInfo BuildError(Exception ex)
    {
        var stackTrace = new StackTrace(ex, true);
        var frame = stackTrace.GetFrames()?.FirstOrDefault(f => !string.IsNullOrWhiteSpace(f.GetFileName()));
        var guidance = BuildGuidance(ex);

        return new ErrorInfo
        {
            Message = ex.Message,
            File = frame != null ? Path.GetFileName(frame.GetFileName()) : null,
            LineNumber = frame?.GetFileLineNumber(),
            Guidance = guidance
        };
    }

    private string? BuildGuidance(Exception ex)
    {
        var connectionString = _configuration.GetConnectionString("DefaultConnection") ?? string.Empty;
        var message = ex.Message.ToLowerInvariant();
        var hasClientId = !string.IsNullOrWhiteSpace(_configuration["AZURE_CLIENT_ID"]) ||
                          !string.IsNullOrWhiteSpace(_configuration["ManagedIdentityClientId"]);

        if (!hasClientId)
        {
            return "Set AZURE_CLIENT_ID and ManagedIdentityClientId to the managed identity client ID.";
        }

        if (!connectionString.Contains("User Id", StringComparison.OrdinalIgnoreCase))
        {
            return "Update the connection string to include User Id={managed-identity-client-id}.";
        }

        if (message.Contains("managed identity") || message.Contains("access token"))
        {
            return "Confirm the managed identity has database permissions and the ConnectionStrings__DefaultConnection uses Active Directory Managed Identity.";
        }

        return null;
    }

    private static IReadOnlyList<ExpenseRecord> DummyExpenses()
    {
        return new List<ExpenseRecord>
        {
            new()
            {
                ExpenseId = 1,
                UserId = 1,
                UserName = "Alice Example",
                CategoryId = 1,
                CategoryName = "Travel",
                StatusId = 2,
                StatusName = "Submitted",
                AmountMinor = 2540,
                Amount = 25.40m,
                Currency = "GBP",
                ExpenseDate = DateTime.UtcNow.Date.AddDays(-3),
                Description = "Taxi from airport to client site",
                SubmittedAt = DateTime.UtcNow.AddDays(-2),
                CreatedAt = DateTime.UtcNow.AddDays(-3)
            },
            new()
            {
                ExpenseId = 2,
                UserId = 1,
                UserName = "Alice Example",
                CategoryId = 2,
                CategoryName = "Meals",
                StatusId = 3,
                StatusName = "Approved",
                AmountMinor = 1425,
                Amount = 14.25m,
                Currency = "GBP",
                ExpenseDate = DateTime.UtcNow.Date.AddDays(-15),
                Description = "Client lunch meeting",
                SubmittedAt = DateTime.UtcNow.AddDays(-14),
                ReviewedAt = DateTime.UtcNow.AddDays(-13),
                ReviewedByName = "Bob Manager",
                CreatedAt = DateTime.UtcNow.AddDays(-15)
            }
        };
    }

    private static IReadOnlyList<ExpenseCategory> DummyCategories() =>
        new List<ExpenseCategory>
        {
            new() { CategoryId = 1, CategoryName = "Travel", IsActive = true },
            new() { CategoryId = 2, CategoryName = "Meals", IsActive = true },
            new() { CategoryId = 3, CategoryName = "Supplies", IsActive = true }
        };

    private static IReadOnlyList<ExpenseStatusModel> DummyStatuses() =>
        new List<ExpenseStatusModel>
        {
            new() { StatusId = 1, StatusName = "Draft" },
            new() { StatusId = 2, StatusName = "Submitted" },
            new() { StatusId = 3, StatusName = "Approved" },
            new() { StatusId = 4, StatusName = "Rejected" }
        };

    private static IReadOnlyList<ExpenseSummary> DummySummary() =>
        new List<ExpenseSummary>
        {
            new() { StatusName = "Submitted", Count = 1, TotalAmount = 25.40m },
            new() { StatusName = "Approved", Count = 1, TotalAmount = 14.25m }
        };
}
