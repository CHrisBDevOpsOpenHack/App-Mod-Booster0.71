using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ExpenseManagement.Models;
using ExpenseManagement.Services;

namespace ExpenseManagement.Pages;

public class ApproveExpensesModel : PageModel
{
    private readonly IExpenseService _expenseService;
    private readonly ILogger<ApproveExpensesModel> _logger;

    public ApproveExpensesModel(IExpenseService expenseService, ILogger<ApproveExpensesModel> logger)
    {
        _expenseService = expenseService;
        _logger = logger;
    }

    public List<Expense> PendingExpenses { get; set; } = new();
    
    [BindProperty(SupportsGet = true)]
    public string? FilterText { get; set; }

    public string? ErrorMessage { get; set; }
    public string? SuccessMessage { get; set; }

    public async Task OnGetAsync()
    {
        await LoadPendingExpensesAsync();
    }

    public async Task<IActionResult> OnPostApproveAsync(int expenseId)
    {
        try
        {
            var request = new ApproveExpenseRequest { ExpenseId = expenseId, ReviewedBy = 2 };
            await _expenseService.ApproveExpenseAsync(request);
            SuccessMessage = "Expense approved successfully";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error approving expense");
            ErrorMessage = "Failed to approve expense";
        }

        await LoadPendingExpensesAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostRejectAsync(int expenseId)
    {
        try
        {
            var request = new ApproveExpenseRequest { ExpenseId = expenseId, ReviewedBy = 2 };
            await _expenseService.RejectExpenseAsync(request);
            SuccessMessage = "Expense rejected successfully";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rejecting expense");
            ErrorMessage = "Failed to reject expense";
        }

        await LoadPendingExpensesAsync();
        return Page();
    }

    private async Task LoadPendingExpensesAsync()
    {
        try
        {
            var allPending = await _expenseService.GetExpensesByStatusAsync("Submitted");
            
            if (!string.IsNullOrWhiteSpace(FilterText))
            {
                PendingExpenses = allPending.Where(e =>
                    (e.Description?.Contains(FilterText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (e.CategoryName?.Contains(FilterText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (e.UserName?.Contains(FilterText, StringComparison.OrdinalIgnoreCase) ?? false)
                ).ToList();
            }
            else
            {
                PendingExpenses = allPending.ToList();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading pending expenses");
            ErrorMessage = "Unable to load pending expenses from database";
        }
    }
}
