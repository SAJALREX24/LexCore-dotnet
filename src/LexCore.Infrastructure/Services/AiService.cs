using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using LexCore.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace LexCore.Infrastructure.Services;

public class AiService : IAiService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _model;
    private readonly ILogger<AiService> _logger;

    public AiService(IConfiguration configuration, ILogger<AiService> logger)
    {
        _apiKey = configuration["Anthropic:ApiKey"] ?? throw new Exception("Anthropic API key not configured");
        _model = configuration["Anthropic:Model"] ?? "claude-sonnet-4-6";
        _logger = logger;
        _httpClient = new HttpClient();
        _httpClient.BaseAddress = new Uri("https://api.anthropic.com");
        _httpClient.DefaultRequestHeaders.Add("x-api-key", _apiKey);
        _httpClient.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
    }

    public async Task<string> GenerateClientUpdateAsync(string hearingNote, string caseTitle, string clientName)
    {
        var prompt = $"""
            You are a professional legal assistant in India. 
            A lawyer has added the following hearing note for their case:
            
            Case: {caseTitle}
            Client: {clientName}
            Lawyer's Note: {hearingNote}
            
            Write a brief, professional client update message in simple English (not legal jargon) 
            that the lawyer can send to their client. The message should:
            - Be polite and reassuring
            - Explain what happened at the hearing in simple terms
            - Mention the next steps if any
            - Be maximum 3-4 sentences
            - Not start with "Dear" — just get straight to the update
            
            Return only the message text, nothing else.
            """;

        return await CallClaudeAsync(prompt);
    }

    public async Task<string> AnalyzeDocumentAsync(string documentText)
    {
        var prompt = $"""
            You are a legal assistant specialized in Indian law.
            Analyze the following legal document and extract structured information.
            
            Document:
            {documentText}
            
            Extract and return a JSON object with these fields:
            - caseTitle: string (parties involved)
            - courtName: string
            - caseType: string (Criminal/Civil/Family/Property/Corporate/Labour/Consumer)
            - filedDate: string (if found, in YYYY-MM-DD format)
            - sections: string (IPC sections or relevant acts)
            - nextHearingDate: string (if found, in YYYY-MM-DD format)
            - summary: string (2-3 sentence summary)
            
            Return only valid JSON, nothing else.
            """;

        return await CallClaudeAsync(prompt);
    }

    public async Task<string> DraftDocumentAsync(string caseDetails, string documentType, string instructions)
    {
        var prompt = $"""
            You are a legal drafting assistant specialized in Indian law and court procedures.
            
            Case Details:
            {caseDetails}
            
            Document Type: {documentType}
            
            Additional Instructions: {instructions}
            
            Draft a professional {documentType} following Indian court format and conventions.
            Use proper legal language appropriate for Indian district courts.
            Include all standard sections required for this document type.
            
            Return only the drafted document text, nothing else.
            """;

        return await CallClaudeAsync(prompt);
    }

    private async Task<string> CallClaudeAsync(string prompt)
    {
        try
        {
            var requestBody = new
            {
                model = _model,
                max_tokens = 1024,
                messages = new[]
                {
                    new { role = "user", content = prompt }
                }
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("/v1/messages", content);
            var responseText = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Claude API error: {Response}", responseText);
                throw new Exception($"Claude API returned {response.StatusCode}");
            }

            var responseJson = JsonDocument.Parse(responseText);
            var text = responseJson.RootElement
                .GetProperty("content")[0]
                .GetProperty("text")
                .GetString();

            return text ?? string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling Claude API");
            throw;
        }
    }
}