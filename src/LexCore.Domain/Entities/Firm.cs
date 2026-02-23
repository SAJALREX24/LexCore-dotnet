using LexCore.Domain.Enums;

namespace LexCore.Domain.Entities;

public class Firm : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public SubscriptionStatus SubscriptionStatus { get; set; } = SubscriptionStatus.Active;
    public SubscriptionPlan Plan { get; set; } = SubscriptionPlan.Trial;
    public Guid OwnerId { get; set; }
    public string? GstNumber { get; set; }
    public string? Address { get; set; }
    public string? LogoUrl { get; set; }
    
    public ICollection<User> Users { get; set; } = new List<User>();
    public ICollection<Case> Cases { get; set; } = new List<Case>();
    public ICollection<Document> Documents { get; set; } = new List<Document>();
    public ICollection<Hearing> Hearings { get; set; } = new List<Hearing>();
    public ICollection<Chat> Chats { get; set; } = new List<Chat>();
    public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
    public ICollection<Subscription> Subscriptions { get; set; } = new List<Subscription>();
    public ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();
    public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
}
