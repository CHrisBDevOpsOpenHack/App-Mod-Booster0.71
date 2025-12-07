using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ExpenseManagement.Services;
using System.Text.Json;

namespace ExpenseManagement.Pages;

public class ChatModel : PageModel
{
    private readonly ChatService _chatService;
    private readonly ILogger<ChatModel> _logger;

    public ChatModel(ChatService chatService, ILogger<ChatModel> logger)
    {
        _chatService = chatService;
        _logger = logger;
    }

    public bool IsConfigured => _chatService.IsConfigured;

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostSendMessageAsync([FromBody] ChatMessageRequest request)
    {
        try
        {
            var response = await _chatService.SendMessageAsync(request.Message);
            return new JsonResult(new { response });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing chat message");
            return new JsonResult(new { response = $"Error: {ex.Message}" });
        }
    }
}

public class ChatMessageRequest
{
    public string Message { get; set; } = string.Empty;
}
