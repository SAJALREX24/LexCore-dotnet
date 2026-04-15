namespace LexCore.Domain.Entities;

public class Document : TenantEntity
{
    public Guid CaseId { get; set; }
    public Case? Case { get; set; }
    public Guid UploadedBy { get; set; }
    public User? Uploader { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FileUrl { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string? MimeType { get; set; }
    public int Version { get; set; } = 1;
    public bool IsClientVisible { get; set; } = true;
    public string? Tags { get; set; }
    public string? Description { get; set; }

    // Document categorisation (new)
    public string? DocumentCategory { get; set; }  // "ClientID","Vakalatnama","HearingDocument","CaseDocument","AIDraft"
    public string? DocumentTag { get; set; }        // "Aadhaar Card","Court Order · 15 Mar 2024" etc.
    public string? DocumentSource { get; set; }     // "CaseCreation","HearingUpload","DirectUpload","AIDraft"
    public Guid? HearingId { get; set; }            // FK to Hearing — nullable
    public DateTime? HearingDate { get; set; }  // denormalised hearing date for display without join
    public Hearing? Hearing { get; set; }           // navigation property
    public Guid? AIDraftId { get; set; }            // FK to AiDraft — nullable
    public bool IsAIDraft { get; set; } = false;
    public string? AIDraftStatus { get; set; }      // "Draft","Final" — only when IsAIDraft=true

    public ICollection<DocumentVersion> Versions { get; set; } = new List<DocumentVersion>();
}
