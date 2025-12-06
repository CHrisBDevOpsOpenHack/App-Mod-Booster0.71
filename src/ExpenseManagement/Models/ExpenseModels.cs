namespace ExpenseManagement.Models;

public class Expense
{
    public int ExpenseId { get; set; }
    public int UserId { get; set; }
    public int CategoryId { get; set; }
    public int StatusId { get; set; }
    public int AmountMinor { get; set; }
    public string Currency { get; set; } = "GBP";
    public DateTime ExpenseDate { get; set; }
    public string? Description { get; set; }
    public string? ReceiptFile { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public int? ReviewedBy { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    
    // Navigation properties
    public string? UserName { get; set; }
    public string? Email { get; set; }
    public string? CategoryName { get; set; }
    public string? StatusName { get; set; }
    public string? ReviewerName { get; set; }
    
    // Calculated property for display
    public decimal AmountDecimal => AmountMinor / 100.0m;
}

public class ExpenseCategory
{
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

public class ExpenseStatus
{
    public int StatusId { get; set; }
    public string StatusName { get; set; } = string.Empty;
}

public class User
{
    public int UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string RoleName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? ManagerName { get; set; }
}

public class CreateExpenseRequest
{
    public int UserId { get; set; } = 1; // Default to first user (Alice)
    public int CategoryId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "GBP";
    public DateTime ExpenseDate { get; set; } = DateTime.Today;
    public string? Description { get; set; }
    public string? ReceiptFile { get; set; }
    public string StatusName { get; set; } = "Draft";
}

public class UpdateExpenseRequest
{
    public int ExpenseId { get; set; }
    public int CategoryId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "GBP";
    public DateTime ExpenseDate { get; set; }
    public string? Description { get; set; }
    public string? ReceiptFile { get; set; }
}

public class ApproveExpenseRequest
{
    public int ExpenseId { get; set; }
    public int ReviewedBy { get; set; } = 2; // Default to second user (Bob Manager)
}

public class ErrorInfo
{
    public string Message { get; set; } = string.Empty;
    public string? FileName { get; set; }
    public int? LineNumber { get; set; }
    public string? Guidance { get; set; }
}
