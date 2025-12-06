using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using ExpenseManagement.Models;
using ExpenseManagement.Services;

namespace ExpenseManagement.Pages;

public class AddExpenseModel : PageModel
{
    private readonly IExpenseService _expenseService;
    private readonly ILogger<AddExpenseModel> _logger;

    public AddExpenseModel(IExpenseService expenseService, ILogger<AddExpenseModel> logger)
    {
        _expenseService = expenseService;
        _logger = logger;
    }

    [BindProperty]
    public CreateExpenseRequest ExpenseRequest { get; set; } = new();

    public List<SelectListItem> Categories { get; set; } = new();
    public string? ErrorMessage { get; set; }
    public string? SuccessMessage { get; set; }

    public async Task OnGetAsync()
    {
        await LoadCategoriesAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            await LoadCategoriesAsync();
            return Page();
        }

        try
        {
            var expense = await _expenseService.CreateExpenseAsync(ExpenseRequest);
            SuccessMessage = $"Expense created successfully with ID: {expense.ExpenseId}";
            ExpenseRequest = new CreateExpenseRequest(); // Reset form
            await LoadCategoriesAsync();
            return Page();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating expense");
            ErrorMessage = "Failed to create expense. Please try again.";
            await LoadCategoriesAsync();
            return Page();
        }
    }

    private async Task LoadCategoriesAsync()
    {
        try
        {
            var categories = await _expenseService.GetAllCategoriesAsync();
            Categories = categories.Select(c => new SelectListItem
            {
                Value = c.CategoryId.ToString(),
                Text = c.CategoryName
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading categories");
            Categories = new List<SelectListItem>
            {
                new() { Value = "1", Text = "Travel" },
                new() { Value = "2", Text = "Meals" },
                new() { Value = "3", Text = "Supplies" }
            };
        }
    }
}
