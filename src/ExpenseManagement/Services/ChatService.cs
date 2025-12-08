using System.Text;
using System.Text.Json;
using Azure;
using Azure.AI.OpenAI;
using Azure.Core;
using Azure.Identity;
using ExpenseManagement.Models;

namespace ExpenseManagement.Services;

public class ChatService
{
    private readonly IConfiguration _configuration;
    private readonly ExpenseService _expenseService;
    private readonly ILogger<ChatService> _logger;
    private OpenAIClient? _client;

    public ChatService(IConfiguration configuration, ExpenseService expenseService, ILogger<ChatService> logger)
    {
        _configuration = configuration;
        _expenseService = expenseService;
        _logger = logger;
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_configuration["GenAISettings:OpenAIEndpoint"]);

    public async Task<ChatInteractionResult> GetResponseAsync(string userMessage)
    {
        if (!IsConfigured)
        {
            return new ChatInteractionResult
            {
                Response = "AI Chat is not available yet. To enable it, redeploy using the -DeployGenAI switch.",
                UsedFallback = true
            };
        }

        try
        {
            var client = GetClient();
            var options = BuildChatOptions(userMessage);

            var response = await client.GetChatCompletionsAsync(options);
            var message = response.Value.Choices.FirstOrDefault()?.Message;
            if (message is null)
            {
                return new ChatInteractionResult { Response = "I wasn't able to generate a response right now. Please try again." };
            }

            if (message.ToolCalls?.Count > 0)
            {
                var toolResponses = new List<string>();
                foreach (var toolCall in message.ToolCalls.OfType<ChatCompletionsFunctionToolCall>())
                {
                    var toolResult = await ExecuteFunctionAsync(toolCall);
                    toolResponses.Add(toolResult);
                }

                return new ChatInteractionResult { Response = string.Join(Environment.NewLine, toolResponses) };
            }

            return new ChatInteractionResult { Response = message.Content ?? "Operation completed." };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Chat service failed");
            return new ChatInteractionResult
            {
                Response = "Something went wrong while talking to Azure OpenAI.",
                UsedFallback = true,
                Error = new ErrorInfo { Message = ex.Message }
            };
        }
    }

    private ChatCompletionsOptions BuildChatOptions(string userMessage)
    {
        var deployment = _configuration["GenAISettings:OpenAIModelName"] ?? "gpt-4o";
        var systemPrompt = """
You are an assistant for an expense management application. Use the available functions to read or update expenses instead of inventing data. Always confirm actions before creating or updating expenses. Keep responses concise and formatted for end users.
""";

        var options = new ChatCompletionsOptions
        {
            DeploymentName = deployment,
            Temperature = 0.2f
        };

        options.Messages.Add(new ChatRequestSystemMessage(systemPrompt));
        options.Messages.Add(new ChatRequestUserMessage(userMessage));

        options.Tools.Add(CreateToolDefinition(
            "get_expenses",
            "Retrieve expenses with optional status or search filter",
            new
            {
                type = "object",
                properties = new
                {
                    status = new { type = "string", description = "Filter by status name e.g. Submitted or Approved" },
                    searchTerm = new { type = "string", description = "Optional text to match description or category" }
                }
            }
        ));

        options.Tools.Add(CreateToolDefinition(
            "create_expense",
            "Create a new expense entry",
            new
            {
                type = "object",
                properties = new
                {
                    userId = new { type = "integer", description = "User ID submitting the expense" },
                    categoryId = new { type = "integer", description = "Expense category id" },
                    amount = new { type = "number", description = "Expense amount in pounds" },
                    description = new { type = "string", description = "Description of the expense" },
                    expenseDate = new { type = "string", description = "ISO date of the expense" }
                },
                required = new[] { "userId", "categoryId", "amount", "description", "expenseDate" }
            }
        ));

        options.Tools.Add(CreateToolDefinition(
            "update_expense_status",
            "Approve or reject an expense",
            new
            {
                type = "object",
                properties = new
                {
                    expenseId = new { type = "integer", description = "Expense identifier" },
                    statusName = new { type = "string", description = "New status value e.g. Approved or Rejected" },
                    reviewerId = new { type = "integer", description = "Reviewer user id", @nullable = true }
                },
                required = new[] { "expenseId", "statusName" }
            }
        ));

        return options;
    }

    private ChatCompletionsFunctionToolDefinition CreateToolDefinition(string name, string description, object schema)
    {
        return new ChatCompletionsFunctionToolDefinition
        {
            Name = name,
            Description = description,
            Parameters = BinaryData.FromObjectAsJson(schema, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            })
        };
    }

    private async Task<string> ExecuteFunctionAsync(ChatCompletionsFunctionToolCall toolCall)
    {
        var arguments = JsonDocument.Parse(toolCall.Arguments).RootElement;
        switch (toolCall.Name)
        {
            case "get_expenses":
                {
                    var status = arguments.TryGetProperty("status", out var statusProp) ? statusProp.GetString() : null;
                    var search = arguments.TryGetProperty("searchTerm", out var searchProp) ? searchProp.GetString() : null;
                    var expenses = await _expenseService.GetExpensesAsync(status, null, search);
                    var builder = new StringBuilder();
                    foreach (var expense in expenses.Data ?? Array.Empty<ExpenseRecord>())
                    {
                        builder.AppendLine($"#{expense.ExpenseId} - {expense.CategoryName} - £{expense.Amount} ({expense.StatusName})");
                    }
                    return builder.Length > 0 ? builder.ToString() : "No expenses found.";
                }
            case "create_expense":
                {
                    var request = new CreateExpenseRequest
                    {
                        UserId = arguments.GetProperty("userId").GetInt32(),
                        CategoryId = arguments.GetProperty("categoryId").GetInt32(),
                        Amount = arguments.GetProperty("amount").GetDecimal(),
                        Description = arguments.GetProperty("description").GetString() ?? string.Empty,
                        ExpenseDate = arguments.GetProperty("expenseDate").GetDateTime()
                    };
                    var result = await _expenseService.CreateExpenseAsync(request);
                    return result.Success ? $"Created expense #{result.Data}" : $"Failed to create expense: {result.Error?.Message}";
                }
            case "update_expense_status":
                {
                    var expenseId = arguments.GetProperty("expenseId").GetInt32();
                    var status = arguments.GetProperty("statusName").GetString() ?? "Submitted";
                    int? reviewerId = null;
                    if (arguments.TryGetProperty("reviewerId", out var reviewerProp) && reviewerProp.ValueKind == JsonValueKind.Number)
                    {
                        reviewerId = reviewerProp.GetInt32();
                    }
                    var result = await _expenseService.UpdateExpenseStatusAsync(expenseId, new UpdateExpenseStatusRequest
                    {
                        StatusName = status,
                        ReviewerId = reviewerId
                    });
                    return result.Success ? $"Updated expense #{expenseId} to {status}." : $"Failed to update expense: {result.Error?.Message}";
                }
            default:
                return "Unsupported operation.";
        }
    }

    private OpenAIClient GetClient()
    {
        if (_client != null)
        {
            return _client;
        }

        var endpoint = _configuration["GenAISettings:OpenAIEndpoint"];
        var managedIdentityClientId = _configuration["ManagedIdentityClientId"] ?? _configuration["AZURE_CLIENT_ID"];
        TokenCredential credential = string.IsNullOrWhiteSpace(managedIdentityClientId)
            ? new DefaultAzureCredential()
            : new ManagedIdentityCredential(managedIdentityClientId);

        _client = new OpenAIClient(new Uri(endpoint!), credential);
        return _client;
    }
}
