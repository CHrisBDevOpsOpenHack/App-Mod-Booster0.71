using Microsoft.AspNetCore.Mvc;
using ExpenseManagement.Models;
using ExpenseManagement.Services;

namespace ExpenseManagement.Controllers;

[ApiController]
[Route("api/[controller]")]
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
    [ProducesResponseType(typeof(IEnumerable<Expense>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<Expense>>> GetAll()
    {
        var expenses = await _expenseService.GetAllExpensesAsync();
        return Ok(expenses);
    }

    /// <summary>
    /// Get expenses by status
    /// </summary>
    [HttpGet("status/{statusName}")]
    [ProducesResponseType(typeof(IEnumerable<Expense>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<Expense>>> GetByStatus(string statusName)
    {
        var expenses = await _expenseService.GetExpensesByStatusAsync(statusName);
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
    /// Create a new expense
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(Expense), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<Expense>> Create(CreateExpenseRequest request)
    {
        try
        {
            var expense = await _expenseService.CreateExpenseAsync(request);
            return CreatedAtAction(nameof(GetById), new { id = expense.ExpenseId }, expense);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating expense");
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Update an existing expense
    /// </summary>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(Expense), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Expense>> Update(int id, UpdateExpenseRequest request)
    {
        if (id != request.ExpenseId)
            return BadRequest("Expense ID mismatch");

        try
        {
            var expense = await _expenseService.UpdateExpenseAsync(request);
            return Ok(expense);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating expense");
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Submit an expense for approval
    /// </summary>
    [HttpPost("{id}/submit")]
    [ProducesResponseType(typeof(Expense), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<Expense>> Submit(int id)
    {
        try
        {
            var expense = await _expenseService.SubmitExpenseAsync(id);
            return Ok(expense);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting expense");
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Approve an expense
    /// </summary>
    [HttpPost("{id}/approve")]
    [ProducesResponseType(typeof(Expense), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<Expense>> Approve(int id, [FromBody] ApproveExpenseRequest request)
    {
        if (id != request.ExpenseId)
            return BadRequest("Expense ID mismatch");

        try
        {
            var expense = await _expenseService.ApproveExpenseAsync(request);
            return Ok(expense);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error approving expense");
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Reject an expense
    /// </summary>
    [HttpPost("{id}/reject")]
    [ProducesResponseType(typeof(Expense), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<Expense>> Reject(int id, [FromBody] ApproveExpenseRequest request)
    {
        if (id != request.ExpenseId)
            return BadRequest("Expense ID mismatch");

        try
        {
            var expense = await _expenseService.RejectExpenseAsync(request);
            return Ok(expense);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rejecting expense");
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Search expenses
    /// </summary>
    [HttpGet("search")]
    [ProducesResponseType(typeof(IEnumerable<Expense>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<Expense>>> Search([FromQuery] string q)
    {
        var expenses = await _expenseService.SearchExpensesAsync(q ?? "");
        return Ok(expenses);
    }

    /// <summary>
    /// Delete an expense
    /// </summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> Delete(int id)
    {
        try
        {
            await _expenseService.DeleteExpenseAsync(id);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting expense");
            return BadRequest(new { error = ex.Message });
        }
    }
}

[ApiController]
[Route("api/[controller]")]
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
    [ProducesResponseType(typeof(IEnumerable<ExpenseCategory>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<ExpenseCategory>>> GetAll()
    {
        var categories = await _expenseService.GetAllCategoriesAsync();
        return Ok(categories);
    }
}

[ApiController]
[Route("api/[controller]")]
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
    [ProducesResponseType(typeof(IEnumerable<User>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<User>>> GetAll()
    {
        var users = await _expenseService.GetAllUsersAsync();
        return Ok(users);
    }
}

[ApiController]
[Route("api/[controller]")]
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
    [ProducesResponseType(typeof(IEnumerable<ExpenseStatus>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<ExpenseStatus>>> GetAll()
    {
        var statuses = await _expenseService.GetAllStatusesAsync();
        return Ok(statuses);
    }
}
