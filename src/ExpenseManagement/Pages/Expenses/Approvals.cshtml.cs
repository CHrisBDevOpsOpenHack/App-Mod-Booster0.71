using ExpenseManagement.Models;
using ExpenseManagement.Services;
using Microsoft.AspNetCore.Mvc;

namespace ExpenseManagement.Pages.Expenses;

public class ApprovalsModel : PageModelBase
{
    private readonly ExpenseService _expenseService;
    private readonly ILogger<ApprovalsModel> _logger;

    public ApprovalsModel(ExpenseService expenseService, ILogger<ApprovalsModel> logger)
    {
        _expenseService = expenseService;
        _logger = logger;
    }

    public IReadOnlyList<ExpenseRecord> Pending { get; private set; } = Array.Empty<ExpenseRecord>();

    [BindProperty]
    public int ReviewerId { get; set; } = 2;

    public async Task OnGetAsync()
    {
        await LoadPending();
    }

    public async Task<IActionResult> OnPostApproveAsync(int expenseId, int reviewerId)
    {
        ReviewerId = reviewerId;
        var result = await _expenseService.UpdateExpenseStatusAsync(expenseId, new UpdateExpenseStatusRequest
        {
            StatusName = "Approved",
            ReviewerId = reviewerId
        });
        CaptureError(result);
        await LoadPending();
        return Page();
    }

    public async Task<IActionResult> OnPostRejectAsync(int expenseId, int reviewerId)
    {
        ReviewerId = reviewerId;
        var result = await _expenseService.UpdateExpenseStatusAsync(expenseId, new UpdateExpenseStatusRequest
        {
            StatusName = "Rejected",
            ReviewerId = reviewerId
        });
        CaptureError(result);
        await LoadPending();
        return Page();
    }

    private async Task LoadPending()
    {
        var expenses = await _expenseService.GetExpensesAsync("Submitted", null, null);
        CaptureError(expenses);
        Pending = expenses.Data ?? Array.Empty<ExpenseRecord>();
    }
}
