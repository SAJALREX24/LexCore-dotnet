namespace LexCore.Domain.Entities;

public class AiConversation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public Guid? CaseId { get; set; }
    public string ConversationType { get; set; } = "chat";
    public string? Title { get; set; }
    public string? Summary { get; set; }
    public int SummaryTokenCount { get; set; }
    public int TotalTokensUsed { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<AiMessage> Messages { get; set; } = new List<AiMessage>();
}
