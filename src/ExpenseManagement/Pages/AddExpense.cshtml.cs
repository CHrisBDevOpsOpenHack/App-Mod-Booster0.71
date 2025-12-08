using ExpenseManagement.Models;
using ExpenseManagement.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ExpenseManagement.Pages;

public class AddExpenseModel : PageModel
{
    private readonly IExpenseService _expenseService;

    public AddExpenseModel(IExpenseService expenseService)
    {
        _expenseService = expenseService;
    }

    public List<Category> Categories { get; set; } = new();
    public List<User> Users { get; set; } = new();

    public async Task OnGetAsync()
    {
        Categories = await _expenseService.GetCategoriesAsync();
        Users = await _expenseService.GetUsersAsync();

        if (!_expenseService.IsConnected && _expenseService.LastError != null)
        {
            ViewData["Error"] = _expenseService.LastError.Message;
            ViewData["ErrorGuidance"] = _expenseService.LastError.Guidance;
        }
    }

    public async Task<IActionResult> OnPostAsync(int userId, int categoryId, decimal amount, DateTime expenseDate, string? description)
    {
        var request = new ExpenseCreateRequest
        {
            UserId = userId,
            CategoryId = categoryId,
            Amount = amount,
            ExpenseDate = expenseDate,
            Description = description
        };

        var expenseId = await _expenseService.CreateExpenseAsync(request);
        
        if (expenseId > 0)
        {
            return RedirectToPage("/Expenses");
        }

        Categories = await _expenseService.GetCategoriesAsync();
        Users = await _expenseService.GetUsersAsync();
        
        ViewData["Error"] = "Failed to create expense";
        return Page();
    }
}
