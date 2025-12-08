using ExpenseManagement.Models;
using ExpenseManagement.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ExpenseManagement.Pages;

public class IndexModel : PageModel
{
    private readonly IExpenseService _expenseService;

    public IndexModel(IExpenseService expenseService)
    {
        _expenseService = expenseService;
    }

    public List<Expense> RecentExpenses { get; set; } = new();
    public int TotalExpenses { get; set; }
    public int PendingApprovals { get; set; }
    public decimal ApprovedAmount { get; set; }
    public int ApprovedCount { get; set; }

    public async Task OnGetAsync()
    {
        var allExpenses = await _expenseService.GetAllExpensesAsync();
        var summary = await _expenseService.GetExpenseSummaryAsync();

        RecentExpenses = allExpenses.Take(10).ToList();
        TotalExpenses = allExpenses.Count;

        var submitted = summary.FirstOrDefault(s => s.StatusName == "Submitted");
        PendingApprovals = submitted?.Count ?? 0;

        var approved = summary.FirstOrDefault(s => s.StatusName == "Approved");
        ApprovedAmount = approved?.TotalAmount ?? 0;
        ApprovedCount = approved?.Count ?? 0;

        if (!_expenseService.IsConnected && _expenseService.LastError != null)
        {
            ViewData["Error"] = _expenseService.LastError.Message;
            ViewData["ErrorFile"] = _expenseService.LastError.File;
            ViewData["ErrorLine"] = _expenseService.LastError.LineNumber;
            ViewData["ErrorGuidance"] = _expenseService.LastError.Guidance;
        }
    }
}
