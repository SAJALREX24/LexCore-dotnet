namespace LexCore.Domain.Entities;

public class AiUsageQuota
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string PlanTier { get; set; } = "basic";

    // Month-scoped counters — reset each calendar month
    // MonthYear format: "2025-01", "2025-02", etc.
    public string MonthYear { get; set; } = DateTime.UtcNow.ToString("yyyy-MM");
    public int ChatCount { get; set; } = 0;
    public int DraftCount { get; set; } = 0;
    public int ResearchCount { get; set; } = 0;
    public int TotalTokensUsed { get; set; } = 0;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Monthly limits per plan tier
    public static (int chat, int draft, int research) GetLimits(string planTier) =>
        planTier switch
        {
            "pro"        => (200, 100, 50),
            "enterprise" => (500, 200, 100),
            _            => (50,  20,  10)   // basic (default for solo lawyers)
        };
}
