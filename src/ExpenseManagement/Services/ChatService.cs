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
            var endpoint = _configuration["GenAISettings:OpenAIEndpoint"];
            var modelName = _configuration["GenAISettings:OpenAIModelName"];
            
            if (string.IsNullOrEmpty(endpoint) || string.IsNullOrEmpty(modelName))
            {
                return "AI Chat configuration is incomplete.";
            }

            // Create credential - prefer ManagedIdentityCredential with explicit client ID
            TokenCredential credential;
            var managedIdentityClientId = _configuration["ManagedIdentityClientId"];
            
            if (!string.IsNullOrEmpty(managedIdentityClientId))
            {
                _logger.LogInformation("Using ManagedIdentityCredential with client ID: {ClientId}", managedIdentityClientId);
                credential = new Azure.Identity.ManagedIdentityCredential(managedIdentityClientId);
            }
            else
            {
                _logger.LogInformation("Using DefaultAzureCredential");
                credential = new DefaultAzureCredential();
            }

            var client = new OpenAIClient(new Uri(endpoint), credential);

            // Define available functions
            var tools = new List<ChatTool>
            {
                ChatTool.CreateFunctionTool(
                    name: "get_all_expenses",
                    description: "Retrieves all expenses from the database with user and category details"
                ),
                ChatTool.CreateFunctionTool(
                    name: "get_expenses_by_status",
                    description: "Retrieves expenses filtered by status (Draft, Submitted, Approved, or Rejected)",
                    parameters: BinaryData.FromString(@"{
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
                ),
                ChatTool.CreateFunctionTool(
                    name: "get_expense_categories",
                    description: "Retrieves all available expense categories"
                ),
                ChatTool.CreateFunctionTool(
                    name: "create_expense",
                    description: "Creates a new expense. Amount should be in minor units (e.g., £12.50 = 1250 pence)",
                    parameters: BinaryData.FromString(@"{
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
                            ""amountMinor"": {
                                ""type"": ""integer"",
                                ""description"": ""Amount in minor units (pence). For £12.50, use 1250""
                            },
                            ""expenseDate"": {
                                ""type"": ""string"",
                                ""description"": ""Date of the expense in YYYY-MM-DD format""
                            },
                            ""description"": {
                                ""type"": ""string"",
                                ""description"": ""Description of the expense""
                            },
                            ""status"": {
                                ""type"": ""string"",
                                ""enum"": [""Draft"", ""Submitted""],
                                ""description"": ""Initial status (Draft or Submitted)""
                            }
                        },
                        ""required"": [""userId"", ""categoryId"", ""amountMinor"", ""expenseDate"", ""description""]
                    }")
                ),
                ChatTool.CreateFunctionTool(
                    name: "update_expense_status",
                    description: "Updates the status of an expense (e.g., approve or reject)",
                    parameters: BinaryData.FromString(@"{
                        ""type"": ""object"",
                        ""properties"": {
                            ""expenseId"": {
                                ""type"": ""integer"",
                                ""description"": ""The ID of the expense to update""
                            },
                            ""status"": {
                                ""type"": ""string"",
                                ""enum"": [""Submitted"", ""Approved"", ""Rejected""],
                                ""description"": ""The new status""
                            },
                            ""reviewedBy"": {
                                ""type"": ""integer"",
                                ""description"": ""The ID of the user reviewing (usually 2 for Bob Manager)""
                            }
                        },
                        ""required"": [""expenseId"", ""status""]
                    }")
                )
            };

            var chatOptions = new ChatCompletionsOptions
            {
                DeploymentName = modelName,
                Messages = {
                    new ChatRequestSystemMessage(GetSystemPrompt()),
                    new ChatRequestUserMessage(userMessage)
                },
                Temperature = 0.7f,
                MaxTokens = 800
            };

            foreach (var tool in tools)
            {
                chatOptions.Tools.Add(tool);
            }

            // Main conversation loop for function calling
            var response = await client.GetChatCompletionsAsync(chatOptions);
            var responseMessage = response.Value.Choices[0].Message;

            // Handle function calls
            while (responseMessage.ToolCalls?.Count > 0)
            {
                chatOptions.Messages.Add(new ChatRequestAssistantMessage(responseMessage));

                foreach (var toolCall in responseMessage.ToolCalls)
                {
                    if (toolCall is ChatCompletionsFunctionToolCall functionCall)
                    {
                        _logger.LogInformation("Function call: {FunctionName}", functionCall.Name);
                        
                        var functionResult = await ExecuteFunctionAsync(functionCall.Name, functionCall.Arguments);
                        
                        chatOptions.Messages.Add(new ChatRequestToolMessage(
                            content: functionResult,
                            toolCallId: functionCall.Id
                        ));
                    }
                }

                // Get next response
                response = await client.GetChatCompletionsAsync(chatOptions);
                responseMessage = response.Value.Choices[0].Message;
            }

            return responseMessage.Content ?? "I couldn't generate a response.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in chat service");
            return $"Error: {ex.Message}";
        }
    }

    private async Task<string> ExecuteFunctionAsync(string functionName, string argumentsJson)
    {
        try
        {
            _logger.LogInformation("Executing function: {FunctionName} with args: {Args}", functionName, argumentsJson);

            switch (functionName)
            {
                case "get_all_expenses":
                    var allExpenses = await _expenseService.GetAllExpensesAsync();
                    return JsonSerializer.Serialize(allExpenses);

                case "get_expenses_by_status":
                    var statusArgs = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(argumentsJson);
                    if (statusArgs != null && statusArgs.TryGetValue("status", out var statusValue))
                    {
                        var expenses = await _expenseService.GetExpensesByStatusAsync(statusValue.GetString() ?? "");
                        return JsonSerializer.Serialize(expenses);
                    }
                    return JsonSerializer.Serialize(new { error = "Status parameter required" });

                case "get_expense_categories":
                    var categories = await _expenseService.GetAllCategoriesAsync();
                    return JsonSerializer.Serialize(categories);

                case "create_expense":
                    var createArgs = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(argumentsJson);
                    if (createArgs != null)
                    {
                        var request = new CreateExpenseRequest
                        {
                            UserId = createArgs["userId"].GetInt32(),
                            CategoryId = createArgs["categoryId"].GetInt32(),
                            Amount = createArgs["amountMinor"].GetInt32() / 100.0m,
                            ExpenseDate = createArgs["expenseDate"].GetString() ?? DateTime.Today.ToString("yyyy-MM-dd"),
                            Description = createArgs["description"].GetString() ?? "",
                            Currency = "GBP"
                        };

                        var expenseId = await _expenseService.CreateExpenseAsync(request);
                        
                        // Submit if requested
                        if (createArgs.ContainsKey("status") && createArgs["status"].GetString() == "Submitted")
                        {
                            await _expenseService.SubmitExpenseAsync(expenseId);
                        }
                        
                        var expense = await _expenseService.GetExpenseByIdAsync(expenseId);
                        return JsonSerializer.Serialize(expense);
                    }
                    return JsonSerializer.Serialize(new { error = "Invalid arguments" });

                case "update_expense_status":
                    var updateArgs = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(argumentsJson);
                    if (updateArgs != null)
                    {
                        var expenseId = updateArgs["expenseId"].GetInt32();
                        var newStatus = updateArgs["status"].GetString() ?? "";
                        var reviewedBy = updateArgs.ContainsKey("reviewedBy") ? updateArgs["reviewedBy"].GetInt32() : 2; // Default to Bob

                        if (newStatus == "Approved")
                        {
                            await _expenseService.ApproveExpenseAsync(expenseId, reviewedBy);
                        }
                        else if (newStatus == "Rejected")
                        {
                            await _expenseService.RejectExpenseAsync(expenseId, reviewedBy);
                        }
                        else if (newStatus == "Submitted")
                        {
                            await _expenseService.SubmitExpenseAsync(expenseId);
                        }

                        var updatedExpense = await _expenseService.GetExpenseByIdAsync(expenseId);
                        return JsonSerializer.Serialize(updatedExpense);
                    }
                    return JsonSerializer.Serialize(new { error = "Invalid arguments" });

                default:
                    return JsonSerializer.Serialize(new { error = $"Unknown function: {functionName}" });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing function: {FunctionName}", functionName);
            return JsonSerializer.Serialize(new { error = ex.Message });
        }
    }

    private string GetSystemPrompt()
    {
        return @"You are a helpful AI assistant for an expense management system. You can help users manage their expenses by retrieving information, creating expenses, and updating statuses.

Available users:
- Alice Example (User ID: 1) - Employee who reports to Bob
- Bob Manager (User ID: 2) - Manager who can approve/reject expenses

Available expense categories:
- Travel (Category ID: 1)
- Meals (Category ID: 2)
- Supplies (Category ID: 3)
- Accommodation (Category ID: 4)
- Other (Category ID: 5)

Expense statuses:
- Draft: Initial state, not yet submitted
- Submitted: Waiting for manager approval
- Approved: Approved by manager
- Rejected: Rejected by manager

Important notes:
- All amounts must be in MINOR UNITS (pence). For £12.50, use 1250
- When users say 'today' or 'yesterday', calculate the actual date
- Default to User ID 1 (Alice) for creating expenses unless specified
- Default to User ID 2 (Bob) for reviewing/approving expenses
- When showing expenses, format amounts as currency (divide by 100)
- Always confirm actions before executing (e.g., 'I will create an expense for £50...')

Be conversational, helpful, and proactive. When listing expenses, format them nicely with the amount, date, category, and status.";
    }
}
