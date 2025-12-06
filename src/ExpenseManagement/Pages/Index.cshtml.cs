using Microsoft.AspNetCore.Mvc.RazorPages;
using ExpenseManagement.Services;

namespace ExpenseManagement.Pages;

public class IndexModel : PageModel
{
    private readonly IExpenseService _expenseService;
    private readonly ILogger<IndexModel> _logger;

    public IndexModel(IExpenseService expenseService, ILogger<IndexModel> logger)
    {
        _expenseService = expenseService;
        _logger = logger;
    }

    public int TotalExpenses { get; set; }
    public int PendingExpenses { get; set; }
    public int ApprovedExpenses { get; set; }
    public string? ErrorMessage { get; set; }

    public async Task OnGetAsync()
    {
        try
        {
            var allExpenses = await _expenseService.GetAllExpensesAsync();
            TotalExpenses = allExpenses.Count();
            PendingExpenses = allExpenses.Count(e => e.StatusName == "Submitted");
            ApprovedExpenses = allExpenses.Count(e => e.StatusName == "Approved");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading dashboard data");
            ErrorMessage = "Unable to connect to database. Using sample data for demonstration.";
        }
    }
}
