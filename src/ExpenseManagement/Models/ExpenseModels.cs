namespace ExpenseManagement.Models;

public class ExpenseRecord
{
    public int ExpenseId { get; set; }
    public int UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public int StatusId { get; set; }
    public string StatusName { get; set; } = string.Empty;
    public int AmountMinor { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "GBP";
    public DateTime ExpenseDate { get; set; }
    public string? Description { get; set; }
    public string? ReceiptFile { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public int? ReviewedBy { get; set; }
    public string? ReviewedByName { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ExpenseCategory
{
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

public class ExpenseStatusModel
{
    public int StatusId { get; set; }
    public string StatusName { get; set; } = string.Empty;
}

public class ExpenseSummary
{
    public string StatusName { get; set; } = string.Empty;
    public int Count { get; set; }
    public decimal TotalAmount { get; set; }
    public int TotalAmountMinor => (int)Math.Round(TotalAmount * 100, MidpointRounding.AwayFromZero);
}

public class CreateExpenseRequest
{
    public int UserId { get; set; }
    public int CategoryId { get; set; }
    public decimal Amount { get; set; }
    public DateTime ExpenseDate { get; set; } = DateTime.UtcNow.Date;
    public string Currency { get; set; } = "GBP";
    public string Description { get; set; } = string.Empty;
    public string? ReceiptFile { get; set; }
}

public class UpdateExpenseStatusRequest
{
    public string StatusName { get; set; } = string.Empty;
    public int? ReviewerId { get; set; }
}

public class ErrorInfo
{
    public string Message { get; set; } = string.Empty;
    public string? File { get; set; }
    public int? LineNumber { get; set; }
    public string? Guidance { get; set; }
}

public class OperationResult<T>
{
    public bool Success { get; private set; }
    public T? Data { get; private set; }
    public ErrorInfo? Error { get; private set; }

    public static OperationResult<T> FromSuccess(T data) => new()
    {
        Success = true,
        Data = data
    };

    public static OperationResult<T> FromError(ErrorInfo error, T? fallback = default) => new()
    {
        Success = false,
        Error = error,
        Data = fallback
    };
}
