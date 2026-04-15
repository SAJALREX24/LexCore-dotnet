using LexCore.Domain.Enums;

namespace LexCore.Domain.Entities;

public class Invoice : TenantEntity
{
    public Guid CaseId { get; set; }
    public Case? Case { get; set; }
    public Guid? ClientId { get; set; }
    public User? Client { get; set; }
    public string? ClientName { get; set; }
    public string? ClientEmail { get; set; }
    public string? ClientPhone { get; set; }
    public decimal Amount { get; set; }
    public decimal GstAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public InvoiceStatus Status { get; set; } = InvoiceStatus.Draft;
    public DateTime? DueDate { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? LineItems { get; set; }
    public bool IsInterState { get; set; } = false;
    public decimal PaidAmount { get; set; } = 0;
    public string? PaymentMode { get; set; }
    public string? TxnReference { get; set; }
    public DateTime? PaymentDate { get; set; }

    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
}
