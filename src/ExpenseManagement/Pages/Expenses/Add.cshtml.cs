using ExpenseManagement.Models;
using ExpenseManagement.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace ExpenseManagement.Pages.Expenses;

public class AddModel : PageModelBase
{
    private readonly ExpenseService _expenseService;
    private readonly ILogger<AddModel> _logger;

    public AddModel(ExpenseService expenseService, ILogger<AddModel> logger)
    {
        _expenseService = expenseService;
        _logger = logger;
    }

    [BindProperty]
    public CreateExpenseRequest Input { get; set; } = new() { ExpenseDate = DateTime.UtcNow.Date };

    public string? SuccessMessage { get; set; }
    public List<SelectListItem> CategoryOptions { get; private set; } = new();

    public async Task OnGetAsync()
    {
        await LoadCategories();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            await LoadCategories();
            return Page();
        }

        var result = await _expenseService.CreateExpenseAsync(Input);
        CaptureError(result);
        if (result.Success)
        {
            SuccessMessage = $"Expense #{result.Data} created.";
            ModelState.Clear();
            Input = new CreateExpenseRequest { ExpenseDate = DateTime.UtcNow.Date };
        }
        await LoadCategories();
        return Page();
    }

    private async Task LoadCategories()
    {
        var categories = await _expenseService.GetCategoriesAsync();
        CaptureError(categories);
        CategoryOptions = categories.Data?.Select(c => new SelectListItem
        {
            Value = c.CategoryId.ToString(),
            Text = c.CategoryName
        }).ToList() ?? new List<SelectListItem>();
    }
}
