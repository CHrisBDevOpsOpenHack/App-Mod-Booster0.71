using Microsoft.AspNetCore.Mvc.RazorPages;
using ExpenseManagement.Services;

namespace ExpenseManagement.Pages;

public class ChatModel : PageModel
{
    private readonly ILogger<ChatModel> _logger;
    private readonly IConfiguration _configuration;

    public bool IsConfigured { get; private set; }

    public ChatModel(ILogger<ChatModel> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    public void OnGet()
    {
        var openAIEndpoint = _configuration["GenAISettings:OpenAIEndpoint"];
        var openAIModelName = _configuration["GenAISettings:OpenAIModelName"];
        
        IsConfigured = !string.IsNullOrEmpty(openAIEndpoint) && !string.IsNullOrEmpty(openAIModelName);
        
        _logger.LogInformation("Chat page loaded. GenAI configured: {IsConfigured}", IsConfigured);
    }
}
