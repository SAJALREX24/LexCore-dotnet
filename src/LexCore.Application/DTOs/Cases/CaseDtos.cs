using System.ComponentModel.DataAnnotations;
using LexCore.Domain.Enums;

namespace LexCore.Application.DTOs.Cases;

public class CreateCaseRequest
{
    [Required]
    [StringLength(500)]
    public string Title { get; set; } = string.Empty;

    public string? CaseBackground { get; set; }
    public string? CaseType { get; set; }
    public string? CourtName { get; set; }
    public DateTime? FiledDate { get; set; }
    public string? PrivateNotes { get; set; }
    public string? ClientInstructions { get; set; }

    // Client & Parties
    public string? ClientName { get; set; }
    public string? ClientPhone { get; set; }
    public string? ClientWhatsApp { get; set; }
    public string? ClientPosition { get; set; }
    public string? OppositeParty { get; set; }
    public string? OppositePartyLawyer { get; set; }

    // Court & Legal
    public string? SectionAct { get; set; }
    public string? FIRNumber { get; set; }
    public string? CaseStage { get; set; }
    public string? ReliefSought { get; set; }

    // Fees
    public string? FeeType { get; set; }
    public decimal? AgreedFees { get; set; }
    public decimal? AdvancePaid { get; set; }
    public decimal? PerHearingFee { get; set; }
    public bool VakalatnamaSigned { get; set; } = false;

    // Screen 1 — Court hierarchy
    public string? CourtType { get; set; }
    public string? StateUT { get; set; }
    public string? District { get; set; }
    public string? CourtHierarchyName { get; set; }
    public string? CaseTypeCode { get; set; }
    public string? CaseNature { get; set; }

    // Screen 2 — Client details
    public string? ClientType { get; set; }
    public string? ClientFatherName { get; set; }
    public int? ClientAge { get; set; }
    public string? ClientAddress { get; set; }
    public string? ClientIDDocumentType { get; set; }
    public string? CompanyName { get; set; }
    public string? CompanyCIN { get; set; }
    public string? CompanyGST { get; set; }
    public string? AuthorisedRepresentative { get; set; }
    public string? AuthorisedRepresentativeDesignation { get; set; }

    // Screen 3 — Opposite party
    public string? OppositePartiesJson { get; set; }
    public string? OppositeCounselName { get; set; }
    public string? OppositeCounselPhone { get; set; }
    public string? OppositeCounselEnrollment { get; set; }
    public string? OppositeCounselCity { get; set; }

    // Screen 4 — Legal details
    public string? ActsAndSectionsJson { get; set; }
    public DateTime? FIRDate { get; set; }
    public string? PSDistrict { get; set; }
    public string? PSState { get; set; }
    public string? NatureOfOffence { get; set; }
    public string? CaseNotesHtml { get; set; }

    // Screen 5 — Fee
    public string? PaymentMode { get; set; }
    public string? PaymentReference { get; set; }
    public DateTime? PaymentDate { get; set; }

    // Screen 6 — Notifications
    public bool HearingReminderEvening { get; set; } = true;
    public bool HearingReminderMorning { get; set; } = true;
    public bool LimitationAlertEnabled { get; set; } = true;
    public bool InvoiceOverdueAlert { get; set; } = true;
    public bool CaseStageChangeAlert { get; set; } = false;
    public bool ClientWhatsAppEnabled { get; set; } = false;
    public string? PrivateNotesHtml { get; set; }
    public string? ClientInstructionsHtml { get; set; }

    // Dates & Notifications
    public DateTime? LimitationDate { get; set; }
    public bool NotifyClientOnHearing { get; set; } = true;
    public bool NotifyClientOnStatus { get; set; } = true;
}

public class UpdateCaseRequest
{
    public string? Title { get; set; }
    public string? CaseBackground { get; set; }
    public string? CaseType { get; set; }
    public string? CourtName { get; set; }
    public DateTime? FiledDate { get; set; }
    public string? PrivateNotes { get; set; }
    public string? ClientInstructions { get; set; }
    public CaseStatus? Status { get; set; }

    // Client & Parties
    public string? ClientName { get; set; }
    public string? ClientPhone { get; set; }
    public string? ClientWhatsApp { get; set; }
    public string? ClientPosition { get; set; }
    public string? OppositeParty { get; set; }
    public string? OppositePartyLawyer { get; set; }

    // Court & Legal
    public string? SectionAct { get; set; }
    public string? FIRNumber { get; set; }
    public string? CaseStage { get; set; }
    public string? ReliefSought { get; set; }

    // Fees
    public string? FeeType { get; set; }
    public decimal? PerHearingFee { get; set; }
    public bool? VakalatnamaSigned { get; set; }

    // Dates & Notifications
    public DateTime? LimitationDate { get; set; }
    public bool? NotifyClientOnHearing { get; set; }
    public bool? NotifyClientOnStatus { get; set; }

    public string? CourtType { get; set; }
    public string? StateUT { get; set; }
    public string? District { get; set; }
    public string? CourtHierarchyName { get; set; }
    public string? CaseTypeCode { get; set; }
    public string? CaseNature { get; set; }
    public string? ClientType { get; set; }
    public string? ClientFatherName { get; set; }
    public int? ClientAge { get; set; }
    public string? ClientAddress { get; set; }
    public string? ClientIDDocumentType { get; set; }
    public string? CompanyName { get; set; }
    public string? CompanyCIN { get; set; }
    public string? CompanyGST { get; set; }
    public string? AuthorisedRepresentative { get; set; }
    public string? AuthorisedRepresentativeDesignation { get; set; }
    public string? OppositePartiesJson { get; set; }
    public string? OppositeCounselName { get; set; }
    public string? OppositeCounselPhone { get; set; }
    public string? OppositeCounselEnrollment { get; set; }
    public string? OppositeCounselCity { get; set; }
    public string? ActsAndSectionsJson { get; set; }
    public DateTime? FIRDate { get; set; }
    public string? PSDistrict { get; set; }
    public string? PSState { get; set; }
    public string? NatureOfOffence { get; set; }
    public string? CaseNotesHtml { get; set; }
    public bool? HearingReminderEvening { get; set; }
    public bool? HearingReminderMorning { get; set; }
    public bool? LimitationAlertEnabled { get; set; }
    public bool? InvoiceOverdueAlert { get; set; }
    public bool? CaseStageChangeAlert { get; set; }
    public bool? ClientWhatsAppEnabled { get; set; }
    public string? PrivateNotesHtml { get; set; }
    public string? ClientInstructionsHtml { get; set; }
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
    [StringLength(2000)]
    public string Note { get; set; } = string.Empty;

    public string NoteType { get; set; } = "Other";
}

public class CaseNoteDto
{
    public Guid Id { get; set; }
    public string Note { get; set; } = string.Empty;
    public string NoteType { get; set; } = "Other";
    public string? LawyerName { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CaseDto
{
    public Guid Id { get; set; }
    public string CaseNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? CaseBackground { get; set; }
    public string? CaseType { get; set; }
    public string? CourtName { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? FiledDate { get; set; }
    public string? PrivateNotes { get; set; }
    public string? ClientInstructions { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<AssignedUserDto> AssignedLawyers { get; set; } = new();
    public List<AssignedUserDto> AssignedClients { get; set; } = new();
    public int DocumentsCount { get; set; }
    public int HearingsCount { get; set; }

    // Client & Parties
    public string? ClientName { get; set; }
    public string? ClientPhone { get; set; }
    public string? ClientWhatsApp { get; set; }
    public string? ClientPosition { get; set; }
    public string? OppositeParty { get; set; }
    public string? OppositePartyLawyer { get; set; }

    // Court & Legal
    public string? SectionAct { get; set; }
    public string? FIRNumber { get; set; }
    public string? CaseStage { get; set; }
    public string? ReliefSought { get; set; }

    // Fees
    public string? FeeType { get; set; }
    public decimal? AgreedFees { get; set; }
    public decimal? AdvancePaid { get; set; }
    public decimal? PerHearingFee { get; set; }
    public bool VakalatnamaSigned { get; set; }

    // Dates
    public DateTime? LimitationDate { get; set; }

    // Notifications
    public bool NotifyClientOnHearing { get; set; }
    public bool NotifyClientOnStatus { get; set; }
    public bool LimitationAlertSent30 { get; set; }
    public bool LimitationAlertSent7 { get; set; }
    public bool LimitationAlertSent1 { get; set; }

    // Screen 1
    public string? CourtType { get; set; }
    public string? StateUT { get; set; }
    public string? District { get; set; }
    public string? CourtHierarchyName { get; set; }
    public string? CaseTypeCode { get; set; }
    public string? CaseNature { get; set; }
    public DateTime? TotalFeeLockedAt { get; set; }

    // Screen 2
    public string? ClientType { get; set; }
    public string? ClientFatherName { get; set; }
    public int? ClientAge { get; set; }
    public string? ClientAddress { get; set; }
    public string? ClientIDDocumentType { get; set; }
    public string? CompanyName { get; set; }
    public string? CompanyCIN { get; set; }
    public string? CompanyGST { get; set; }
    public string? AuthorisedRepresentative { get; set; }
    public string? AuthorisedRepresentativeDesignation { get; set; }

    // Screen 3
    public string? OppositePartiesJson { get; set; }
    public string? OppositeCounselName { get; set; }
    public string? OppositeCounselPhone { get; set; }
    public string? OppositeCounselEnrollment { get; set; }
    public string? OppositeCounselCity { get; set; }

    // Screen 4
    public string? ActsAndSectionsJson { get; set; }
    public DateTime? FIRDate { get; set; }
    public string? PSDistrict { get; set; }
    public string? PSState { get; set; }
    public string? NatureOfOffence { get; set; }
    public string? CaseNotesHtml { get; set; }

    // Screen 5
    public string? PaymentMode { get; set; }
    public string? PaymentReference { get; set; }

    // Screen 6
    public bool HearingReminderEvening { get; set; }
    public bool HearingReminderMorning { get; set; }
    public bool LimitationAlertEnabled { get; set; }
    public bool InvoiceOverdueAlert { get; set; }
    public bool CaseStageChangeAlert { get; set; }
    public bool ClientWhatsAppEnabled { get; set; }
    public string? PrivateNotesHtml { get; set; }
    public string? ClientInstructionsHtml { get; set; }
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
    public string? ClientName { get; set; }
    public string? CaseStage { get; set; }
    public DateTime? LimitationDate { get; set; }
    public DateTime? NextHearingDate { get; set; }
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
