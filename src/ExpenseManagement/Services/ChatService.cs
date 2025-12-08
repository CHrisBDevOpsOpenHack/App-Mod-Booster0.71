using Azure;
using Azure.AI.OpenAI;
using Azure.Identity;
using OpenAI.Chat;
using System.Text.Json;
using ExpenseManagement.Models;

namespace ExpenseManagement.Services;

public interface IChatService
{
    bool IsConfigured { get; }
    Task<string> GetChatResponseAsync(string userMessage, List<ChatMessageInfo> conversationHistory);
}

public class ChatMessageInfo
{
    public string Role { get; set; } = "user";
    public string Content { get; set; } = string.Empty;
}

public class ChatService : IChatService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<ChatService> _logger;
    private readonly IExpenseService _expenseService;
    private readonly string? _openAIEndpoint;
    private readonly string? _openAIModelName;

    public bool IsConfigured => !string.IsNullOrEmpty(_openAIEndpoint);

    public ChatService(IConfiguration configuration, ILogger<ChatService> logger, IExpenseService expenseService)
    {
        _configuration = configuration;
        _logger = logger;
        _expenseService = expenseService;
        _openAIEndpoint = _configuration["GenAISettings:OpenAIEndpoint"];
        _openAIModelName = _configuration["GenAISettings:OpenAIModelName"] ?? "gpt-4o";
    }

    public async Task<string> GetChatResponseAsync(string userMessage, List<ChatMessageInfo> conversationHistory)
    {
        if (!IsConfigured)
        {
            return "AI Chat is not available. To enable it, redeploy using the -DeployGenAI switch.";
        }

        try
        {
            var managedIdentityClientId = _configuration["ManagedIdentityClientId"];
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

            var client = new AzureOpenAIClient(new Uri(_openAIEndpoint!), credential);
            var chatClient = client.GetChatClient(_openAIModelName);

            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(GetSystemPrompt())
            };

            foreach (var msg in conversationHistory)
            {
                if (msg.Role == "user")
                    messages.Add(new UserChatMessage(msg.Content));
                else if (msg.Role == "assistant")
                    messages.Add(new AssistantChatMessage(msg.Content));
            }

            messages.Add(new UserChatMessage(userMessage));

            var tools = GetFunctionTools();
            var options = new ChatCompletionOptions();
            foreach (var tool in tools)
            {
                options.Tools.Add(tool);
            }

            var response = await chatClient.CompleteChatAsync(messages, options);
            var completion = response.Value;

            // Handle function calls
            while (completion.FinishReason == ChatFinishReason.ToolCalls)
            {
                var toolCalls = completion.ToolCalls;
                messages.Add(new AssistantChatMessage(toolCalls));

                foreach (var toolCall in toolCalls)
                {
                    var functionResult = await ExecuteFunctionAsync(toolCall.FunctionName, toolCall.FunctionArguments.ToString());
                    messages.Add(new ToolChatMessage(toolCall.Id, functionResult));
                }

                response = await chatClient.CompleteChatAsync(messages, options);
                completion = response.Value;
            }

            return completion.Content[0].Text;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting chat response");
            return $"Sorry, I encountered an error: {ex.Message}";
        }
    }

    private string GetSystemPrompt()
    {
        return @"You are a helpful assistant for the Expense Management System. You can help users:
- View expenses and expense summaries
- Create new expenses
- Submit expenses for approval
- Approve or reject expenses (as a manager)
- Get information about categories and users

When users ask about expenses, use the available functions to retrieve real data.
When creating expenses, ask for all required information: amount, category, date, and description.
Format currency amounts in GBP (£).
Be concise and helpful.";
    }

    private List<ChatTool> GetFunctionTools()
    {
        return new List<ChatTool>
        {
            ChatTool.CreateFunctionTool(
                "get_all_expenses",
                "Retrieves all expenses from the database",
                BinaryData.FromString("{\"type\":\"object\",\"properties\":{},\"required\":[]}")
            ),
            ChatTool.CreateFunctionTool(
                "get_expense_summary",
                "Gets a summary of expenses grouped by status, including counts and totals",
                BinaryData.FromString("{\"type\":\"object\",\"properties\":{},\"required\":[]}")
            ),
            ChatTool.CreateFunctionTool(
                "get_expenses_by_status",
                "Gets expenses filtered by status",
                BinaryData.FromString("{\"type\":\"object\",\"properties\":{\"status\":{\"type\":\"string\",\"description\":\"Status to filter by: Draft, Submitted, Approved, or Rejected\"}},\"required\":[\"status\"]}")
            ),
            ChatTool.CreateFunctionTool(
                "create_expense",
                "Creates a new expense record",
                BinaryData.FromString("{\"type\":\"object\",\"properties\":{\"userId\":{\"type\":\"integer\",\"description\":\"User ID of the expense owner\"},\"categoryId\":{\"type\":\"integer\",\"description\":\"Category ID (1=Travel, 2=Meals, 3=Supplies, 4=Accommodation, 5=Other)\"},\"amount\":{\"type\":\"number\",\"description\":\"Amount in GBP\"},\"expenseDate\":{\"type\":\"string\",\"description\":\"Date of expense in YYYY-MM-DD format\"},\"description\":{\"type\":\"string\",\"description\":\"Description of the expense\"}},\"required\":[\"userId\",\"categoryId\",\"amount\",\"expenseDate\",\"description\"]}")
            ),
            ChatTool.CreateFunctionTool(
                "submit_expense",
                "Submits an expense for approval",
                BinaryData.FromString("{\"type\":\"object\",\"properties\":{\"expenseId\":{\"type\":\"integer\",\"description\":\"ID of the expense to submit\"}},\"required\":[\"expenseId\"]}")
            ),
            ChatTool.CreateFunctionTool(
                "approve_expense",
                "Approves an expense (manager action)",
                BinaryData.FromString("{\"type\":\"object\",\"properties\":{\"expenseId\":{\"type\":\"integer\",\"description\":\"ID of the expense to approve\"},\"reviewerId\":{\"type\":\"integer\",\"description\":\"User ID of the manager approving\"}},\"required\":[\"expenseId\",\"reviewerId\"]}")
            ),
            ChatTool.CreateFunctionTool(
                "reject_expense",
                "Rejects an expense (manager action)",
                BinaryData.FromString("{\"type\":\"object\",\"properties\":{\"expenseId\":{\"type\":\"integer\",\"description\":\"ID of the expense to reject\"},\"reviewerId\":{\"type\":\"integer\",\"description\":\"User ID of the manager rejecting\"}},\"required\":[\"expenseId\",\"reviewerId\"]}")
            ),
            ChatTool.CreateFunctionTool(
                "get_categories",
                "Gets all expense categories",
                BinaryData.FromString("{\"type\":\"object\",\"properties\":{},\"required\":[]}")
            ),
            ChatTool.CreateFunctionTool(
                "get_users",
                "Gets all users in the system",
                BinaryData.FromString("{\"type\":\"object\",\"properties\":{},\"required\":[]}")
            )
        };
    }

    private async Task<string> ExecuteFunctionAsync(string functionName, string arguments)
    {
        try
        {
            var args = JsonDocument.Parse(arguments);

            switch (functionName)
            {
                case "get_all_expenses":
                    var expenses = await _expenseService.GetAllExpensesAsync();
                    return JsonSerializer.Serialize(expenses.Select(e => new
                    {
                        e.ExpenseId,
                        e.UserName,
                        e.CategoryName,
                        Amount = $"£{e.Amount:N2}",
                        e.StatusName,
                        Date = e.ExpenseDate.ToString("dd MMM yyyy"),
                        e.Description
                    }));

                case "get_expense_summary":
                    var summary = await _expenseService.GetExpenseSummaryAsync();
                    return JsonSerializer.Serialize(summary.Select(s => new
                    {
                        s.StatusName,
                        s.Count,
                        TotalAmount = $"£{s.TotalAmount:N2}"
                    }));

                case "get_expenses_by_status":
                    var status = args.RootElement.GetProperty("status").GetString()!;
                    var filtered = await _expenseService.GetExpensesByStatusAsync(status);
                    return JsonSerializer.Serialize(filtered.Select(e => new
                    {
                        e.ExpenseId,
                        e.UserName,
                        e.CategoryName,
                        Amount = $"£{e.Amount:N2}",
                        Date = e.ExpenseDate.ToString("dd MMM yyyy"),
                        e.Description
                    }));

                case "create_expense":
                    var createRequest = new ExpenseCreateRequest
                    {
                        UserId = args.RootElement.GetProperty("userId").GetInt32(),
                        CategoryId = args.RootElement.GetProperty("categoryId").GetInt32(),
                        Amount = args.RootElement.GetProperty("amount").GetDecimal(),
                        ExpenseDate = DateTime.Parse(args.RootElement.GetProperty("expenseDate").GetString()!),
                        Description = args.RootElement.GetProperty("description").GetString()
                    };
                    var newId = await _expenseService.CreateExpenseAsync(createRequest);
                    return newId > 0 
                        ? JsonSerializer.Serialize(new { success = true, expenseId = newId, message = $"Expense created with ID {newId}" })
                        : JsonSerializer.Serialize(new { success = false, message = "Failed to create expense" });

                case "submit_expense":
                    var submitId = args.RootElement.GetProperty("expenseId").GetInt32();
                    var submitted = await _expenseService.SubmitExpenseAsync(submitId);
                    return JsonSerializer.Serialize(new { success = submitted, message = submitted ? "Expense submitted for approval" : "Failed to submit expense" });

                case "approve_expense":
                    var approveId = args.RootElement.GetProperty("expenseId").GetInt32();
                    var reviewerId = args.RootElement.GetProperty("reviewerId").GetInt32();
                    var approved = await _expenseService.ApproveExpenseAsync(approveId, reviewerId);
                    return JsonSerializer.Serialize(new { success = approved, message = approved ? "Expense approved" : "Failed to approve expense" });

                case "reject_expense":
                    var rejectId = args.RootElement.GetProperty("expenseId").GetInt32();
                    var rejReviewerId = args.RootElement.GetProperty("reviewerId").GetInt32();
                    var rejected = await _expenseService.RejectExpenseAsync(rejectId, rejReviewerId);
                    return JsonSerializer.Serialize(new { success = rejected, message = rejected ? "Expense rejected" : "Failed to reject expense" });

                case "get_categories":
                    var categories = await _expenseService.GetCategoriesAsync();
                    return JsonSerializer.Serialize(categories.Select(c => new { c.CategoryId, c.CategoryName }));

                case "get_users":
                    var users = await _expenseService.GetUsersAsync();
                    return JsonSerializer.Serialize(users.Select(u => new { u.UserId, u.UserName, u.RoleName, u.Email }));

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
