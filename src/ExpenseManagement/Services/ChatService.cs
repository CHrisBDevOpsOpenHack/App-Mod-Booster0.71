using Azure;
using Azure.AI.OpenAI;
using Azure.Identity;
using ExpenseManagement.Models;
using System.Text.Json;
using OpenAI.Chat;

namespace ExpenseManagement.Services;

public class ChatService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<ChatService> _logger;
    private readonly ExpenseService _expenseService;
    private ChatClient? _chatClient;

    public ChatService(
        IConfiguration configuration,
        ILogger<ChatService> logger,
        ExpenseService expenseService)
    {
        _configuration = configuration;
        _logger = logger;
        _expenseService = expenseService;
    }

    private ChatClient GetChatClient()
    {
        if (_chatClient != null)
            return _chatClient;

        var openAIEndpoint = _configuration["GenAISettings:OpenAIEndpoint"];
        var openAIModelName = _configuration["GenAISettings:OpenAIModelName"];
        var managedIdentityClientId = _configuration["ManagedIdentityClientId"];

        if (string.IsNullOrEmpty(openAIEndpoint) || string.IsNullOrEmpty(openAIModelName))
        {
            throw new InvalidOperationException(
                "Azure OpenAI is not configured. Deploy with -DeployGenAI switch to enable chat functionality.");
        }

        _logger.LogInformation("Initializing Azure OpenAI client");
        _logger.LogInformation("Endpoint: {Endpoint}", openAIEndpoint);
        _logger.LogInformation("Model: {Model}", openAIModelName);

        Azure.Core.TokenCredential credential;
        
        if (!string.IsNullOrEmpty(managedIdentityClientId))
        {
            _logger.LogInformation("Using ManagedIdentityCredential with client ID: {ClientId}", managedIdentityClientId);
            credential = new ManagedIdentityCredential(managedIdentityClientId);
        }
        else
        {
            _logger.LogInformation("Using DefaultAzureCredential");
            credential = new DefaultAzureCredential();
        }

        var client = new AzureOpenAIClient(new Uri(openAIEndpoint), credential);
        _chatClient = client.GetChatClient(openAIModelName);

        return _chatClient;
    }

    public async Task<string> SendMessageAsync(string userMessage, List<ChatMessage> conversationHistory)
    {
        try
        {
            var client = GetChatClient();
            
            // Build messages list with system prompt
            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(GetSystemPrompt())
            };
            
            // Add conversation history
            messages.AddRange(conversationHistory);
            
            // Add current user message
            messages.Add(new UserChatMessage(userMessage));

            // Define function tools for the AI
            var tools = new List<ChatTool>
            {
                ChatTool.CreateFunctionTool(
                    functionName: "get_expenses",
                    functionDescription: "Retrieves expenses from the database. Can filter by status if provided.",
                    functionParameters: BinaryData.FromString("""
                    {
                        "type": "object",
                        "properties": {
                            "status": {
                                "type": "string",
                                "description": "Optional status to filter by (Draft, Submitted, Approved, Rejected)",
                                "enum": ["Draft", "Submitted", "Approved", "Rejected"]
                            }
                        }
                    }
                    """)
                ),
                ChatTool.CreateFunctionTool(
                    functionName: "get_expense_by_id",
                    functionDescription: "Retrieves a specific expense by its ID",
                    functionParameters: BinaryData.FromString("""
                    {
                        "type": "object",
                        "properties": {
                            "expenseId": {
                                "type": "integer",
                                "description": "The ID of the expense to retrieve"
                            }
                        },
                        "required": ["expenseId"]
                    }
                    """)
                ),
                ChatTool.CreateFunctionTool(
                    functionName: "create_expense",
                    functionDescription: "Creates a new expense record",
                    functionParameters: BinaryData.FromString("""
                    {
                        "type": "object",
                        "properties": {
                            "userId": {
                                "type": "integer",
                                "description": "The ID of the user creating the expense (default: 1)"
                            },
                            "categoryId": {
                                "type": "integer",
                                "description": "The category ID (1=Travel, 2=Meals, 3=Office Supplies, 4=Entertainment, 5=Other)"
                            },
                            "amount": {
                                "type": "number",
                                "description": "The expense amount in major currency units (e.g., 50.00 for £50)"
                            },
                            "currency": {
                                "type": "string",
                                "description": "Currency code (GBP, USD, EUR)",
                                "enum": ["GBP", "USD", "EUR"]
                            },
                            "expenseDate": {
                                "type": "string",
                                "description": "The date of the expense in YYYY-MM-DD format"
                            },
                            "description": {
                                "type": "string",
                                "description": "Description of the expense"
                            }
                        },
                        "required": ["categoryId", "amount", "currency", "expenseDate", "description"]
                    }
                    """)
                ),
                ChatTool.CreateFunctionTool(
                    functionName: "update_expense_status",
                    functionDescription: "Updates the status of an expense (approve or reject)",
                    functionParameters: BinaryData.FromString("""
                    {
                        "type": "object",
                        "properties": {
                            "expenseId": {
                                "type": "integer",
                                "description": "The ID of the expense to update"
                            },
                            "status": {
                                "type": "string",
                                "description": "The new status (Approved or Rejected)",
                                "enum": ["Approved", "Rejected", "Submitted"]
                            },
                            "reviewedBy": {
                                "type": "integer",
                                "description": "The ID of the user reviewing (default: 1)"
                            }
                        },
                        "required": ["expenseId", "status"]
                    }
                    """)
                )
            };

            var options = new ChatCompletionOptions();
            foreach (var tool in tools)
            {
                options.Tools.Add(tool);
            }

            // Initial API call
            var response = await client.CompleteChatAsync(messages, options);
            
            // Handle function calling loop
            while (response.Value.FinishReason == ChatFinishReason.ToolCalls)
            {
                // Add assistant's response with tool calls to history
                messages.Add(new AssistantChatMessage(response.Value));

                foreach (var toolCall in response.Value.ToolCalls)
                {
                    var functionToolCall = toolCall as ChatToolCall;
                    if (functionToolCall != null)
                    {
                        _logger.LogInformation("AI called function: {FunctionName} with arguments: {Arguments}", 
                            functionToolCall.FunctionName, 
                            functionToolCall.FunctionArguments);

                        var functionResult = await ExecuteFunctionAsync(
                            functionToolCall.FunctionName, 
                            functionToolCall.FunctionArguments.ToString());

                        messages.Add(new ToolChatMessage(functionToolCall.Id, functionResult));
                    }
                }

                // Get next response
                response = await client.CompleteChatAsync(messages, options);
            }

            var finalMessage = response.Value.Content[0].Text;
            return finalMessage;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending message to OpenAI");
            throw;
        }
    }

    private async Task<string> ExecuteFunctionAsync(string functionName, string argumentsJson)
    {
        try
        {
            _logger.LogInformation("Executing function: {FunctionName}", functionName);

            switch (functionName)
            {
                case "get_expenses":
                    {
                        try
                        {
                            var args = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(argumentsJson);
                            string? status = args != null && args.ContainsKey("status") 
                                ? args["status"].GetString() 
                                : null;

                            var expenses = string.IsNullOrEmpty(status)
                                ? await _expenseService.GetAllExpensesAsync()
                                : await _expenseService.GetExpensesByStatusAsync(status);

                            return JsonSerializer.Serialize(expenses);
                        }
                        catch (JsonException ex)
                        {
                            _logger.LogError(ex, "Error parsing JSON for get_expenses");
                            return JsonSerializer.Serialize(new { error = "Invalid JSON format" });
                        }
                    }

                case "get_expense_by_id":
                    {
                        try
                        {
                            var args = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(argumentsJson);
                            if (args == null || !args.ContainsKey("expenseId"))
                                return JsonSerializer.Serialize(new { error = "expenseId is required" });

                            var expenseId = args["expenseId"].GetInt32();
                            var expense = await _expenseService.GetExpenseByIdAsync(expenseId);

                            if (expense == null)
                                return JsonSerializer.Serialize(new { error = "Expense not found" });

                            return JsonSerializer.Serialize(expense);
                        }
                        catch (JsonException ex)
                        {
                            _logger.LogError(ex, "Error parsing JSON for get_expense_by_id");
                            return JsonSerializer.Serialize(new { error = "Invalid JSON format" });
                        }
                    }

                case "create_expense":
                    {
                        try
                        {
                            var args = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(argumentsJson);
                            if (args == null)
                                return JsonSerializer.Serialize(new { error = "Invalid arguments" });

                            var userId = args.ContainsKey("userId") ? args["userId"].GetInt32() : 1;
                            var categoryId = args["categoryId"].GetInt32();
                            var amount = args["amount"].GetDouble();
                            var amountMinor = (int)(amount * 100); // Convert to minor units
                            var currency = args["currency"].GetString() ?? "GBP";
                            var expenseDateStr = args["expenseDate"].GetString() ?? DateTime.Now.ToString("yyyy-MM-dd");
                            
                            // Use TryParseExact with specific format for reliable parsing
                            if (!DateTime.TryParseExact(expenseDateStr, "yyyy-MM-dd", 
                                System.Globalization.CultureInfo.InvariantCulture, 
                                System.Globalization.DateTimeStyles.None, 
                                out var expenseDate))
                            {
                                return JsonSerializer.Serialize(new { error = "Invalid date format. Use yyyy-MM-dd" });
                            }
                            
                            var description = args["description"].GetString() ?? "";

                            var expense = new Expense
                            {
                                UserId = userId,
                                CategoryId = categoryId,
                                StatusId = 1, // Draft
                                AmountMinor = amountMinor,
                                Currency = currency,
                                ExpenseDate = expenseDate,
                                Description = description
                            };

                            var createdExpense = await _expenseService.CreateExpenseAsync(expense);
                            return JsonSerializer.Serialize(createdExpense);
                        }
                        catch (JsonException ex)
                        {
                            _logger.LogError(ex, "Error parsing JSON for create_expense");
                            return JsonSerializer.Serialize(new { error = "Invalid JSON format" });
                        }
                    }

                case "update_expense_status":
                    {
                        try
                        {
                            var args = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(argumentsJson);
                            if (args == null)
                                return JsonSerializer.Serialize(new { error = "Invalid arguments" });

                            var expenseId = args["expenseId"].GetInt32();
                            var status = args["status"].GetString() ?? "Submitted";
                            var reviewedBy = args.ContainsKey("reviewedBy") ? args["reviewedBy"].GetInt32() : 1;

                            var expense = await _expenseService.UpdateExpenseStatusAsync(expenseId, status, reviewedBy);

                            if (expense == null)
                                return JsonSerializer.Serialize(new { error = "Failed to update expense" });

                            return JsonSerializer.Serialize(expense);
                        }
                        catch (JsonException ex)
                        {
                            _logger.LogError(ex, "Error parsing JSON for update_expense_status");
                            return JsonSerializer.Serialize(new { error = "Invalid JSON format" });
                        }
                    }

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

    private string GetSystemPrompt()
    {
        return @"You are a helpful AI assistant for an expense management system. You can help users:

1. View expenses - retrieve and display expense information
2. Create expenses - add new expense records
3. Update expenses - approve or reject expense submissions
4. Search expenses - find specific expenses

When displaying lists of expenses, format them clearly with:
- Expense ID
- User name
- Amount (formatted with currency symbol)
- Category
- Status
- Description
- Date

When creating expenses, ask for clarification if needed:
- Amount (specify currency)
- Category (Travel, Meals, Office Supplies, Entertainment, Other)
- Date (default to today if not specified)
- Description

For amounts, the currency should be GBP (£), USD ($), or EUR (€).

Categories are:
- 1 = Travel
- 2 = Meals  
- 3 = Office Supplies
- 4 = Entertainment
- 5 = Other

When approving or rejecting expenses, confirm the action was successful.

Be conversational, friendly, and helpful. If an operation fails, explain what went wrong in simple terms.";
    }

    public bool IsConfigured()
    {
        var openAIEndpoint = _configuration["GenAISettings:OpenAIEndpoint"];
        var openAIModelName = _configuration["GenAISettings:OpenAIModelName"];
        
        return !string.IsNullOrEmpty(openAIEndpoint) && !string.IsNullOrEmpty(openAIModelName);
    }
}
