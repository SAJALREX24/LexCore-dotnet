using LexCore.Domain.Enums;

namespace LexCore.Domain.Entities;

public class Case : TenantEntity
{
    public string CaseNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? CaseType { get; set; }
    public string? CourtName { get; set; }
    public CaseStatus Status { get; set; } = CaseStatus.Pending;
    public DateTime? FiledDate { get; set; }
    public string? InternalNotes { get; set; }
    public string? ClientVisibleNotes { get; set; }
    
    public ICollection<CaseLawyer> CaseLawyers { get; set; } = new List<CaseLawyer>();
    public ICollection<CaseClient> CaseClients { get; set; } = new List<CaseClient>();
    public ICollection<Document> Documents { get; set; } = new List<Document>();
    public ICollection<Hearing> Hearings { get; set; } = new List<Hearing>();
    public ICollection<Chat> Chats { get; set; } = new List<Chat>();
    public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
}
