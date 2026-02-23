namespace LexCore.Domain.Entities;

public class Payment : TenantEntity
{
    public Guid InvoiceId { get; set; }
    public Invoice? Invoice { get; set; }
    public string? RazorpayPaymentId { get; set; }
    public decimal Amount { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? PaidAt { get; set; }
}
