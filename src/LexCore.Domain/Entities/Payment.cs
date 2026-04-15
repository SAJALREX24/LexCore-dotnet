namespace LexCore.Domain.Entities;

public class Payment : TenantEntity
{
    // For invoice payments: InvoiceId is set, CaseId is null
    // For advance payments: CaseId is set, InvoiceId is null
    public Guid? InvoiceId { get; set; }
    public Invoice? Invoice { get; set; }

    public Guid? CaseId { get; set; }
    public Case? Case { get; set; }

    public string? RazorpayPaymentId { get; set; }
    public decimal Amount { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? PaidAt { get; set; }

    // Payment details (new)
    public string? PaymentMode { get; set; }      // "Cash","UPI","BankTransfer","Cheque","DD"
    public string? ReferenceNumber { get; set; }  // UTR/cheque no/transaction ID
    public string? PaymentType { get; set; }      // "Advance","Invoice","Partial"
    public bool IsAdvancePayment { get; set; } = false;
}
