using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LexCore.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace LexCore.Infrastructure.Services;

public class RazorpayService : IRazorpayService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<RazorpayService> _logger;
    private readonly HttpClient _httpClient;
    private readonly bool _testMode;

    public RazorpayService(IConfiguration configuration, ILogger<RazorpayService> logger, IHttpClientFactory httpClientFactory)
    {
        _configuration = configuration;
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient("Razorpay");
        _testMode = configuration["Razorpay:TestMode"]?.ToLower() == "true";

        if (!_testMode)
        {
            var keyId = configuration["Razorpay:KeyId"];
            var keySecret = configuration["Razorpay:KeySecret"];
            var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{keyId}:{keySecret}"));
            _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", credentials);
            _httpClient.BaseAddress = new Uri("https://api.razorpay.com/v1/");
        }
    }

    public async Task<(string subscriptionId, string paymentLink)> CreateSubscriptionAsync(Guid firmId, string planId, string customerEmail, string customerName)
    {
        if (_testMode)
        {
            _logger.LogInformation("TEST MODE: Creating mock subscription for firm {FirmId}", firmId);
            var mockSubscriptionId = $"sub_test_{Guid.NewGuid():N}";
            var mockPaymentLink = $"https://rzp.io/test/{mockSubscriptionId}";
            return (mockSubscriptionId, mockPaymentLink);
        }

        var payload = new
        {
            plan_id = planId,
            total_count = 12,
            quantity = 1,
            customer_notify = 1,
            notes = new { firm_id = firmId.ToString() }
        };

        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync("subscriptions", content);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            _logger.LogError("Razorpay subscription creation failed: {Error}", error);
            throw new Exception($"Failed to create subscription: {error}");
        }

        var result = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(result);
        var subscriptionId = doc.RootElement.GetProperty("id").GetString()!;
        var shortUrl = doc.RootElement.GetProperty("short_url").GetString()!;

        return (subscriptionId, shortUrl);
    }

    public async Task<bool> CancelSubscriptionAsync(string subscriptionId)
    {
        if (_testMode)
        {
            _logger.LogInformation("TEST MODE: Cancelling mock subscription {SubscriptionId}", subscriptionId);
            return true;
        }

        var response = await _httpClient.PostAsync($"subscriptions/{subscriptionId}/cancel", null);
        return response.IsSuccessStatusCode;
    }

    public bool VerifyWebhookSignature(string payload, string signature, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var computedHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        var computedSignature = BitConverter.ToString(computedHash).Replace("-", "").ToLower();
        return computedSignature == signature;
    }
}
