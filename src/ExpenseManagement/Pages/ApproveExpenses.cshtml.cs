using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ExpenseManagement.Services;
using ExpenseManagement.Models;

namespace ExpenseManagement.Pages;

public class ApproveExpensesModel : PageModel
{
    private readonly ExpenseService _expenseService;
    private readonly ILogger<ApproveExpensesModel> _logger;

    public ApproveExpensesModel(ExpenseService expenseService, ILogger<ApproveExpensesModel> logger)
    {
        _expenseService = expenseService;
        _logger = logger;
    }

    public List<Expense>? Expenses { get; set; }

    public async Task OnGetAsync()
    {
        try
        {
            Expenses = await _expenseService.GetExpensesByStatusAsync("Submitted");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading expenses");
            Expenses = new List<Expense>();
        }
    }

    public async Task<IActionResult> OnPostAsync(int expenseId, string action)
    {
        const int managerId = 2; // Bob Manager

        try
        {
            if (action == "approve")
            {
                await _expenseService.ApproveExpenseAsync(expenseId, managerId);
            }
            else if (action == "reject")
            {
                await _expenseService.RejectExpenseAsync(expenseId, managerId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing expense approval");
        }

        return RedirectToPage();
    }
}
