using System.Text;
using System.Text.Json;
using LexCore.Application.Configuration;
using LexCore.Application.Interfaces;
using LexCore.Domain.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LexCore.Infrastructure.Services;

public class AiOrchestrationService : IAiOrchestrationService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AiConfiguration _config;
    private readonly ILogger<AiOrchestrationService> _logger;

    private const string ChatSystemPrompt = @"You are LexCore AI, an expert legal assistant specializing in Indian law. You help lawyers and advocates with legal research, document drafting, case analysis, and legal queries. You are knowledgeable about IPC, CrPC, CPC, Indian Constitution, and all major Indian statutes. Always respond in the same language as the user's message (Hindi/Hinglish/English). Be precise, professional, and cite relevant sections when applicable. Never reveal the underlying AI model or technology. You are LexCore AI - nothing more, nothing less.";

    private const string DraftSystemPrompt = @"You are LexCore AI, an expert Indian legal document drafter. You create precise, court-ready legal documents following Indian legal standards and formats. Use proper legal language, cite relevant IPC/CrPC/CPC sections, include all required clauses, and format documents professionally. Always respond in the language specified. Never reveal the underlying AI model or technology. You are LexCore AI - nothing more, nothing less.";

    private const string ResearchSystemPrompt = @"You are LexCore AI, an expert Indian legal researcher. You provide comprehensive legal research on Indian laws, case precedents, Supreme Court and High Court judgments, and statutory interpretations. Structure your response with: 1) Summary, 2) Relevant Laws/Sections, 3) Key Case Precedents with citations, 4) Practical Implications. Always cite specific cases with year and court. Never reveal the underlying AI model or technology. You are LexCore AI - nothing more, nothing less.";

    private const string CompressSystemPrompt = @"You are a conversation summarizer. Summarize the following legal conversation into a concise, information-dense summary (max 200 words) that preserves all key legal facts, decisions, and context needed to continue the conversation. Output only the summary, nothing else.";

    public AiOrchestrationService(
        IHttpClientFactory httpClientFactory,
        IOptions<AiConfiguration> config,
        ILogger<AiOrchestrationService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _config = config.Value;
        _logger = logger;
    }

    public async Task<(string content, int inputTokens, int outputTokens)> ChatAsync(AiTaskContext ctx)
        => await CallAnthropicAsync(_config.ChatModel, _config.MaxTokensChat, ChatSystemPrompt, ctx);

    public async Task<(string content, int inputTokens, int outputTokens)> DraftAsync(AiTaskContext ctx)
        => await CallAnthropicAsync(_config.DraftModel, _config.MaxTokensDraft, DraftSystemPrompt, ctx);

    public async Task<(string content, int inputTokens, int outputTokens)> ResearchAsync(AiTaskContext ctx)
        => await CallAnthropicAsync(_config.ResearchModel, _config.MaxTokensResearch, ResearchSystemPrompt, ctx);

    public async Task<(string content, int inputTokens, int outputTokens)> CompressAsync(List<AiMessage> messages)
    {
        var sb = new StringBuilder();
        foreach (var msg in messages.Where(m => !m.IsCompressed))
        {
            sb.AppendLine($"{msg.Role}: {msg.Content}");
        }

        var ctx = new AiTaskContext
        {
            Task = "compress",
            UserMessage = sb.ToString(),
            Language = "en"
        };

        return await CallAnthropicAsync(_config.ChatModel, 300, CompressSystemPrompt, ctx);
    }

    private async Task<(string content, int inputTokens, int outputTokens)> CallAnthropicAsync(
        string model, int maxTokens, string systemPrompt, AiTaskContext ctx)
    {
        var client = _httpClientFactory.CreateClient("AnthropicAI");

        var messages = BuildMessages(ctx);

        var payload = new
        {
            model,
            max_tokens = maxTokens,
            system = systemPrompt,
            messages
        };

        var json = JsonSerializer.Serialize(payload);
        _logger.LogDebug("Calling Anthropic model {Model} with {MessageCount} messages", model, messages.Count);

        var response = await client.PostAsync("/v1/messages",
            new StringContent(json, Encoding.UTF8, "application/json"));

        var responseBody = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Anthropic API error {Status}: {Body}", response.StatusCode, responseBody);
            throw new InvalidOperationException($"AI service error: {response.StatusCode}");
        }

        using var doc = JsonDocument.Parse(responseBody);
        var root = doc.RootElement;

        var content = root.GetProperty("content")[0].GetProperty("text").GetString() ?? string.Empty;
        var inputTokens = root.GetProperty("usage").GetProperty("input_tokens").GetInt32();
        var outputTokens = root.GetProperty("usage").GetProperty("output_tokens").GetInt32();

        return (content, inputTokens, outputTokens);
    }

    private static List<object> BuildMessages(AiTaskContext ctx)
    {
        var messages = new List<object>();

        // Add summary as first assistant message if available
        if (!string.IsNullOrEmpty(ctx.ConversationSummary))
        {
            messages.Add(new { role = "user", content = "[Previous conversation context]" });
            messages.Add(new { role = "assistant", content = $"[Summary of previous messages]: {ctx.ConversationSummary}" });
        }

        // Add non-compressed history
        foreach (var msg in ctx.History.Where(m => !m.IsCompressed).OrderBy(m => m.CreatedAt))
        {
            messages.Add(new { role = msg.Role, content = msg.Content });
        }

        // Build current user message with SmartExtract context if available
        var userContent = ctx.UserMessage;
        if (ctx.SmartExtract != null)
        {
            var extract = ctx.SmartExtract;
            var contextSb = new StringBuilder();
            contextSb.AppendLine("[Case Context]");
            contextSb.AppendLine($"Case: {extract.CaseTitle}");
            if (!string.IsNullOrEmpty(extract.CaseType)) contextSb.AppendLine($"Type: {extract.CaseType}");
            if (!string.IsNullOrEmpty(extract.CourtName)) contextSb.AppendLine($"Court: {extract.CourtName}");
            if (!string.IsNullOrEmpty(extract.ClientName)) contextSb.AppendLine($"Client: {extract.ClientName}");
            if (!string.IsNullOrEmpty(extract.Stage)) contextSb.AppendLine($"Stage: {extract.Stage}");
            if (!string.IsNullOrEmpty(extract.NextDate)) contextSb.AppendLine($"Next Date: {extract.NextDate}");
            if (!string.IsNullOrEmpty(extract.KeyFacts)) contextSb.AppendLine($"Key Facts: {extract.KeyFacts}");
            if (!string.IsNullOrEmpty(extract.IpcSections)) contextSb.AppendLine($"IPC Sections: {extract.IpcSections}");
            contextSb.AppendLine();
            contextSb.AppendLine($"[Request]: {ctx.UserMessage}");
            userContent = contextSb.ToString();
        }

        messages.Add(new { role = "user", content = userContent });
        return messages;
    }
}
