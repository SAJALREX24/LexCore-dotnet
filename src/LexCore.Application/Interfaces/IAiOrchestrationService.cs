using LexCore.Domain.Entities;

namespace LexCore.Application.Interfaces;

public class AiTaskContext
{
    public string Task { get; set; } = string.Empty;
    public string UserMessage { get; set; } = string.Empty;
    // SystemOverride removed — system prompts are hardcoded constants only.
    // No user-supplied data must ever reach the system prompt.
    public List<AiMessage> History { get; set; } = new();
    public string? ConversationSummary { get; set; }
    public SmartExtract? SmartExtract { get; set; }
    public string Language { get; set; } = "hi";
}

public interface IAiOrchestrationService
{
    Task<(string content, int inputTokens, int outputTokens)> ChatAsync(AiTaskContext ctx);
    Task<(string content, int inputTokens, int outputTokens)> DraftAsync(AiTaskContext ctx);
    Task<(string content, int inputTokens, int outputTokens)> ResearchAsync(AiTaskContext ctx);
    Task<(string content, int inputTokens, int outputTokens)> CompressAsync(List<AiMessage> messages);
}
