namespace LexCore.Domain.Entities;

public class Chat : TenantEntity
{
    public Guid CaseId { get; set; }
    public Case? Case { get; set; }
    public Guid SenderId { get; set; }
    public User? Sender { get; set; }
    public string Message { get; set; } = string.Empty;
    public bool IsInternal { get; set; }
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
}
