using Microsoft.AspNetCore.Mvc.RazorPages;
using ExpenseManagement.Services;
using ExpenseManagement.Models;

namespace ExpenseManagement.Pages;

public class ViewExpensesModel : PageModel
{
    private readonly ExpenseService _expenseService;
    private readonly ILogger<ViewExpensesModel> _logger;

    public ViewExpensesModel(ExpenseService expenseService, ILogger<ViewExpensesModel> logger)
    {
        _expenseService = expenseService;
        _logger = logger;
    }

    public List<Expense>? Expenses { get; set; }
    public string? ErrorMessage { get; set; }

    public async Task OnGetAsync()
    {
        try
        {
            Expenses = await _expenseService.GetAllExpensesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading expenses");
            ErrorMessage = ex.Message;
            Expenses = new List<Expense>();
        }
    }
}
