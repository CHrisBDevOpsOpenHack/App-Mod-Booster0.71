using ExpenseManagement.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ExpenseManagement.Pages;

public class ChatModel : PageModel
{
    private readonly ChatService _chatService;

    public bool IsConfigured { get; private set; }

    public ChatModel(ChatService chatService)
    {
        _chatService = chatService;
    }

    public void OnGet()
    {
        IsConfigured = _chatService.IsConfigured;
    }
}
