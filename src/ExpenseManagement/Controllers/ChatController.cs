using Microsoft.AspNetCore.Mvc;
using ExpenseManagement.Services;
using OpenAI.Chat;
using System.Text.Json;

namespace ExpenseManagement.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ChatController : ControllerBase
{
    private readonly IChatService _chatService;
    private readonly ILogger<ChatController> _logger;

    public ChatController(IChatService chatService, ILogger<ChatController> logger)
    {
        _chatService = chatService;
        _logger = logger;
    }

    public class ChatRequest
    {
        public string Message { get; set; } = string.Empty;
        public List<ChatHistoryItem>? History { get; set; }
    }

    public class ChatHistoryItem
    {
        public string Role { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
    }

    public class ChatResponse
    {
        public string Response { get; set; } = string.Empty;
        public bool Success { get; set; }
        public string? Error { get; set; }
    }

    /// <summary>
    /// Send a message to the chat assistant
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ChatResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ChatResponse>> SendMessage([FromBody] ChatRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Message))
            {
                return BadRequest(new ChatResponse
                {
                    Success = false,
                    Error = "Message cannot be empty"
                });
            }

            // Convert history to ChatMessage list
            var conversationHistory = new List<ChatMessage>();
            if (request.History != null)
            {
                foreach (var item in request.History)
                {
                    if (item.Role.ToLower() == "user")
                    {
                        conversationHistory.Add(ChatMessage.CreateUserMessage(item.Content));
                    }
                    else if (item.Role.ToLower() == "assistant")
                    {
                        conversationHistory.Add(ChatMessage.CreateAssistantMessage(item.Content));
                    }
                }
            }

            var response = await _chatService.SendMessageAsync(request.Message, conversationHistory);

            return Ok(new ChatResponse
            {
                Response = response,
                Success = true
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing chat message");
            return Ok(new ChatResponse
            {
                Success = false,
                Error = ex.Message,
                Response = "I encountered an error processing your request. Please try again."
            });
        }
    }
}
