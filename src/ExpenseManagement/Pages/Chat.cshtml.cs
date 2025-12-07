using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ExpenseManagement.Pages;

public class ChatModel : PageModel
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<ChatModel> _logger;

    public bool GenAIEnabled { get; private set; }
    public string? OpenAIEndpoint { get; private set; }

    public ChatModel(IConfiguration configuration, ILogger<ChatModel> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public void OnGet()
    {
        OpenAIEndpoint = _configuration["GenAISettings:OpenAIEndpoint"];
        GenAIEnabled = !string.IsNullOrEmpty(OpenAIEndpoint);
        
        if (!GenAIEnabled)
        {
            _logger.LogWarning("GenAI features are not configured. Deploy with -DeployGenAI switch.");
        }
    }
}
