using ExpenseManagement.Models;
using ExpenseManagement.Services;

namespace ExpenseManagement.Pages;

public class IndexModel : PageModelBase
{
    private readonly ExpenseService _expenseService;
    private readonly ILogger<IndexModel> _logger;

    public IndexModel(ExpenseService expenseService, ILogger<IndexModel> logger)
    {
        _expenseService = expenseService;
        _logger = logger;
    }

    public IReadOnlyList<ExpenseSummary> Summaries { get; private set; } = Array.Empty<ExpenseSummary>();
    public IReadOnlyList<ExpenseRecord> RecentExpenses { get; private set; } = Array.Empty<ExpenseRecord>();

    public async Task OnGetAsync()
    {
        var summary = await _expenseService.GetExpenseSummaryAsync();
        CaptureError(summary);
        Summaries = summary.Data ?? Array.Empty<ExpenseSummary>();

        var expenses = await _expenseService.GetExpensesAsync(null, null, null);
        CaptureError(expenses);
        RecentExpenses = expenses.Data?.Take(5).ToList() ?? new List<ExpenseRecord>();
    }
}
