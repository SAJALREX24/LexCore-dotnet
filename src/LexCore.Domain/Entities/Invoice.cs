using LexCore.Domain.Enums;

namespace LexCore.Domain.Entities;

public class Invoice : TenantEntity
{
    public Guid CaseId { get; set; }
    public Case? Case { get; set; }
    public Guid ClientId { get; set; }
    public User? Client { get; set; }
    public decimal Amount { get; set; }
    public decimal GstAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public InvoiceStatus Status { get; set; } = InvoiceStatus.Draft;
    public DateTime? DueDate { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? LineItems { get; set; }
    
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
}
