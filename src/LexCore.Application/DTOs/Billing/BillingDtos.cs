using System.ComponentModel.DataAnnotations;
using LexCore.Domain.Enums;

namespace LexCore.Application.DTOs.Billing;

public class CreateSubscriptionRequest
{
    [Required]
    public SubscriptionPlan Plan { get; set; }
}

public class SubscriptionDto
{
    public Guid Id { get; set; }
    public string Plan { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string? RazorpaySubscriptionId { get; set; }
}

public class CreateInvoiceRequest
{
    [Required]
    public Guid CaseId { get; set; }

    [Required]
    public Guid ClientId { get; set; }

    [Required]
    [Range(0.01, double.MaxValue)]
    public decimal Amount { get; set; }

    public string? Description { get; set; }
    public string? LineItems { get; set; }
    public DateTime? DueDate { get; set; }
}

public class InvoiceDto
{
    public Guid Id { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public Guid CaseId { get; set; }
    public string CaseTitle { get; set; } = string.Empty;
    public Guid ClientId { get; set; }
    public string ClientName { get; set; } = string.Empty;
    public string ClientEmail { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal GstAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? DueDate { get; set; }
    public string? Description { get; set; }
    public string? LineItems { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class InvoiceListDto
{
    public Guid Id { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public string CaseTitle { get; set; } = string.Empty;
    public string ClientName { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? DueDate { get; set; }
    public DateTime CreatedAt { get; set; }
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
