using LexCore.Domain.Enums;

namespace LexCore.Domain.Entities;

public class User : TenantEntity
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? BarEnrollmentNumber { get; set; }
    public string? CourtType { get; set; }
    public string? State { get; set; }
    public string? City { get; set; }
    public UserRole Role { get; set; }
    public bool IsVerified { get; set; }
    public string? PhoneOtp { get; set; }
    public DateTime? PhoneOtpExpiry { get; set; }
    public bool IsPhoneVerified { get; set; }
    public string? InviteToken { get; set; }
    public DateTime? InviteTokenExpiry { get; set; }
    public string? RefreshToken { get; set; }
    public DateTime? RefreshTokenExpiry { get; set; }
    public DateTime? LastLogin { get; set; }
    public string? VerificationToken { get; set; }
    public DateTime? VerificationTokenExpiry { get; set; }
    public string? PasswordResetToken { get; set; }
    public DateTime? PasswordResetTokenExpiry { get; set; }
    public string? FcmToken { get; set; }
    // Login brute force protection
    public int FailedLoginAttempts { get; set; } = 0;
    public DateTime? LockoutEnd { get; set; }

    // OTP brute force protection
    public int OtpFailedAttempts { get; set; } = 0;
    public DateTime? OtpLockoutEnd { get; set; }

    public ICollection<CaseLawyer> CaseLawyers { get; set; } = new List<CaseLawyer>();
    public ICollection<CaseClient> CaseClients { get; set; } = new List<CaseClient>();
    public ICollection<Document> UploadedDocuments { get; set; } = new List<Document>();
    public ICollection<Chat> SentChats { get; set; } = new List<Chat>();
    public ICollection<Invoice> ClientInvoices { get; set; } = new List<Invoice>();
    public ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();
    public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
}
