namespace LexCore.Domain.Entities;

public class AiResearchCache
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string QueryHash { get; set; } = string.Empty;
    public string QueryNormalized { get; set; } = string.Empty;
    public string Result { get; set; } = string.Empty;
    public string? Citations { get; set; }
    public int HitCount { get; set; } = 1;
    public DateTime LastAccessed { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
