using System.ComponentModel.DataAnnotations;

namespace LexCore.Application.DTOs.Chat;

public class SendMessageRequest
{
    [Required]
    public string Message { get; set; } = string.Empty;

    public bool IsInternal { get; set; }
}

public class ChatMessageDto
{
    public Guid Id { get; set; }
    public Guid CaseId { get; set; }
    public Guid SenderId { get; set; }
    public string SenderName { get; set; } = string.Empty;
    public string SenderRole { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public bool IsInternal { get; set; }
    public DateTime SentAt { get; set; }
}
