namespace LexCore.Domain.Entities;

public class AiMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ConversationId { get; set; }
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string ContentLanguage { get; set; } = "hi";
    public int TokenCount { get; set; }
    public string? ModelUsed { get; set; }
    public bool IsCompressed { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public AiConversation Conversation { get; set; } = null!;
}
