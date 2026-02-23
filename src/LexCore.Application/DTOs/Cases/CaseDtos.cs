using System.ComponentModel.DataAnnotations;
using LexCore.Domain.Enums;

namespace LexCore.Application.DTOs.Cases;

public class CreateCaseRequest
{
    [Required]
    [StringLength(500)]
    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }
    public string? CaseType { get; set; }
    public string? CourtName { get; set; }
    public DateTime? FiledDate { get; set; }
    public string? InternalNotes { get; set; }
    public string? ClientVisibleNotes { get; set; }
}

public class UpdateCaseRequest
{
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? CaseType { get; set; }
    public string? CourtName { get; set; }
    public DateTime? FiledDate { get; set; }
    public string? InternalNotes { get; set; }
    public string? ClientVisibleNotes { get; set; }
}

public class UpdateCaseStatusRequest
{
    [Required]
    public CaseStatus Status { get; set; }
}

public class AssignUserRequest
{
    [Required]
    public Guid UserId { get; set; }
}

public class AddCaseNoteRequest
{
    [Required]
    public string Note { get; set; } = string.Empty;
    public bool IsClientVisible { get; set; }
}

public class CaseDto
{
    public Guid Id { get; set; }
    public string CaseNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? CaseType { get; set; }
    public string? CourtName { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? FiledDate { get; set; }
    public string? InternalNotes { get; set; }
    public string? ClientVisibleNotes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<AssignedUserDto> AssignedLawyers { get; set; } = new();
    public List<AssignedUserDto> AssignedClients { get; set; } = new();
    public int DocumentsCount { get; set; }
    public int HearingsCount { get; set; }
}

public class CaseListDto
{
    public Guid Id { get; set; }
    public string CaseNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? CaseType { get; set; }
    public string? CourtName { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? FiledDate { get; set; }
    public DateTime CreatedAt { get; set; }
    public int LawyersCount { get; set; }
    public int ClientsCount { get; set; }
}

public class AssignedUserDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public DateTime AssignedAt { get; set; }
}

public class CaseTimelineDto
{
    public Guid Id { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? UserName { get; set; }
    public DateTime Timestamp { get; set; }
    public string? Details { get; set; }
}

public class CaseFilterRequest
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public CaseStatus? Status { get; set; }
    public string? CaseType { get; set; }
    public Guid? LawyerId { get; set; }
    public string? Search { get; set; }
}
