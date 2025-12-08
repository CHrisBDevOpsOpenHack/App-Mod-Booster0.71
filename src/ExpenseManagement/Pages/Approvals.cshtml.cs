using ExpenseManagement.Models;
using ExpenseManagement.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ExpenseManagement.Pages;

public class ApprovalsModel : PageModel
{
    private readonly IExpenseService _expenseService;

    public ApprovalsModel(IExpenseService expenseService)
    {
        _expenseService = expenseService;
    }

    public List<Expense> PendingExpenses { get; set; } = new();

    public async Task OnGetAsync()
    {
        PendingExpenses = await _expenseService.GetExpensesByStatusAsync("Submitted");

        if (!_expenseService.IsConnected && _expenseService.LastError != null)
        {
            ViewData["Error"] = _expenseService.LastError.Message;
            ViewData["ErrorGuidance"] = _expenseService.LastError.Guidance;
        }
    }

    public async Task<IActionResult> OnPostApproveAsync(int expenseId)
    {
        // Using manager user ID 2 (Bob Manager) for demo purposes
        await _expenseService.ApproveExpenseAsync(expenseId, 2);
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRejectAsync(int expenseId)
    {
        // Using manager user ID 2 (Bob Manager) for demo purposes
        await _expenseService.RejectExpenseAsync(expenseId, 2);
        return RedirectToPage();
    }
}
