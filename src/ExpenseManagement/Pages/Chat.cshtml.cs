using System.Text.Json;
using ExpenseManagement.Models;
using ExpenseManagement.Services;
using Microsoft.AspNetCore.Mvc;

namespace ExpenseManagement.Pages;

public class ChatModel : PageModelBase
{
    private readonly ChatService _chatService;
    private readonly ILogger<ChatModel> _logger;

    public ChatModel(ChatService chatService, ILogger<ChatModel> logger)
    {
        _chatService = chatService;
        _logger = logger;
    }

    [BindProperty]
    public string? UserMessage { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? ConversationJson { get; set; }

    public bool ChatAvailable => _chatService.IsConfigured;

    public List<ChatViewMessage> Conversation { get; private set; } = new();

    public void OnGet()
    {
        Conversation = ParseConversation();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        Conversation = ParseConversation();
        if (!string.IsNullOrWhiteSpace(UserMessage))
        {
            Conversation.Add(new ChatViewMessage("You", UserMessage));
            var response = await _chatService.GetResponseAsync(UserMessage);
            Conversation.Add(new ChatViewMessage("AI", response.Response));
            if (response.Error != null)
            {
                CaptureError(response.Error);
            }
        }

        ConversationJson = JsonSerializer.Serialize(Conversation);
        return Page();
    }

    private List<ChatViewMessage> ParseConversation()
    {
        if (!string.IsNullOrWhiteSpace(ConversationJson))
        {
            try
            {
                return JsonSerializer.Deserialize<List<ChatViewMessage>>(ConversationJson) ?? new List<ChatViewMessage>();
            }
            catch (JsonException)
            {
                return new List<ChatViewMessage>();
            }
        }
        return new List<ChatViewMessage>();
    }
}

public record ChatViewMessage(string Role, string Content);
