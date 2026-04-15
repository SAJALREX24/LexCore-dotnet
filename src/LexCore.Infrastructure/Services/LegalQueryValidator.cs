using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using LexCore.Application.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LexCore.Infrastructure.Services;

public class LegalQueryValidator
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AiConfiguration _config;
    private readonly ILogger<LegalQueryValidator> _logger;
    private readonly ConcurrentDictionary<string, (bool result, DateTime expiresAt)> _cache = new();

    private static readonly HashSet<string> LegalKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "law", "legal", "court", "judge", "lawyer", "advocate", "case", "petition",
        "bail", "hearing", "verdict", "judgment", "appeal", "supreme court", "high court",
        "district court", "ipc", "crpc", "cpc", "evidence", "criminal", "civil", "family",
        "divorce", "custody", "property", "contract", "agreement", "deed", "affidavit",
        "fir", "complaint", "police", "arrest", "trial", "conviction", "acquittal",
        "section", "act", "statute", "constitution", "fundamental rights", "habeas corpus",
        "writ", "injunction", "stay", "order", "decree", "settlement", "arbitration",
        "mediation", "notary", "stamp duty", "registration", "landlord", "tenant",
        "rent", "eviction", "succession", "inheritance", "will", "testament",
        "trademark", "copyright", "patent", "gdpr", "privacy", "defamation",
        "cheque bounce", "dishonour", "negotiable instrument", "rti", "consumer",
        "labour", "employment", "tax", "gst", "income tax", "kanoon", "nyay",
        "adhikar", "vakeel", "muqadma", "adalat"
    };

    public LegalQueryValidator(IHttpClientFactory httpClientFactory, IOptions<AiConfiguration> config, ILogger<LegalQueryValidator> logger)
    {
        _httpClientFactory = httpClientFactory;
        _config = config.Value;
        _logger = logger;
    }

    public async Task<bool> IsLegalAsync(string message)
    {
        // Keyword check first
        foreach (var keyword in LegalKeywords)
        {
            if (message.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        // Check in-memory cache
        var hash = ComputeHash(message);
        if (_cache.TryGetValue(hash, out var cached) && cached.expiresAt > DateTime.UtcNow)
            return cached.result;

        // Ask model
        try
        {
            var client = _httpClientFactory.CreateClient("AnthropicAI");
            var payload = new
            {
                model = _config.ChatModel,
                max_tokens = 10,
                messages = new[]
                {
                    new { role = "user", content = $"Is this a legal question? Reply YES or NO only: {message}" }
                }
            };

            var json = System.Text.Json.JsonSerializer.Serialize(payload);
            var response = await client.PostAsync("/v1/messages",
                new StringContent(json, Encoding.UTF8, "application/json"));

            if (response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                var result = body.Contains("YES", StringComparison.OrdinalIgnoreCase);
                _cache[hash] = (result, DateTime.UtcNow.AddHours(24));
                return result;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "LegalQueryValidator AI call failed, defaulting to true");
        }

        return true; // Default to legal on failure
    }

    private static string ComputeHash(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input.ToLowerInvariant().Trim()));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
