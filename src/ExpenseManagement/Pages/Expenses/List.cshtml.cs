using ExpenseManagement.Models;
using ExpenseManagement.Services;

namespace ExpenseManagement.Pages.Expenses;

public class ListModel : PageModelBase
{
    private readonly ExpenseService _expenseService;
    private readonly ILogger<ListModel> _logger;

    public ListModel(ExpenseService expenseService, ILogger<ListModel> logger)
    {
        _expenseService = expenseService;
        _logger = logger;
    }

    public IReadOnlyList<ExpenseRecord> Expenses { get; private set; } = Array.Empty<ExpenseRecord>();
    public IReadOnlyList<ExpenseStatusModel> Statuses { get; private set; } = Array.Empty<ExpenseStatusModel>();

    public string? Status { get; private set; }
    public string? Search { get; private set; }

    public async Task OnGetAsync(string? status, string? search)
    {
        Status = status;
        Search = search;

        var statuses = await _expenseService.GetStatusesAsync();
        CaptureError(statuses);
        Statuses = statuses.Data ?? Array.Empty<ExpenseStatusModel>();

        var expenses = await _expenseService.GetExpensesAsync(status, null, search);
        CaptureError(expenses);
        Expenses = expenses.Data ?? Array.Empty<ExpenseRecord>();
    }
}
