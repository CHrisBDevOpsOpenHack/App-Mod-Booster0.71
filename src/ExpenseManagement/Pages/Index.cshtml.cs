using ExpenseManagement.Models;
using ExpenseManagement.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ExpenseManagement.Pages;

public class IndexModel : PageModel
{
    private readonly ExpenseService _expenseService;
    private readonly ILogger<IndexModel> _logger;

    public List<Expense>? Expenses { get; set; }
    public List<ExpenseSummary>? Summary { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ErrorLocation { get; set; }
    public string? ErrorGuidance { get; set; }
    public bool UseDummyData { get; set; }

    public IndexModel(ExpenseService expenseService, ILogger<IndexModel> logger)
    {
        _expenseService = expenseService;
        _logger = logger;
    }

    public async Task OnGetAsync()
    {
        try
        {
            Expenses = await _expenseService.GetAllExpensesAsync();
            Summary = await _expenseService.GetExpenseSummaryAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading expense data");
            
            // Parse error and provide helpful guidance
            ErrorMessage = ex.Message;
            ErrorLocation = $"{ex.Source}";
            
            if (ex.Message.Contains("Unable to load the proper Managed Identity") || 
                ex.Message.Contains("AZURE_CLIENT_ID"))
            {
                ErrorGuidance = "The AZURE_CLIENT_ID environment variable is not set. Run the infrastructure deployment script to configure this setting.";
            }
            else if (ex.Message.Contains("Login failed") || ex.Message.Contains("authentication"))
            {
                ErrorGuidance = "The managed identity does not have permission to access the database. Ensure the identity was granted db_datareader, db_datawriter, and EXECUTE permissions.";
            }
            else if (ex.Message.Contains("connection string"))
            {
                ErrorGuidance = "The connection string is not configured or is invalid. Check that ConnectionStrings__DefaultConnection is set in App Service configuration.";
            }
            else if (ex.Message.Contains("User Id"))
            {
                ErrorGuidance = "The connection string is missing the 'User Id' parameter with the managed identity client ID.";
            }

            // Use dummy data for demonstration
            UseDummyData = true;
            Expenses = GetDummyExpenses();
            Summary = GetDummySummary();
        }
    }

    private List<Expense> GetDummyExpenses()
    {
        return new List<Expense>
        {
            new Expense
            {
                ExpenseId = 1,
                UserName = "Alice Example",
                CategoryName = "Travel",
                Amount = 25.40m,
                Currency = "GBP",
                ExpenseDate = DateTime.Now.AddDays(-5),
                Description = "Taxi from airport to client site",
                StatusName = "Submitted"
            },
            new Expense
            {
                ExpenseId = 2,
                UserName = "Alice Example",
                CategoryName = "Meals",
                Amount = 14.25m,
                Currency = "GBP",
                ExpenseDate = DateTime.Now.AddDays(-10),
                Description = "Client lunch meeting",
                StatusName = "Approved"
            },
            new Expense
            {
                ExpenseId = 3,
                UserName = "Alice Example",
                CategoryName = "Supplies",
                Amount = 7.99m,
                Currency = "GBP",
                ExpenseDate = DateTime.Now.AddDays(-2),
                Description = "Office stationery",
                StatusName = "Draft"
            }
        };
    }

    private List<ExpenseSummary> GetDummySummary()
    {
        return new List<ExpenseSummary>
        {
            new ExpenseSummary { StatusName = "Draft", Count = 1, TotalAmount = 7.99m },
            new ExpenseSummary { StatusName = "Submitted", Count = 1, TotalAmount = 25.40m },
            new ExpenseSummary { StatusName = "Approved", Count = 1, TotalAmount = 14.25m },
            new ExpenseSummary { StatusName = "Rejected", Count = 0, TotalAmount = 0m }
        };
    }
}
