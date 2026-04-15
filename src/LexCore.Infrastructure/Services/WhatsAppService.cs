using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using LexCore.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace LexCore.Infrastructure.Services;

public class WhatsAppService : IWhatsAppService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _apiKey;
    private readonly ILogger<WhatsAppService> _logger;

    public WhatsAppService(IHttpClientFactory httpClientFactory,
                           IConfiguration configuration,
                           ILogger<WhatsAppService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _apiKey = configuration["Fast2SMS:ApiKey"] ?? string.Empty;
        _logger = logger;
    }

    public async Task SendAsync(string? phoneNumber, string message)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
            return;

        try
        {
            var cleaned = Regex.Replace(phoneNumber, @"\D", string.Empty);

            var payload = new
            {
                route = "q",
                message,
                language = "english",
                flash = 0,
                numbers = cleaned
            };

            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.TryAddWithoutValidation("authorization", _apiKey);

            var response = await client.PostAsync("https://www.fast2sms.com/dev/bulkV2", content);

            if (response.IsSuccessStatusCode)
            {
                var maskedWa = cleaned.Length >= 4 ? "****" + cleaned[^4..] : "****";
                _logger.LogInformation("WhatsApp message sent successfully to {PhoneNumber}.", maskedWa);
            }
            else
            {
                var maskedWaWarn = cleaned.Length >= 4 ? "****" + cleaned[^4..] : "****";
                _logger.LogWarning("WhatsApp send failed for {PhoneNumber}. Status: {StatusCode}",
                    maskedWaWarn, (int)response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "WhatsApp send threw an exception: {Message}", ex.Message);
        }
    }
}
