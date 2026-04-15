namespace LexCore.Domain.Entities;

public class AiResearch
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public Guid? CaseId { get; set; }
    public string Query { get; set; } = string.Empty;
    public string? QueryHash { get; set; }
    public string Result { get; set; } = string.Empty;
    public string? Citations { get; set; }
    public string? RelevantSections { get; set; }
    public int TokenCount { get; set; }
    public bool IsCached { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
