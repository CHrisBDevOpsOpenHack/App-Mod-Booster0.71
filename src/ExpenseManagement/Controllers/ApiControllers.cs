using Microsoft.AspNetCore.Mvc;
using ExpenseManagement.Models;
using ExpenseManagement.Services;
using OpenAI.Chat;

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
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<List<Expense>>> GetAllExpenses()
    {
        try
        {
            var expenses = await _expenseService.GetAllExpensesAsync();
            return Ok(expenses);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all expenses");
            return StatusCode(500, new { error = "An error occurred while retrieving expenses" });
        }
    }

    /// <summary>
    /// Get expenses by status
    /// </summary>
    /// <param name="status">Status name (Draft, Submitted, Approved, Rejected)</param>
    [HttpGet("status/{status}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<List<Expense>>> GetExpensesByStatus(string status)
    {
        try
        {
            var expenses = await _expenseService.GetExpensesByStatusAsync(status);
            return Ok(expenses);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting expenses by status: {Status}", status);
            return StatusCode(500, new { error = "An error occurred while retrieving expenses" });
        }
    }

    /// <summary>
    /// Get expense by ID
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<Expense>> GetExpenseById(int id)
    {
        try
        {
            var expense = await _expenseService.GetExpenseByIdAsync(id);
            if (expense == null)
            {
                return NotFound(new { error = $"Expense {id} not found" });
            }
            return Ok(expense);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting expense {Id}", id);
            return StatusCode(500, new { error = "An error occurred while retrieving the expense" });
        }
    }

    /// <summary>
    /// Search expenses by description or user name
    /// </summary>
    [HttpGet("search")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<List<Expense>>> SearchExpenses([FromQuery] string term)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(term))
            {
                return BadRequest(new { error = "Search term is required" });
            }
            
            var expenses = await _expenseService.SearchExpensesAsync(term);
            return Ok(expenses);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching expenses: {Term}", term);
            return StatusCode(500, new { error = "An error occurred while searching expenses" });
        }
    }

    /// <summary>
    /// Create a new expense
    /// </summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<Expense>> CreateExpense([FromBody] Expense expense)
    {
        try
        {
            var created = await _expenseService.CreateExpenseAsync(expense);
            return CreatedAtAction(nameof(GetExpenseById), new { id = created.ExpenseId }, created);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating expense");
            return StatusCode(500, new { error = "An error occurred while creating the expense" });
        }
    }

    /// <summary>
    /// Update expense status (e.g., approve, reject, submit)
    /// </summary>
    [HttpPatch("{id}/status")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<Expense>> UpdateExpenseStatus(
        int id, 
        [FromBody] UpdateStatusRequest request)
    {
        try
        {
            var updated = await _expenseService.UpdateExpenseStatusAsync(
                id, 
                request.StatusName, 
                request.ReviewedBy);
            return Ok(updated);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating expense status");
            return StatusCode(500, new { error = "An error occurred while updating the expense status" });
        }
    }

    /// <summary>
    /// Delete an expense
    /// </summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DeleteExpense(int id)
    {
        try
        {
            var success = await _expenseService.DeleteExpenseAsync(id);
            if (!success)
            {
                return BadRequest(new { error = "Cannot delete expense with current status" });
            }
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting expense {Id}", id);
            return StatusCode(500, new { error = "An error occurred while deleting the expense" });
        }
    }
}

[ApiController]
[Route("api/[controller]")]
public class CategoriesController : ControllerBase
{
    private readonly ExpenseService _expenseService;

    public CategoriesController(ExpenseService expenseService)
    {
        _expenseService = expenseService;
    }

    /// <summary>
    /// Get all expense categories
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<ExpenseCategory>>> GetCategories()
    {
        var categories = await _expenseService.GetCategoriesAsync();
        return Ok(categories);
    }
}

[ApiController]
[Route("api/[controller]")]
public class StatusesController : ControllerBase
{
    private readonly ExpenseService _expenseService;

    public StatusesController(ExpenseService expenseService)
    {
        _expenseService = expenseService;
    }

    /// <summary>
    /// Get all expense statuses
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<ExpenseStatus>>> GetStatuses()
    {
        var statuses = await _expenseService.GetStatusesAsync();
        return Ok(statuses);
    }
}

[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly ExpenseService _expenseService;

    public UsersController(ExpenseService expenseService)
    {
        _expenseService = expenseService;
    }

    /// <summary>
    /// Get all users
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<User>>> GetUsers()
    {
        var users = await _expenseService.GetUsersAsync();
        return Ok(users);
    }
}

public record UpdateStatusRequest(string StatusName, int? ReviewedBy = null);

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
    /// Send a message to the AI chat assistant
    /// </summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ChatResponse>> SendMessage([FromBody] ChatRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Message))
            {
                return BadRequest(new { error = "Message cannot be empty" });
            }

            if (!_chatService.IsConfigured())
            {
                return BadRequest(new { error = "Azure OpenAI is not configured. Deploy with -DeployGenAI switch." });
            }

            // Convert conversation history to ChatMessage format
            var conversationHistory = new List<OpenAI.Chat.ChatMessage>();
            
            if (request.ConversationHistory != null)
            {
                foreach (var msg in request.ConversationHistory)
                {
                    if (msg.Role?.ToLower() == "user")
                    {
                        conversationHistory.Add(new OpenAI.Chat.UserChatMessage(msg.Content ?? ""));
                    }
                    else if (msg.Role?.ToLower() == "assistant")
                    {
                        conversationHistory.Add(new OpenAI.Chat.AssistantChatMessage(msg.Content ?? ""));
                    }
                }
            }

            var response = await _chatService.SendMessageAsync(request.Message, conversationHistory);

            return Ok(new ChatResponse(response));
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Chat service not configured");
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing chat message");
            return StatusCode(500, new { error = "An error occurred while processing your message" });
        }
    }
}

public record ChatRequest(string Message, List<ConversationMessage>? ConversationHistory = null);
public record ConversationMessage(string? Role, string? Content);
public record ChatResponse(string Response);
