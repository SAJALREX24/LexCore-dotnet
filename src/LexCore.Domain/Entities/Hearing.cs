using LexCore.Domain.Enums;

namespace LexCore.Domain.Entities;

public class Hearing : BaseEntity
{
    public Guid CaseId { get; set; }
    public Case? Case { get; set; }
    public DateTime HearingDate { get; set; }
    public TimeSpan HearingTime { get; set; }
    public string? CourtName { get; set; }
    public string? JudgeName { get; set; }
    public string? Notes { get; set; }
    public HearingStatus Status { get; set; } = HearingStatus.Scheduled;
    public bool ReminderSent { get; set; }

    // Post-hearing outcome fields
    public string? Outcome { get; set; }
    public string? JudgeOrder { get; set; }
    public DateTime? NextHearingDate { get; set; }
    public TimeSpan? NextHearingTime { get; set; }
    public string? ActionRequired { get; set; }
    public bool UpdatedAfterHearing { get; set; } = false;
    public DateTime? UpdatedAfterAt { get; set; }
}
