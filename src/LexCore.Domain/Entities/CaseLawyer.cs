namespace LexCore.Domain.Entities;

public class CaseLawyer : BaseEntity
{
    public Guid CaseId { get; set; }
    public Case? Case { get; set; }
    public Guid LawyerId { get; set; }
    public User? Lawyer { get; set; }
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
}
