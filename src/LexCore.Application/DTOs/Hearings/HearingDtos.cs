using System.ComponentModel.DataAnnotations;
using LexCore.Domain.Enums;

namespace LexCore.Application.DTOs.Hearings;

public class CreateHearingRequest
{
    [Required]
    public Guid CaseId { get; set; }

    [Required]
    public DateTime HearingDate { get; set; }

    [Required]
    public TimeSpan HearingTime { get; set; }

    public string? CourtName { get; set; }
    public string? JudgeName { get; set; }
    public string? Notes { get; set; }
}

public class UpdateHearingRequest
{
    public DateTime? HearingDate { get; set; }
    public TimeSpan? HearingTime { get; set; }
    public string? CourtName { get; set; }
    public string? JudgeName { get; set; }
    public string? Notes { get; set; }
}

public class UpdateHearingStatusRequest
{
    [Required]
    public HearingStatus Status { get; set; }
}

public class HearingDto
{
    public Guid Id { get; set; }
    public Guid CaseId { get; set; }
    public string CaseNumber { get; set; } = string.Empty;
    public string CaseTitle { get; set; } = string.Empty;
    public DateTime HearingDate { get; set; }
    public TimeSpan HearingTime { get; set; }
    public string? CourtName { get; set; }
    public string? JudgeName { get; set; }
    public string? Notes { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool ReminderSent { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class HearingListDto
{
    public Guid Id { get; set; }
    public Guid CaseId { get; set; }
    public string CaseNumber { get; set; } = string.Empty;
    public string CaseTitle { get; set; } = string.Empty;
    public DateTime HearingDate { get; set; }
    public TimeSpan HearingTime { get; set; }
    public string? CourtName { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class HearingCalendarDto
{
    public int Month { get; set; }
    public int Year { get; set; }
    public List<HearingCalendarDay> Days { get; set; } = new();
}

public class HearingCalendarDay
{
    public int Day { get; set; }
    public List<HearingListDto> Hearings { get; set; } = new();
}

public class HearingFilterRequest
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public Guid? CaseId { get; set; }
    public HearingStatus? Status { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
}
