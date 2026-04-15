using LexCore.Domain.Enums;

namespace LexCore.Domain.Entities;

public class Notification : BaseEntity
{
    // Who receives this notification
    public Guid LawyerId { get; set; }
    public User? Lawyer { get; set; }

    // What it relates to (both optional)
    public Guid? CaseId { get; set; }
    public Case? Case { get; set; }

    public Guid? HearingId { get; set; }
    public Hearing? Hearing { get; set; }

    // Content
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;

    // Classification
    public NotificationType Type { get; set; }
    public NotificationChannel Channel { get; set; }
    public NotificationDeliveryStatus DeliveryStatus { get; set; }
        = NotificationDeliveryStatus.Pending;

    // State tracking
    public bool IsRead { get; set; } = false;
    public DateTime? SentAt { get; set; }
    public string? FailureReason { get; set; }
}
