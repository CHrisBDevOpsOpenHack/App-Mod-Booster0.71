using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ExpenseManagement.Services;
using ExpenseManagement.Models;

namespace ExpenseManagement.Pages;

public class IndexModel : PageModel
{
    private readonly ILogger<IndexModel> _logger;
    private readonly ExpenseService _expenseService;

    public IndexModel(ILogger<IndexModel> logger, ExpenseService expenseService)
    {
        _logger = logger;
        _expenseService = expenseService;
    }

    public List<ExpenseSummary>? Summary { get; set; }
    public string? ErrorMessage { get; set; }

    public async Task OnGetAsync()
    {
        try
        {
            Summary = await _expenseService.GetExpenseSummaryAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading dashboard");
            ErrorMessage = ex.Message;
            
            // Provide dummy data when database is unavailable
            Summary = new List<ExpenseSummary>
            {
                new ExpenseSummary { StatusName = "Draft", Count = 1, TotalAmount = 7.99m },
                new ExpenseSummary { StatusName = "Submitted", Count = 1, TotalAmount = 25.40m },
                new ExpenseSummary { StatusName = "Approved", Count = 2, TotalAmount = 137.25m },
                new ExpenseSummary { StatusName = "Rejected", Count = 0, TotalAmount = 0m }
            };
        }
    }
}
