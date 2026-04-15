using System.Security.Cryptography;
using System.Text;
using LexCore.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace LexCore.Infrastructure.Services;

public class RazorpayService : IRazorpayService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<RazorpayService> _logger;
    private readonly HttpClient _httpClient;

    public RazorpayService(IConfiguration configuration, ILogger<RazorpayService> logger, IHttpClientFactory httpClientFactory)
    {
        _configuration = configuration;
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient("Razorpay");

        var testMode = configuration["Razorpay:TestMode"]?.ToLower() == "true";
        if (!testMode)
        {
            var keyId = configuration["Razorpay:KeyId"];
            var keySecret = configuration["Razorpay:KeySecret"];
            var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{keyId}:{keySecret}"));
            _httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", credentials);
            _httpClient.BaseAddress = new Uri("https://api.razorpay.com/v1/");
        }
    }

    public bool VerifyWebhookSignature(string payload, string signature, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var computedHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        var computedSignature = BitConverter.ToString(computedHash).Replace("-", "").ToLower();

        // CAT-3 fix: use constant-time comparison to prevent timing attacks
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(computedSignature),
            Encoding.UTF8.GetBytes(signature));
    }
}
