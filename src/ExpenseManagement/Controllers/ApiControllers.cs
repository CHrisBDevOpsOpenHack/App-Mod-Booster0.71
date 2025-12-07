using Microsoft.AspNetCore.Mvc;
using ExpenseManagement.Models;
using ExpenseManagement.Services;

namespace ExpenseManagement.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class ExpensesController : ControllerBase
{
    private readonly ExpenseService _expenseService;
    private readonly ILogger<ExpensesController> _logger;

    public ExpensesController(ExpenseService expenseService, ILogger<ExpensesController> logger)
    {
        _expenseService = expenseService;
        _logger = logger;
    }

    /// <summary>
    /// Get all expenses
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<Expense>>> GetAll()
    {
        try
        {
            var expenses = await _expenseService.GetAllExpensesAsync();
            return Ok(expenses);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all expenses");
            return StatusCode(500, new { error = "Failed to retrieve expenses", details = ex.Message });
        }
    }

    /// <summary>
    /// Get expenses by status
    /// </summary>
    /// <param name="status">Status name (Draft, Submitted, Approved, Rejected)</param>
    [HttpGet("status/{status}")]
    public async Task<ActionResult<List<Expense>>> GetByStatus(string status)
    {
        try
        {
            var expenses = await _expenseService.GetExpensesByStatusAsync(status);
            return Ok(expenses);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting expenses by status: {Status}", status);
            return StatusCode(500, new { error = "Failed to retrieve expenses", details = ex.Message });
        }
    }

    /// <summary>
    /// Get expenses by user ID
    /// </summary>
    [HttpGet("user/{userId}")]
    public async Task<ActionResult<List<Expense>>> GetByUser(int userId)
    {
        try
        {
            var expenses = await _expenseService.GetExpensesByUserAsync(userId);
            return Ok(expenses);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting expenses by user: {UserId}", userId);
            return StatusCode(500, new { error = "Failed to retrieve expenses", details = ex.Message });
        }
    }

    /// <summary>
    /// Get a single expense by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<Expense>> GetById(int id)
    {
        try
        {
            var expense = await _expenseService.GetExpenseByIdAsync(id);
            if (expense == null)
            {
                return NotFound(new { error = "Expense not found" });
            }
            return Ok(expense);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting expense by ID: {ExpenseId}", id);
            return StatusCode(500, new { error = "Failed to retrieve expense", details = ex.Message });
        }
    }

    /// <summary>
    /// Create a new expense
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<int>> Create([FromBody] CreateExpenseRequest request)
    {
        try
        {
            var expenseId = await _expenseService.CreateExpenseAsync(request);
            return CreatedAtAction(nameof(GetById), new { id = expenseId }, new { expenseId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating expense");
            return StatusCode(500, new { error = "Failed to create expense", details = ex.Message });
        }
    }

    /// <summary>
    /// Submit an expense for approval
    /// </summary>
    [HttpPost("{id}/submit")]
    public async Task<IActionResult> Submit(int id)
    {
        try
        {
            await _expenseService.SubmitExpenseAsync(id);
            return Ok(new { message = "Expense submitted successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting expense: {ExpenseId}", id);
            return StatusCode(500, new { error = "Failed to submit expense", details = ex.Message });
        }
    }

    /// <summary>
    /// Approve an expense
    /// </summary>
    [HttpPost("{id}/approve")]
    public async Task<IActionResult> Approve(int id, [FromBody] ReviewExpenseRequest request)
    {
        try
        {
            await _expenseService.ApproveExpenseAsync(id, request.ReviewerId);
            return Ok(new { message = "Expense approved successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error approving expense: {ExpenseId}", id);
            return StatusCode(500, new { error = "Failed to approve expense", details = ex.Message });
        }
    }

    /// <summary>
    /// Reject an expense
    /// </summary>
    [HttpPost("{id}/reject")]
    public async Task<IActionResult> Reject(int id, [FromBody] ReviewExpenseRequest request)
    {
        try
        {
            await _expenseService.RejectExpenseAsync(id, request.ReviewerId);
            return Ok(new { message = "Expense rejected successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rejecting expense: {ExpenseId}", id);
            return StatusCode(500, new { error = "Failed to reject expense", details = ex.Message });
        }
    }

    /// <summary>
    /// Update an expense
    /// </summary>
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateExpenseRequest request)
    {
        try
        {
            await _expenseService.UpdateExpenseAsync(id, request);
            return Ok(new { message = "Expense updated successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating expense: {ExpenseId}", id);
            return StatusCode(500, new { error = "Failed to update expense", details = ex.Message });
        }
    }

    /// <summary>
    /// Delete an expense
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            await _expenseService.DeleteExpenseAsync(id);
            return Ok(new { message = "Expense deleted successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting expense: {ExpenseId}", id);
            return StatusCode(500, new { error = "Failed to delete expense", details = ex.Message });
        }
    }

    /// <summary>
    /// Get expense summary statistics
    /// </summary>
    [HttpGet("summary")]
    public async Task<ActionResult<List<ExpenseSummary>>> GetSummary()
    {
        try
        {
            var summary = await _expenseService.GetExpenseSummaryAsync();
            return Ok(summary);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting expense summary");
            return StatusCode(500, new { error = "Failed to retrieve summary", details = ex.Message });
        }
    }
}

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class CategoriesController : ControllerBase
{
    private readonly ExpenseService _expenseService;
    private readonly ILogger<CategoriesController> _logger;

    public CategoriesController(ExpenseService expenseService, ILogger<CategoriesController> logger)
    {
        _expenseService = expenseService;
        _logger = logger;
    }

    /// <summary>
    /// Get all expense categories
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<ExpenseCategory>>> GetAll()
    {
        try
        {
            var categories = await _expenseService.GetAllCategoriesAsync();
            return Ok(categories);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting categories");
            return StatusCode(500, new { error = "Failed to retrieve categories", details = ex.Message });
        }
    }
}

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class UsersController : ControllerBase
{
    private readonly ExpenseService _expenseService;
    private readonly ILogger<UsersController> _logger;

    public UsersController(ExpenseService expenseService, ILogger<UsersController> logger)
    {
        _expenseService = expenseService;
        _logger = logger;
    }

    /// <summary>
    /// Get all users
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<User>>> GetAll()
    {
        try
        {
            var users = await _expenseService.GetAllUsersAsync();
            return Ok(users);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting users");
            return StatusCode(500, new { error = "Failed to retrieve users", details = ex.Message });
        }
    }
}

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class StatusesController : ControllerBase
{
    private readonly ExpenseService _expenseService;
    private readonly ILogger<StatusesController> _logger;

    public StatusesController(ExpenseService expenseService, ILogger<StatusesController> logger)
    {
        _expenseService = expenseService;
        _logger = logger;
    }

    /// <summary>
    /// Get all expense statuses
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<ExpenseStatus>>> GetAll()
    {
        try
        {
            var statuses = await _expenseService.GetAllStatusesAsync();
            return Ok(statuses);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting statuses");
            return StatusCode(500, new { error = "Failed to retrieve statuses", details = ex.Message });
        }
    }
}
