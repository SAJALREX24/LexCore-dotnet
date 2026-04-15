using LexCore.Domain.Enums;

namespace LexCore.Application.Interfaces;

public interface INotificationService
{
    /// <summary>
    /// Save in-app notification to DB and send Firebase push if lawyer has FcmToken.
    /// Never throws — logs failures internally.
    /// </summary>
    Task NotifyLawyerAsync(
        Guid lawyerId,
        string title,
        string body,
        NotificationType type,
        Guid? caseId = null,
        Guid? hearingId = null);

    /// <summary>
    /// Send WhatsApp to client via Fast2SMS.
    /// Never throws — logs failures internally.
    /// </summary>
    Task NotifyClientWhatsAppAsync(string? whatsAppNumber, string message);
}
