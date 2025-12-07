using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ExpenseManagement.Services;
using ExpenseManagement.Models;

namespace ExpenseManagement.Pages;

public class AddExpenseModel : PageModel
{
    private readonly ExpenseService _expenseService;
    private readonly ILogger<AddExpenseModel> _logger;

    public AddExpenseModel(ExpenseService expenseService, ILogger<AddExpenseModel> logger)
    {
        _expenseService = expenseService;
        _logger = logger;
    }

    [BindProperty]
    public int UserId { get; set; } = 1;
    
    [BindProperty]
    public int CategoryId { get; set; }
    
    [BindProperty]
    public decimal Amount { get; set; }
    
    [BindProperty]
    public DateTime ExpenseDate { get; set; } = DateTime.Now;
    
    [BindProperty]
    public string? Description { get; set; }

    public List<User>? Users { get; set; }
    public List<ExpenseCategory>? Categories { get; set; }
    public string? SuccessMessage { get; set; }
    public string? ErrorMessage { get; set; }

    public async Task OnGetAsync()
    {
        try
        {
            Users = await _expenseService.GetAllUsersAsync();
            Categories = await _expenseService.GetAllCategoriesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading form data");
            ErrorMessage = ex.Message;
        }
    }

    public async Task<IActionResult> OnPostAsync()
    {
        try
        {
            await OnGetAsync(); // Reload dropdowns
            
            if (!ModelState.IsValid)
            {
                return Page();
            }

            var request = new CreateExpenseRequest
            {
                UserId = UserId,
                CategoryId = CategoryId,
                Amount = Amount,
                ExpenseDate = ExpenseDate,
                Description = Description,
                Currency = "GBP"
            };

            var expenseId = await _expenseService.CreateExpenseAsync(request);
            await _expenseService.SubmitExpenseAsync(expenseId);

            SuccessMessage = $"Expense #{expenseId} created and submitted successfully!";
            
            // Reset form
            Amount = 0;
            Description = null;
            ExpenseDate = DateTime.Now;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating expense");
            ErrorMessage = ex.Message;
        }

        return Page();
    }
}
