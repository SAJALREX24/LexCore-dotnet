using LexCore.Domain.Enums;

namespace LexCore.Domain.Entities;

public class Subscription : TenantEntity
{
    public SubscriptionPlan Plan { get; set; }
    public SubscriptionStatus Status { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string? RazorpaySubscriptionId { get; set; }
}
