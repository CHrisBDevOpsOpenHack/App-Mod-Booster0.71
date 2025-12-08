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

    [HttpGet]
    public async Task<IActionResult> GetExpenses([FromQuery] string? status = null, [FromQuery] string? search = null)
    {
        var result = await _expenseService.GetExpensesAsync(status, null, search);
        if (!result.Success && result.Error is not null)
        {
            Response.Headers.Append("X-App-Error", result.Error.Message);
        }
        return Ok(result.Data);
    }

    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary()
    {
        var result = await _expenseService.GetExpenseSummaryAsync();
        if (!result.Success && result.Error is not null)
        {
            Response.Headers.Append("X-App-Error", result.Error.Message);
        }
        return Ok(result.Data);
    }

    [HttpPost]
    public async Task<IActionResult> CreateExpense([FromBody] CreateExpenseRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }
        var result = await _expenseService.CreateExpenseAsync(request);
        if (!result.Success)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, result.Error);
        }
        return Ok(new { expenseId = result.Data });
    }

    [HttpPost("{expenseId:int}/status")]
    public async Task<IActionResult> UpdateStatus(int expenseId, [FromBody] UpdateExpenseStatusRequest request)
    {
        var result = await _expenseService.UpdateExpenseStatusAsync(expenseId, request);
        if (!result.Success)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, result.Error);
        }
        return Ok(new { success = true });
    }

    [HttpGet("categories")]
    public async Task<IActionResult> GetCategories()
    {
        var result = await _expenseService.GetCategoriesAsync();
        return Ok(result.Data);
    }

    [HttpGet("statuses")]
    public async Task<IActionResult> GetStatuses()
    {
        var result = await _expenseService.GetStatusesAsync();
        return Ok(result.Data);
    }
}
