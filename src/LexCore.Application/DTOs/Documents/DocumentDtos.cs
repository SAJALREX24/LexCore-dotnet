using System.ComponentModel.DataAnnotations;

namespace LexCore.Application.DTOs.Documents;

public class UploadDocumentRequest
{
    [Required]
    public Guid CaseId { get; set; }

    public string? Description { get; set; }
    public string? Tags { get; set; }
    public bool IsClientVisible { get; set; } = true;
}

public class UpdateDocumentRequest
{
    public string? Description { get; set; }
    public string? Tags { get; set; }
    public bool? IsClientVisible { get; set; }
}

public class DocumentDto
{
    public Guid Id { get; set; }
    public Guid CaseId { get; set; }
    public string CaseTitle { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string? MimeType { get; set; }
    public long FileSize { get; set; }
    public int Version { get; set; }
    public bool IsClientVisible { get; set; }
    public string? Tags { get; set; }
    public string? Description { get; set; }
    public string UploadedByName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class DocumentVersionDto
{
    public Guid Id { get; set; }
    public int VersionNumber { get; set; }
    public string UploadedByName { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; }
}
