namespace LexCore.Domain.Entities;

public class DocumentVersion : BaseEntity
{
    public Guid DocumentId { get; set; }
    public Document? Document { get; set; }
    public int VersionNumber { get; set; }
    public string FileUrl { get; set; } = string.Empty;
    public Guid UploadedBy { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
}
