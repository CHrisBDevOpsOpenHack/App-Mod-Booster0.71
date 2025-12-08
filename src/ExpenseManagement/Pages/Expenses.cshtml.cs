using ExpenseManagement.Models;
using ExpenseManagement.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ExpenseManagement.Pages;

public class ExpensesModel : PageModel
{
    private readonly IExpenseService _expenseService;

    public ExpensesModel(IExpenseService expenseService)
    {
        _expenseService = expenseService;
    }

    public List<Expense> Expenses { get; set; } = new();
    public List<Status> Statuses { get; set; } = new();
    public string? SelectedStatus { get; set; }

    public async Task OnGetAsync(string? status)
    {
        SelectedStatus = status;
        Statuses = await _expenseService.GetStatusesAsync();

        if (!string.IsNullOrEmpty(status))
        {
            Expenses = await _expenseService.GetExpensesByStatusAsync(status);
        }
        else
        {
            Expenses = await _expenseService.GetAllExpensesAsync();
        }

        if (!_expenseService.IsConnected && _expenseService.LastError != null)
        {
            ViewData["Error"] = _expenseService.LastError.Message;
            ViewData["ErrorGuidance"] = _expenseService.LastError.Guidance;
        }
    }

    public async Task<IActionResult> OnPostSubmitAsync(int expenseId)
    {
        await _expenseService.SubmitExpenseAsync(expenseId);
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int expenseId)
    {
        await _expenseService.DeleteExpenseAsync(expenseId);
        return RedirectToPage();
    }
}
