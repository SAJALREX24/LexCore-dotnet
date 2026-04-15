using LexCore.Application.Interfaces;
using LexCore.Domain.Entities;
using LexCore.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace LexCore.Infrastructure.Services;

public class NotificationService : INotificationService
{
    private readonly INotificationRepository _repo;
    private readonly IFcmService _fcmService;
    private readonly IWhatsAppService _whatsAppService;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(INotificationRepository repo,
                                IFcmService fcmService,
                                IWhatsAppService whatsAppService,
                                ILogger<NotificationService> logger)
    {
        _repo = repo;
        _fcmService = fcmService;
        _whatsAppService = whatsAppService;
        _logger = logger;
    }

    public async Task NotifyLawyerAsync(
        Guid lawyerId,
        string title,
        string body,
        NotificationType type,
        Guid? caseId = null,
        Guid? hearingId = null)
    {
        try
        {
            // Step 1: Create notification
            var notification = new Notification
            {
                LawyerId = lawyerId,
                Title = title,
                Body = body,
                Type = type,
                CaseId = caseId,
                HearingId = hearingId,
                Channel = NotificationChannel.InApp,
                DeliveryStatus = NotificationDeliveryStatus.Pending,
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            };

            // Step 2: Save to DB
            await _repo.AddAsync(notification);
            await _repo.SaveChangesAsync();

            // Step 3: Get lawyer FCM token
            var fcmToken = await _repo.GetLawyerFcmTokenAsync(lawyerId);

            // Step 4: Send push if token exists
            if (!string.IsNullOrWhiteSpace(fcmToken))
            {
                await _fcmService.SendAsync(fcmToken, title, body);
                notification.Channel = NotificationChannel.Push;
                notification.DeliveryStatus = NotificationDeliveryStatus.Sent;
                notification.SentAt = DateTime.UtcNow;
            }
            else
            {
                notification.Channel = NotificationChannel.InApp;
                notification.DeliveryStatus = NotificationDeliveryStatus.Sent;
            }

            // Step 5: Save updated delivery status
            await _repo.SaveChangesAsync();

            // Step 6: Log success
            _logger.LogInformation("Notification sent to lawyer {LawyerId}: {Title}", lawyerId, title);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send notification to lawyer {LawyerId}: {Title}", lawyerId, title);
        }
    }

    public async Task NotifyClientWhatsAppAsync(string? whatsAppNumber, string message)
    {
        try
        {
            await _whatsAppService.SendAsync(whatsAppNumber, message);
            // Log only last 4 digits — never log full phone number
            var maskedNumber = whatsAppNumber?.Length >= 4
                ? "****" + whatsAppNumber[^4..]
                : "****";
            _logger.LogInformation("WhatsApp notification dispatched to {Number}.", maskedNumber);
        }
        catch (Exception ex)
        {
            var maskedNumber = whatsAppNumber?.Length >= 4
                ? "****" + whatsAppNumber[^4..]
                : "****";
            _logger.LogError(ex, "Failed to send WhatsApp notification to {Number}.", maskedNumber);
        }
    }
}
