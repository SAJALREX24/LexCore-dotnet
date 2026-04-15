namespace LexCore.Application.Interfaces;

public interface IFcmService
{
    /// <summary>Send push to single device. Skip silently if token is null/empty.</summary>
    Task SendAsync(string? fcmToken, string title, string body,
                   Dictionary<string, string>? data = null);

    /// <summary>Send push to multiple devices. Skip null/empty tokens.</summary>
    Task SendToMultipleAsync(IEnumerable<string?> fcmTokens,
                              string title, string body);
}
