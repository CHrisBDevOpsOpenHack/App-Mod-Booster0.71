using Azure;
using Azure.AI.OpenAI;
using Azure.Core;
using Azure.Identity;
using ExpenseManagement.Models;
using System.Text.Json;

namespace ExpenseManagement.Services;

public class ChatService
{
    private readonly IConfiguration _configuration;
    private readonly ExpenseService _expenseService;
    private readonly ILogger<ChatService> _logger;

    public ChatService(IConfiguration configuration, ExpenseService expenseService, ILogger<ChatService> logger)
    {
        _configuration = configuration;
        _expenseService = expenseService;
        _logger = logger;
    }

    public bool IsConfigured
    {
        get
        {
            var endpoint = _configuration["GenAISettings:OpenAIEndpoint"];
            return !string.IsNullOrEmpty(endpoint);
        }
    }

    public async Task<string> SendMessageAsync(string userMessage)
    {
        if (!IsConfigured)
        {
            return "AI Chat is not available. To enable it, redeploy the infrastructure with the -DeployGenAI switch.";
        }

        try
        {
            // TODO: Implement full Azure OpenAI integration with function calling
            // For now, provide a helpful response about the system
            
            await Task.Delay(500); // Simulate processing
            
            return $"I received your message: \"{userMessage}\"\n\nThe AI Chat feature requires the latest Azure.AI.OpenAI SDK and will be fully functional once deployed to Azure with GenAI resources. For now, please use the web interface to manage expenses.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in chat service");
            return $"Error: {ex.Message}";
        }
    }

    private string GetSystemPrompt()
    {
        return @"You are a helpful assistant for an expense management system. You can help users understand their expenses and answer questions about the system.

Sample users:
- Alice Example (User ID: 1) - Employee
- Bob Manager (User ID: 2) - Manager

Sample categories:
- Travel (ID: 1)
- Meals (ID: 2)
- Supplies (ID: 3)
- Accommodation (ID: 4)
- Other (ID: 5)

Be helpful and conversational.";
    }
}
