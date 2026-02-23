using LexCore.Domain.Enums;

namespace LexCore.Domain.Entities;

public class User : TenantEntity
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public bool IsVerified { get; set; }
    public string? InviteToken { get; set; }
    public DateTime? InviteTokenExpiry { get; set; }
    public string? RefreshToken { get; set; }
    public DateTime? RefreshTokenExpiry { get; set; }
    public DateTime? LastLogin { get; set; }
    public string? VerificationToken { get; set; }
    public DateTime? VerificationTokenExpiry { get; set; }
    public string? PasswordResetToken { get; set; }
    public DateTime? PasswordResetTokenExpiry { get; set; }
    
    public ICollection<CaseLawyer> CaseLawyers { get; set; } = new List<CaseLawyer>();
    public ICollection<CaseClient> CaseClients { get; set; } = new List<CaseClient>();
    public ICollection<Document> UploadedDocuments { get; set; } = new List<Document>();
    public ICollection<Chat> SentChats { get; set; } = new List<Chat>();
    public ICollection<Invoice> ClientInvoices { get; set; } = new List<Invoice>();
    public ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();
    public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
}
