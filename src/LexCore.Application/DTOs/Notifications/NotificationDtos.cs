namespace LexCore.Application.DTOs.Notifications;

public class NotificationDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Channel { get; set; } = string.Empty;
    public string DeliveryStatus { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public DateTime? SentAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid? CaseId { get; set; }
    public string? CaseTitle { get; set; }
    public string? FailureReason { get; set; }
}

public class NotificationListResponse
{
    public List<NotificationDto> Data { get; set; } = new();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int UnreadCount { get; set; }
}

public class UnreadCountDto
{
    public int Count { get; set; }
}

public class UpdateFcmTokenRequest
{
    public string Token { get; set; } = string.Empty;
}
