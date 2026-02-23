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
    
    public ICollection<DocumentVersion> Versions { get; set; } = new List<DocumentVersion>();
}
