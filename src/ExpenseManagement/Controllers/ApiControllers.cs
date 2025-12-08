using ExpenseManagement.Models;
using ExpenseManagement.Services;
using Microsoft.AspNetCore.Mvc;

namespace ExpenseManagement.Controllers;

[ApiController]
[Route("api/[controller]")]
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
    public async Task<ActionResult<List<Expense>>> GetAllExpenses()
    {
        try
        {
            var expenses = await _expenseService.GetAllExpensesAsync();
            return Ok(expenses);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving expenses");
            return StatusCode(500, new { error = "Failed to retrieve expenses", details = ex.Message });
        }
    }

    /// <summary>
    /// Get expense by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<Expense>> GetExpenseById(int id)
    {
        try
        {
            var expense = await _expenseService.GetExpenseByIdAsync(id);
            if (expense == null)
                return NotFound(new { error = $"Expense {id} not found" });
            
            return Ok(expense);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving expense {ExpenseId}", id);
            return StatusCode(500, new { error = "Failed to retrieve expense", details = ex.Message });
        }
    }

    /// <summary>
    /// Get expenses by status
    /// </summary>
    [HttpGet("status/{status}")]
    public async Task<ActionResult<List<Expense>>> GetExpensesByStatus(string status)
    {
        try
        {
            var expenses = await _expenseService.GetExpensesByStatusAsync(status);
            return Ok(expenses);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving expenses by status");
            return StatusCode(500, new { error = "Failed to retrieve expenses", details = ex.Message });
        }
    }

    /// <summary>
    /// Get expenses by user ID
    /// </summary>
    [HttpGet("user/{userId}")]
    public async Task<ActionResult<List<Expense>>> GetExpensesByUser(int userId)
    {
        try
        {
            var expenses = await _expenseService.GetExpensesByUserAsync(userId);
            return Ok(expenses);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving expenses for user");
            return StatusCode(500, new { error = "Failed to retrieve expenses", details = ex.Message });
        }
    }

    /// <summary>
    /// Create a new expense
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<int>> CreateExpense([FromBody] CreateExpenseRequest request)
    {
        try
        {
            var expenseId = await _expenseService.CreateExpenseAsync(request);
            return CreatedAtAction(nameof(GetExpenseById), new { id = expenseId }, new { expenseId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating expense");
            return StatusCode(500, new { error = "Failed to create expense", details = ex.Message });
        }
    }

    /// <summary>
    /// Update expense status
    /// </summary>
    [HttpPatch("{id}/status")]
    public async Task<IActionResult> UpdateExpenseStatus(int id, [FromBody] UpdateExpenseStatusRequest request)
    {
        try
        {
            await _expenseService.UpdateExpenseStatusAsync(id, request.StatusName, request.ReviewedBy);
            return Ok(new { success = true, message = "Expense status updated" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating expense status");
            return StatusCode(500, new { error = "Failed to update expense status", details = ex.Message });
        }
    }

    /// <summary>
    /// Delete an expense
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteExpense(int id)
    {
        try
        {
            await _expenseService.DeleteExpenseAsync(id);
            return Ok(new { success = true, message = "Expense deleted" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting expense");
            return StatusCode(500, new { error = "Failed to delete expense", details = ex.Message });
        }
    }

    /// <summary>
    /// Get expense summary by status
    /// </summary>
    [HttpGet("summary")]
    public async Task<ActionResult<List<ExpenseSummary>>> GetExpenseSummary()
    {
        try
        {
            var summary = await _expenseService.GetExpenseSummaryAsync();
            return Ok(summary);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving expense summary");
            return StatusCode(500, new { error = "Failed to retrieve summary", details = ex.Message });
        }
    }
}

[ApiController]
[Route("api/[controller]")]
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
    /// Get all categories
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<Category>>> GetCategories()
    {
        try
        {
            var categories = await _expenseService.GetCategoriesAsync();
            return Ok(categories);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving categories");
            return StatusCode(500, new { error = "Failed to retrieve categories", details = ex.Message });
        }
    }
}

[ApiController]
[Route("api/[controller]")]
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
    public async Task<ActionResult<List<User>>> GetUsers()
    {
        try
        {
            var users = await _expenseService.GetUsersAsync();
            return Ok(users);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving users");
            return StatusCode(500, new { error = "Failed to retrieve users", details = ex.Message });
        }
    }
}

[ApiController]
[Route("api/[controller]")]
public class ChatController : ControllerBase
{
    private readonly ChatService _chatService;
    private readonly ILogger<ChatController> _logger;

    public ChatController(ChatService chatService, ILogger<ChatController> logger)
    {
        _chatService = chatService;
        _logger = logger;
    }

    /// <summary>
    /// Send a message to the AI chat
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<string>> SendMessage([FromBody] ChatRequest request)
    {
        try
        {
            var response = await _chatService.SendMessageAsync(request.Message, request.History ?? new List<ChatMessage>());
            return Ok(new { response });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in chat");
            return StatusCode(500, new { error = "Chat error", details = ex.Message });
        }
    }

    /// <summary>
    /// Check if chat is configured
    /// </summary>
    [HttpGet("status")]
    public ActionResult<bool> GetChatStatus()
    {
        return Ok(new { configured = _chatService.IsConfigured });
    }
}

public class ChatRequest
{
    public string Message { get; set; } = string.Empty;
    public List<ChatMessage>? History { get; set; }
}
