using Azure;
using Azure.AI.OpenAI;
using Azure.Identity;
using ExpenseManagement.Services;
using System.Text.Json;
using System.Text;

namespace ExpenseManagement.Services;

public class ChatService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<ChatService> _logger;
    private readonly ExpenseService _expenseService;

    public ChatService(IConfiguration configuration, ILogger<ChatService> logger, ExpenseService expenseService)
    {
        _configuration = configuration;
        _logger = logger;
        _expenseService = expenseService;
    }

    public bool IsConfigured => !string.IsNullOrEmpty(_configuration["GenAISettings:OpenAIEndpoint"]);

    public async Task<string> SendMessageAsync(string userMessage, List<ChatMessage> conversationHistory)
    {
        if (!IsConfigured)
        {
            return "AI Chat is not configured. To enable it, redeploy the infrastructure with the -DeployGenAI switch.";
        }

        try
        {
            var endpoint = new Uri(_configuration["GenAISettings:OpenAIEndpoint"]!);
            var modelName = _configuration["GenAISettings:OpenAIModelName"]!;
            var managedIdentityClientId = _configuration["ManagedIdentityClientId"];

            Azure.Core.TokenCredential credential;
            if (!string.IsNullOrEmpty(managedIdentityClientId))
            {
                _logger.LogInformation("Using ManagedIdentityCredential with client ID: {ClientId}", managedIdentityClientId);
                credential = new ManagedIdentityCredential(managedIdentityClientId);
            }
            else
            {
                _logger.LogWarning("ManagedIdentityClientId not configured, falling back to DefaultAzureCredential. This may cause unpredictable behavior in production.");
                credential = new DefaultAzureCredential();
            }

            var client = new OpenAIClient(endpoint, credential);

            // Build messages list
            var messages = new List<ChatRequestMessage>
            {
                new ChatRequestSystemMessage(GetSystemPrompt())
            };

            // Add conversation history
            foreach (var msg in conversationHistory)
            {
                if (msg.Role == "user")
                    messages.Add(new ChatRequestUserMessage(msg.Content));
                else if (msg.Role == "assistant")
                    messages.Add(new ChatRequestAssistantMessage(msg.Content));
            }

            // Add current user message
            messages.Add(new ChatRequestUserMessage(userMessage));

            // Define function tools
            var chatCompletionsOptions = new ChatCompletionsOptions
            {
                DeploymentName = modelName,
                Messages = { },
                Temperature = 0.7f,
                MaxTokens = 800,
                Tools = { }
            };

            foreach (var msg in messages)
            {
                chatCompletionsOptions.Messages.Add(msg);
            }

            // Add function tools
            AddFunctionTools(chatCompletionsOptions);

            // Get response
            var response = await client.GetChatCompletionsAsync(chatCompletionsOptions);
            var choice = response.Value.Choices[0];

            // Handle function calls
            while (choice.FinishReason == CompletionsFinishReason.ToolCalls)
            {
                foreach (var toolCall in choice.Message.ToolCalls)
                {
                    if (toolCall is ChatCompletionsFunctionToolCall functionToolCall)
                    {
                        _logger.LogInformation("Function called: {FunctionName} with arguments: {Arguments}",
                            functionToolCall.Name, functionToolCall.Arguments);

                        var functionResult = await ExecuteFunctionAsync(functionToolCall.Name, functionToolCall.Arguments);

                        chatCompletionsOptions.Messages.Add(new ChatRequestAssistantMessage(choice.Message));
                        chatCompletionsOptions.Messages.Add(new ChatRequestToolMessage(functionResult, functionToolCall.Id));
                    }
                }

                response = await client.GetChatCompletionsAsync(chatCompletionsOptions);
                choice = response.Value.Choices[0];
            }

            return choice.Message.Content ?? "I'm sorry, I couldn't process that request.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in chat service");
            return $"Error: {ex.Message}";
        }
    }

    private string GetSystemPrompt()
    {
        return @"You are a helpful AI assistant for an Expense Management System. 
You can help users view, create, and manage their expenses.

Available functions:
- get_all_expenses: Retrieve all expenses
- get_expenses_by_status: Retrieve expenses by status (Draft, Submitted, Approved, Rejected)
- get_expenses_by_user: Retrieve expenses for a specific user
- create_expense: Create a new expense
- update_expense_status: Update the status of an expense (submit, approve, reject)
- get_categories: Get all expense categories
- get_users: Get all users
- get_expense_summary: Get summary of expenses by status

When displaying expense information, format it nicely with amounts in GBP (£).
Be friendly and helpful. Always confirm actions before performing them.";
    }

    private void AddFunctionTools(ChatCompletionsOptions options)
    {
        options.Tools.Add(new ChatCompletionsFunctionToolDefinition
        {
            Name = "get_all_expenses",
            Description = "Retrieves all expenses from the database",
            Parameters = BinaryData.FromString("{\"type\":\"object\",\"properties\":{}}")
        });

        options.Tools.Add(new ChatCompletionsFunctionToolDefinition
        {
            Name = "get_expenses_by_status",
            Description = "Retrieves expenses by status (Draft, Submitted, Approved, or Rejected)",
            Parameters = BinaryData.FromString(@"{
                ""type"": ""object"",
                ""properties"": {
                    ""status"": {
                        ""type"": ""string"",
                        ""enum"": [""Draft"", ""Submitted"", ""Approved"", ""Rejected""],
                        ""description"": ""The status to filter by""
                    }
                },
                ""required"": [""status""]
            }")
        });

        options.Tools.Add(new ChatCompletionsFunctionToolDefinition
        {
            Name = "get_expenses_by_user",
            Description = "Retrieves all expenses for a specific user",
            Parameters = BinaryData.FromString(@"{
                ""type"": ""object"",
                ""properties"": {
                    ""userId"": {
                        ""type"": ""integer"",
                        ""description"": ""The ID of the user""
                    }
                },
                ""required"": [""userId""]
            }")
        });

        options.Tools.Add(new ChatCompletionsFunctionToolDefinition
        {
            Name = "create_expense",
            Description = "Creates a new expense",
            Parameters = BinaryData.FromString(@"{
                ""type"": ""object"",
                ""properties"": {
                    ""userId"": {
                        ""type"": ""integer"",
                        ""description"": ""The ID of the user creating the expense""
                    },
                    ""categoryId"": {
                        ""type"": ""integer"",
                        ""description"": ""The category ID (1=Travel, 2=Meals, 3=Supplies, 4=Accommodation, 5=Other)""
                    },
                    ""amount"": {
                        ""type"": ""number"",
                        ""description"": ""The amount in GBP""
                    },
                    ""expenseDate"": {
                        ""type"": ""string"",
                        ""description"": ""The date of the expense in YYYY-MM-DD format""
                    },
                    ""description"": {
                        ""type"": ""string"",
                        ""description"": ""Description of the expense""
                    }
                },
                ""required"": [""userId"", ""categoryId"", ""amount"", ""expenseDate"", ""description""]
            }")
        });

        options.Tools.Add(new ChatCompletionsFunctionToolDefinition
        {
            Name = "update_expense_status",
            Description = "Updates the status of an expense",
            Parameters = BinaryData.FromString(@"{
                ""type"": ""object"",
                ""properties"": {
                    ""expenseId"": {
                        ""type"": ""integer"",
                        ""description"": ""The ID of the expense to update""
                    },
                    ""status"": {
                        ""type"": ""string"",
                        ""enum"": [""Draft"", ""Submitted"", ""Approved"", ""Rejected""],
                        ""description"": ""The new status""
                    },
                    ""reviewedBy"": {
                        ""type"": ""integer"",
                        ""description"": ""User ID of the reviewer (optional, for Approved/Rejected status)""
                    }
                },
                ""required"": [""expenseId"", ""status""]
            }")
        });

        options.Tools.Add(new ChatCompletionsFunctionToolDefinition
        {
            Name = "get_categories",
            Description = "Gets all expense categories",
            Parameters = BinaryData.FromString("{\"type\":\"object\",\"properties\":{}}")
        });

        options.Tools.Add(new ChatCompletionsFunctionToolDefinition
        {
            Name = "get_users",
            Description = "Gets all users in the system",
            Parameters = BinaryData.FromString("{\"type\":\"object\",\"properties\":{}}")
        });

        options.Tools.Add(new ChatCompletionsFunctionToolDefinition
        {
            Name = "get_expense_summary",
            Description = "Gets a summary of expenses grouped by status with counts and totals",
            Parameters = BinaryData.FromString("{\"type\":\"object\",\"properties\":{}}")
        });
    }

    private async Task<string> ExecuteFunctionAsync(string functionName, string arguments)
    {
        try
        {
            switch (functionName)
            {
                case "get_all_expenses":
                    var allExpenses = await _expenseService.GetAllExpensesAsync();
                    return JsonSerializer.Serialize(allExpenses);

                case "get_expenses_by_status":
                    var statusArgs = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(arguments);
                    var status = statusArgs!["status"].GetString();
                    var expensesByStatus = await _expenseService.GetExpensesByStatusAsync(status!);
                    return JsonSerializer.Serialize(expensesByStatus);

                case "get_expenses_by_user":
                    var userArgs = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(arguments);
                    var userId = userArgs!["userId"].GetInt32();
                    var expensesByUser = await _expenseService.GetExpensesByUserAsync(userId);
                    return JsonSerializer.Serialize(expensesByUser);

                case "create_expense":
                    var createArgs = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(arguments);
                    var createRequest = new Models.CreateExpenseRequest
                    {
                        UserId = createArgs!["userId"].GetInt32(),
                        CategoryId = createArgs["categoryId"].GetInt32(),
                        Amount = createArgs["amount"].GetDecimal(),
                        ExpenseDate = DateTime.Parse(createArgs["expenseDate"].GetString()!),
                        Description = createArgs["description"].GetString(),
                        Currency = "GBP"
                    };
                    var newExpenseId = await _expenseService.CreateExpenseAsync(createRequest);
                    return JsonSerializer.Serialize(new { expenseId = newExpenseId, success = true });

                case "update_expense_status":
                    var updateArgs = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(arguments);
                    var expenseId = updateArgs!["expenseId"].GetInt32();
                    var newStatus = updateArgs["status"].GetString();
                    int? reviewedBy = updateArgs.ContainsKey("reviewedBy") ? updateArgs["reviewedBy"].GetInt32() : null;
                    await _expenseService.UpdateExpenseStatusAsync(expenseId, newStatus!, reviewedBy);
                    return JsonSerializer.Serialize(new { success = true });

                case "get_categories":
                    var categories = await _expenseService.GetCategoriesAsync();
                    return JsonSerializer.Serialize(categories);

                case "get_users":
                    var users = await _expenseService.GetUsersAsync();
                    return JsonSerializer.Serialize(users);

                case "get_expense_summary":
                    var summary = await _expenseService.GetExpenseSummaryAsync();
                    return JsonSerializer.Serialize(summary);

                default:
                    return JsonSerializer.Serialize(new { error = $"Unknown function: {functionName}" });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing function {FunctionName}", functionName);
            return JsonSerializer.Serialize(new { error = ex.Message });
        }
    }
}

public class ChatMessage
{
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}
