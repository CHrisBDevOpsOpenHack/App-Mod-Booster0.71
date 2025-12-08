using ExpenseManagement.Models;
using ExpenseManagement.Services;
using Microsoft.AspNetCore.Mvc;

namespace ExpenseManagement.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class ExpensesController : ControllerBase
{
    private readonly IExpenseService _expenseService;
    private readonly ILogger<ExpensesController> _logger;

    public ExpensesController(IExpenseService expenseService, ILogger<ExpensesController> logger)
    {
        _expenseService = expenseService;
        _logger = logger;
    }

    /// <summary>
    /// Get all expenses
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<Expense>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<Expense>>> GetAll()
    {
        var expenses = await _expenseService.GetAllExpensesAsync();
        return Ok(expenses);
    }

    /// <summary>
    /// Get expense by ID
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(Expense), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Expense>> GetById(int id)
    {
        var expense = await _expenseService.GetExpenseByIdAsync(id);
        if (expense == null)
            return NotFound();
        return Ok(expense);
    }

    /// <summary>
    /// Get expenses by status
    /// </summary>
    [HttpGet("status/{status}")]
    [ProducesResponseType(typeof(List<Expense>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<Expense>>> GetByStatus(string status)
    {
        var expenses = await _expenseService.GetExpensesByStatusAsync(status);
        return Ok(expenses);
    }

    /// <summary>
    /// Get expenses by user
    /// </summary>
    [HttpGet("user/{userId}")]
    [ProducesResponseType(typeof(List<Expense>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<Expense>>> GetByUser(int userId)
    {
        var expenses = await _expenseService.GetExpensesByUserAsync(userId);
        return Ok(expenses);
    }

    /// <summary>
    /// Get expense summary by status
    /// </summary>
    [HttpGet("summary")]
    [ProducesResponseType(typeof(List<ExpenseSummary>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<ExpenseSummary>>> GetSummary()
    {
        var summary = await _expenseService.GetExpenseSummaryAsync();
        return Ok(summary);
    }

    /// <summary>
    /// Create a new expense
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(object), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> Create([FromBody] ExpenseCreateRequest request)
    {
        var expenseId = await _expenseService.CreateExpenseAsync(request);
        if (expenseId <= 0)
            return BadRequest("Failed to create expense");
        
        return CreatedAtAction(nameof(GetById), new { id = expenseId }, new { expenseId });
    }

    /// <summary>
    /// Submit expense for approval
    /// </summary>
    [HttpPost("{id}/submit")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> Submit(int id)
    {
        var success = await _expenseService.SubmitExpenseAsync(id);
        if (!success)
            return BadRequest("Failed to submit expense");
        return Ok(new { message = "Expense submitted for approval" });
    }

    /// <summary>
    /// Approve an expense
    /// </summary>
    [HttpPost("{id}/approve")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> Approve(int id, [FromQuery] int reviewerId)
    {
        var success = await _expenseService.ApproveExpenseAsync(id, reviewerId);
        if (!success)
            return BadRequest("Failed to approve expense");
        return Ok(new { message = "Expense approved" });
    }

    /// <summary>
    /// Reject an expense
    /// </summary>
    [HttpPost("{id}/reject")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> Reject(int id, [FromQuery] int reviewerId)
    {
        var success = await _expenseService.RejectExpenseAsync(id, reviewerId);
        if (!success)
            return BadRequest("Failed to reject expense");
        return Ok(new { message = "Expense rejected" });
    }

    /// <summary>
    /// Delete an expense
    /// </summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> Delete(int id)
    {
        var success = await _expenseService.DeleteExpenseAsync(id);
        if (!success)
            return BadRequest("Failed to delete expense");
        return Ok(new { message = "Expense deleted" });
    }
}

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class CategoriesController : ControllerBase
{
    private readonly IExpenseService _expenseService;

    public CategoriesController(IExpenseService expenseService)
    {
        _expenseService = expenseService;
    }

    /// <summary>
    /// Get all expense categories
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<Category>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<Category>>> GetAll()
    {
        var categories = await _expenseService.GetCategoriesAsync();
        return Ok(categories);
    }
}

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class UsersController : ControllerBase
{
    private readonly IExpenseService _expenseService;

    public UsersController(IExpenseService expenseService)
    {
        _expenseService = expenseService;
    }

    /// <summary>
    /// Get all users
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<User>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<User>>> GetAll()
    {
        var users = await _expenseService.GetUsersAsync();
        return Ok(users);
    }
}

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class StatusesController : ControllerBase
{
    private readonly IExpenseService _expenseService;

    public StatusesController(IExpenseService expenseService)
    {
        _expenseService = expenseService;
    }

    /// <summary>
    /// Get all expense statuses
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<Status>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<Status>>> GetAll()
    {
        var statuses = await _expenseService.GetStatusesAsync();
        return Ok(statuses);
    }
}

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class ChatController : ControllerBase
{
    private readonly IChatService _chatService;
    private readonly ILogger<ChatController> _logger;

    public ChatController(IChatService chatService, ILogger<ChatController> logger)
    {
        _chatService = chatService;
        _logger = logger;
    }

    /// <summary>
    /// Check if chat is configured
    /// </summary>
    [HttpGet("status")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public ActionResult GetStatus()
    {
        return Ok(new { isConfigured = _chatService.IsConfigured });
    }

    /// <summary>
    /// Send a message to the AI assistant
    /// </summary>
    [HttpPost("message")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<ActionResult> SendMessage([FromBody] ChatRequest request)
    {
        var response = await _chatService.GetChatResponseAsync(request.Message, request.History ?? new List<ChatMessageInfo>());
        return Ok(new { response });
    }
}

public class ChatRequest
{
    public string Message { get; set; } = string.Empty;
    public List<ChatMessageInfo>? History { get; set; }
}
