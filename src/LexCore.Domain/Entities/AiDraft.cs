namespace LexCore.Domain.Entities;

public class AiDraft
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public Guid? CaseId { get; set; }
    public string DocumentType { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string? SmartExtractUsed { get; set; }
    public int TokenCount { get; set; }
    public int Version { get; set; } = 1;
    public string Status { get; set; } = "draft";
    public bool PrintReady { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
