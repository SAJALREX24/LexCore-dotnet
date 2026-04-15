namespace LexCore.Domain.Entities;

public class AiAuditLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public string ActionType { get; set; } = string.Empty;
    public Guid? CaseId { get; set; }
    public int TokensInput { get; set; }
    public int TokensOutput { get; set; }
    public string? ModelUsed { get; set; }
    public int? LatencyMs { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string? IpAddress { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
