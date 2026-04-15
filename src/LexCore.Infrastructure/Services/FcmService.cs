using System.Text;
using System.Text.Json;
using LexCore.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace LexCore.Infrastructure.Services;

public class FcmService : IFcmService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _serverKey;
    private readonly ILogger<FcmService> _logger;

    public FcmService(IHttpClientFactory httpClientFactory,
                      IConfiguration configuration,
                      ILogger<FcmService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _serverKey = configuration["Firebase:ServerKey"] ?? string.Empty;
        _logger = logger;
    }

    public async Task SendAsync(string? fcmToken, string title, string body,
                                Dictionary<string, string>? data = null)
    {
        if (string.IsNullOrWhiteSpace(fcmToken))
        {
            _logger.LogWarning("FCM send skipped — token is null or empty.");
            return;
        }

        try
        {
            var payload = new
            {
                to = fcmToken,
                notification = new { title, body },
                data = data ?? new Dictionary<string, string>()
            };

            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", $"key={_serverKey}");

            var response = await client.PostAsync("https://fcm.googleapis.com/fcm/send", content);

            if (response.IsSuccessStatusCode)
                _logger.LogInformation("FCM push sent successfully. Status: {StatusCode}", (int)response.StatusCode);
            else
                _logger.LogWarning("FCM push failed. Status: {StatusCode}", (int)response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "FCM push threw an exception: {Message}", ex.Message);
        }
    }

    public async Task SendToMultipleAsync(IEnumerable<string?> fcmTokens, string title, string body)
    {
        var validTokens = fcmTokens.Where(t => !string.IsNullOrWhiteSpace(t));
        foreach (var token in validTokens)
            await SendAsync(token, title, body);
    }
}
