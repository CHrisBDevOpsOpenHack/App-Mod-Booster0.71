namespace ExpenseManagement.Models;

public class ChatInteractionResult
{
    public string Response { get; set; } = string.Empty;
    public bool UsedFallback { get; set; }
    public ErrorInfo? Error { get; set; }
}
