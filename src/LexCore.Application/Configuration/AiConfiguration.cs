namespace LexCore.Application.Configuration;

public class AiConfiguration
{
    public string ApiKey { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://api.anthropic.com";
    public string AnthropicVersion { get; set; } = "2023-06-01";
    public string DraftModel { get; set; } = "claude-opus-4-6";
    public string ResearchModel { get; set; } = "claude-sonnet-4-6";
    public string ChatModel { get; set; } = "claude-haiku-4-5-20251001";
    public int MaxTokensDraft { get; set; } = 2000;
    public int MaxTokensChat { get; set; } = 600;
    public int MaxTokensResearch { get; set; } = 1200;
    public bool CacheEnabled { get; set; } = true;
    public int CacheTtlHours { get; set; } = 168;
    public int MaxConversationMessages { get; set; } = 10;
    public int TimeoutSeconds { get; set; } = 30;
}
