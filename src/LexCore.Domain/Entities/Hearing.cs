using LexCore.Domain.Enums;

namespace LexCore.Domain.Entities;

public class Hearing : TenantEntity
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
}
