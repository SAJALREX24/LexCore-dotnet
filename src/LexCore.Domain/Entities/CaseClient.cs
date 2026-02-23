namespace LexCore.Domain.Entities;

public class CaseClient : BaseEntity
{
    public Guid CaseId { get; set; }
    public Case? Case { get; set; }
    public Guid ClientId { get; set; }
    public User? Client { get; set; }
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
}
