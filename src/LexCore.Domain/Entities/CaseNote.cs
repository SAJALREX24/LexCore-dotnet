namespace LexCore.Domain.Entities;

public class CaseNote : BaseEntity
{
    public Guid CaseId { get; set; }
    public Case? Case { get; set; }
    public Guid LawyerId { get; set; }
    public User? Lawyer { get; set; }
    public string Note { get; set; } = string.Empty;
    // NoteType: "Call", "Meeting", "Message", "Court Visit", "Other"
    public string NoteType { get; set; } = "Other";
}
