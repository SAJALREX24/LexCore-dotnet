using System.ComponentModel.DataAnnotations;
using LexCore.Domain.Enums;

namespace LexCore.Application.DTOs.Billing;

public class CreateInvoiceRequest
{
    [Required]
    public Guid CaseId { get; set; }

    // Optional — only needed if client is a registered app user
    public Guid? ClientId { get; set; }

    [Required]
    [Range(0.01, double.MaxValue)]
    public decimal Amount { get; set; }

    public string? Description { get; set; }
    public string? LineItems { get; set; }
    public DateTime? DueDate { get; set; }
    public decimal? GstAmount { get; set; }
    public bool IsInterState { get; set; } = false;
}

public class UpdateInvoiceRequest
{
    [Range(0.01, double.MaxValue)]
    public decimal? Amount { get; set; }
    public decimal? GstAmount { get; set; }
    public string? Description { get; set; }
    public DateTime? DueDate { get; set; }
    public string? LineItems { get; set; }
    public bool? IsInterState { get; set; }
}

public class MarkPaidRequest
{
    public decimal? Amount { get; set; }
    public string? PaymentMode { get; set; }
    public string? TxnReference { get; set; }
    public DateTime? PaymentDate { get; set; }
}

public class InvoiceDto
{
    public Guid Id { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public Guid CaseId { get; set; }
    public string CaseTitle { get; set; } = string.Empty;
    public Guid? ClientId { get; set; }
    public string ClientName { get; set; } = string.Empty;
    public string? ClientEmail { get; set; }
    public decimal Amount { get; set; }
    public decimal GstAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? DueDate { get; set; }
    public string? Description { get; set; }
    public string? LineItems { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsInterState { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal OutstandingAmount { get; set; }
    public string? PaymentMode { get; set; }
    public string? TxnReference { get; set; }
    public DateTime? PaymentDate { get; set; }
    public List<PaymentHistoryDto> PaymentHistory { get; set; } = new();
}

public class InvoiceListDto
{
    public Guid Id { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public Guid CaseId { get; set; }
    public string CaseTitle { get; set; } = string.Empty;
    public string ClientName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal GstAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal OutstandingAmount { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? DueDate { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? PaymentMode { get; set; }
    public string? TxnReference { get; set; }
    public DateTime? PaymentDate { get; set; }
    public bool IsInterState { get; set; }
}

public class InvoiceFilterRequest
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public InvoiceStatus? Status { get; set; }
    public Guid? ClientId { get; set; }
    public Guid? CaseId { get; set; }
}

public class RazorpayWebhookPayload
{
    public string Event { get; set; } = string.Empty;
    public RazorpayPayloadData? Payload { get; set; }
}

public class RazorpayPayloadData
{
    public RazorpaySubscription? Subscription { get; set; }
    public RazorpayPayment? Payment { get; set; }
}

public class RazorpaySubscription
{
    public string Id { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Notes { get; set; }
}

public class RazorpayPayment
{
    public RazorpayPaymentEntity? Entity { get; set; }
}

public class RazorpayPaymentEntity
{
    public string Id { get; set; } = string.Empty;
    public int Amount { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class PaymentHistoryDto
{
    public Guid Id { get; set; }
    public decimal Amount { get; set; }
    public string? PaymentMode { get; set; }
    public string? TxnReference { get; set; }
    public DateTime? PaidAt { get; set; }
}
