using LexCore.Domain.Enums;

namespace LexCore.Domain.Entities;

public class Notification : TenantEntity
{
    public Guid UserId { get; set; }
    public User? User { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public NotificationType Type { get; set; }
    public bool IsRead { get; set; }
}
