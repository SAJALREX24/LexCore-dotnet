using LexCore.Application.DTOs;
using LexCore.Application.DTOs.Billing;
using LexCore.Application.Interfaces;
using LexCore.Domain.Entities;
using LexCore.Domain.Enums;
using LexCore.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LexCore.API.Controllers;

[ApiController]
[Route("api/billing")]
public class BillingController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ITenantService _tenantService;
    private readonly IEmailService _emailService;
    private readonly IPdfService _pdfService;
    private readonly IAuditService _auditService;
    private readonly ILogger<BillingController> _logger;

    public BillingController(
        AppDbContext context,
        ITenantService tenantService,
        IEmailService emailService,
        IPdfService pdfService,
        IAuditService auditService,
        ILogger<BillingController> logger)
    {
        _context = context;
        _tenantService = tenantService;
        _emailService = emailService;
        _pdfService = pdfService;
        _auditService = auditService;
        _logger = logger;
    }

    [HttpPost("invoices")]
    [Authorize(Policy = "Lawyer")]
    public async Task<ActionResult<ApiResponse<InvoiceDto>>> CreateInvoice([FromBody] CreateInvoiceRequest request)
    {
        var userId = _tenantService.GetCurrentUserId();

        var caseEntity = await _context.Cases
            .Include(c => c.CaseLawyers)
            .FirstOrDefaultAsync(c =>
                c.Id == request.CaseId &&
                c.CaseLawyers.Any(cl => cl.LawyerId == userId && cl.DeletedAt == null));

        if (caseEntity == null)
            return BadRequest(ApiResponse<InvoiceDto>.ErrorResponse("Case not found", "CASE_NOT_FOUND", 400));

        // Solo lawyer — use case's stored client name/contact fields
        var resolvedClientName = caseEntity.ClientName ?? caseEntity.Title ?? "Client";
        var resolvedClientPhone = caseEntity.ClientWhatsApp ?? caseEntity.ClientPhone;

        // GST compliance: If client explicitly sends GstAmount (even 0),
        // use it — this covers GST-exempt lawyers (turnover < ₹20L).
        // If null, default to 18% with proper paise-level rounding
        // per CGST Rules Rule 34A.
        var gstAmount = request.GstAmount.HasValue
            ? Math.Round(request.GstAmount.Value, 2, MidpointRounding.AwayFromZero)
            : Math.Round(request.Amount * 0.18m, 2, MidpointRounding.AwayFromZero);
        var totalAmount = Math.Round(request.Amount + gstAmount, 2, MidpointRounding.AwayFromZero);

        // Retry loop handles concurrent invoice creation race condition.
        // If two requests generate the same number, the unique index on
        // InvoiceNumber causes DbUpdateException. We catch it and retry
        // with a fresh number (max 3 attempts).
        const int maxRetries = 3;
        Invoice invoice = null!;
        for (var attempt = 0; attempt < maxRetries; attempt++)
        {
            var invoiceNumber = await GenerateInvoiceNumber(userId);

            invoice = new Invoice
            {
                CaseId = request.CaseId,
                ClientName = resolvedClientName,
                ClientPhone = resolvedClientPhone,
                Amount = request.Amount,
                GstAmount = gstAmount,
                TotalAmount = totalAmount,
                Status = InvoiceStatus.Draft,
                DueDate = request.DueDate.HasValue
                    ? DateTime.SpecifyKind(request.DueDate.Value, DateTimeKind.Utc)
                    : DateTime.UtcNow.AddDays(30),
                InvoiceNumber = invoiceNumber,
                Description = request.Description,
                LineItems = request.LineItems,
                IsInterState = request.IsInterState
            };

            await _context.Invoices.AddAsync(invoice);

            try
            {
                await _context.SaveChangesAsync();
                break; // success
            }
            catch (DbUpdateException) when (attempt < maxRetries - 1)
            {
                // Unique constraint violation on InvoiceNumber — retry
                _context.Entry(invoice).State = EntityState.Detached;
                continue;
            }
        }

        await _auditService.LogAsync("INVOICE_CREATED", "Invoice", invoice.Id,
            ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());

        return CreatedAtAction(nameof(GetInvoice), new { id = invoice.Id },
            ApiResponse<InvoiceDto>.SuccessResponse(MapToDto(invoice, caseEntity.Title), "Invoice created successfully"));
    }

    [HttpGet("invoices")]
    [Authorize(Policy = "Lawyer")]
    public async Task<ActionResult<PagedResponse<InvoiceListDto>>> GetInvoices([FromQuery] InvoiceFilterRequest filter)
    {
        var userId = _tenantService.GetCurrentUserId();

        var query = _context.Invoices
            .Include(i => i.Case)
                .ThenInclude(c => c.CaseLawyers)
            .Where(i =>
                i.Case != null &&
                i.Case.CaseLawyers.Any(cl => cl.LawyerId == userId && cl.DeletedAt == null));

        if (filter.Status.HasValue)
            query = query.Where(i => i.Status == filter.Status.Value);

        if (filter.CaseId.HasValue)
            query = query.Where(i => i.CaseId == filter.CaseId.Value);

        var totalCount = await query.CountAsync();

        var invoices = await query
            .OrderByDescending(i => i.CreatedAt)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(i => new InvoiceListDto
            {
                Id = i.Id,
                InvoiceNumber = i.InvoiceNumber,
                CaseId = i.CaseId,
                CaseTitle = i.Case!.Title,
                ClientName = i.ClientName ?? "",
                Amount = i.Amount,
                GstAmount = i.GstAmount,
                TotalAmount = i.TotalAmount,
                PaidAmount = i.PaidAmount,
                OutstandingAmount = i.TotalAmount - i.PaidAmount,
                Status = i.Status.ToString(),
                DueDate = i.DueDate,
                CreatedAt = i.CreatedAt,
                PaymentMode = i.PaymentMode,
                TxnReference = i.TxnReference,
                PaymentDate = i.PaymentDate,
                IsInterState = i.IsInterState,
            })
            .ToListAsync();

        return Ok(new PagedResponse<InvoiceListDto>
        {
            Data = invoices,
            Page = filter.Page,
            PageSize = filter.PageSize,
            TotalCount = totalCount
        });
    }

    [HttpGet("invoices/{id:guid}")]
    [Authorize(Policy = "Lawyer")]
    public async Task<ActionResult<ApiResponse<InvoiceDto>>> GetInvoice(Guid id)
    {
        var userId = _tenantService.GetCurrentUserId();

        var invoice = await _context.Invoices
            .Include(i => i.Case)
                .ThenInclude(c => c.CaseLawyers)
            .Include(i => i.Payments)
            .FirstOrDefaultAsync(i =>
                i.Id == id &&
                i.Case != null &&
                i.Case.CaseLawyers.Any(cl => cl.LawyerId == userId && cl.DeletedAt == null));

        if (invoice == null)
            return NotFound(ApiResponse<InvoiceDto>.ErrorResponse("Invoice not found", "NOT_FOUND", 404));

        var dto = MapToDto(invoice, invoice.Case!.Title);
        dto.PaymentHistory = invoice.Payments
            .OrderBy(p => p.PaidAt)
            .Select(p => new PaymentHistoryDto
            {
                Id = p.Id,
                Amount = p.Amount,
                PaymentMode = p.PaymentMode,
                TxnReference = p.ReferenceNumber,
                PaidAt = p.PaidAt,
            })
            .ToList();

        return Ok(ApiResponse<InvoiceDto>.SuccessResponse(dto));
    }

    [HttpPatch("invoices/{id:guid}")]
    [Authorize(Policy = "Lawyer")]
    public async Task<ActionResult<ApiResponse<InvoiceDto>>> UpdateInvoice(
        Guid id, [FromBody] UpdateInvoiceRequest request)
    {
        var userId = _tenantService.GetCurrentUserId();

        var invoice = await _context.Invoices
            .Include(i => i.Case)
                .ThenInclude(c => c.CaseLawyers)
            .FirstOrDefaultAsync(i =>
                i.Id == id &&
                i.Case != null &&
                i.Case.CaseLawyers.Any(cl => cl.LawyerId == userId && cl.DeletedAt == null));

        if (invoice == null)
            return NotFound(ApiResponse<InvoiceDto>.ErrorResponse("Invoice not found", "NOT_FOUND", 404));

        if (invoice.Status != InvoiceStatus.Draft)
            return BadRequest(ApiResponse<InvoiceDto>.ErrorResponse(
                "Only Draft invoices can be edited", "INVALID_STATUS", 400));

        if (request.Amount.HasValue && request.Amount.Value > 0)
        {
            invoice.Amount = request.Amount.Value;
            var gst = request.GstAmount.HasValue
                ? request.GstAmount.Value
                : Math.Round(invoice.Amount * 0.18m, 2, MidpointRounding.AwayFromZero);
            invoice.GstAmount = gst;
            invoice.TotalAmount = Math.Round(invoice.Amount + gst, 2, MidpointRounding.AwayFromZero);
        }

        if (request.Description != null)
            invoice.Description = request.Description;

        if (request.DueDate.HasValue)
            invoice.DueDate = DateTime.SpecifyKind(request.DueDate.Value, DateTimeKind.Utc);

        if (request.LineItems != null)
            invoice.LineItems = request.LineItems;

        if (request.IsInterState.HasValue)
        {
            invoice.IsInterState = request.IsInterState.Value;
            if (request.Amount.HasValue)
            {
                var gst = request.GstAmount.HasValue
                    ? request.GstAmount.Value
                    : Math.Round(invoice.Amount * 0.18m, 2, MidpointRounding.AwayFromZero);
                invoice.GstAmount = gst;
                invoice.TotalAmount = Math.Round(invoice.Amount + gst, 2, MidpointRounding.AwayFromZero);
            }
        }

        invoice.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        await _auditService.LogAsync("INVOICE_UPDATED", "Invoice", id,
            ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());

        return Ok(ApiResponse<InvoiceDto>.SuccessResponse(
            MapToDto(invoice, invoice.Case!.Title), "Invoice updated successfully"));
    }

    [HttpPatch("invoices/{id:guid}/send")]
    [Authorize(Policy = "Lawyer")]
    public async Task<ActionResult<ApiResponse<InvoiceDto>>> SendInvoice(Guid id)
    {
        var userId = _tenantService.GetCurrentUserId();

        var invoice = await _context.Invoices
            .Include(i => i.Case)
                .ThenInclude(c => c.CaseLawyers)
            .FirstOrDefaultAsync(i =>
                i.Id == id &&
                i.Case != null &&
                i.Case.CaseLawyers.Any(cl => cl.LawyerId == userId && cl.DeletedAt == null));

        if (invoice == null)
            return NotFound(ApiResponse<InvoiceDto>.ErrorResponse("Invoice not found", "NOT_FOUND", 404));

        var lawyerForPdf = await _context.Users.FindAsync(userId);
        var clientForPdf = new User
        {
            Name = invoice.ClientName ?? "Client",
            Email = invoice.ClientEmail ?? "",
            Phone = invoice.ClientPhone
        };
        var pdfBytes = _pdfService.GenerateInvoicePdf(invoice, clientForPdf, lawyerForPdf);

        var emailTo = invoice.ClientEmail;
        var emailName = invoice.ClientName ?? "Client";
        if (!string.IsNullOrEmpty(emailTo))
        {
            await _emailService.SendInvoiceEmailAsync(
                emailTo,
                emailName,
                invoice.InvoiceNumber,
                invoice.TotalAmount,
                invoice.DueDate ?? DateTime.UtcNow.AddDays(30),
                pdfBytes);
        }

        invoice.Status = InvoiceStatus.Sent;
        await _context.SaveChangesAsync();
        await _auditService.LogAsync("INVOICE_SENT", "Invoice", id,
            ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());

        var message = !string.IsNullOrEmpty(emailTo)
            ? "Invoice sent successfully"
            : "Invoice marked as Sent — no client email on file, email was not sent";

        return Ok(ApiResponse<InvoiceDto>.SuccessResponse(
            MapToDto(invoice, invoice.Case!.Title), message));
    }

    [HttpPatch("invoices/{id:guid}/mark-paid")]
    [Authorize(Policy = "Lawyer")]
    public async Task<ActionResult<ApiResponse<InvoiceDto>>> MarkInvoicePaid(
        Guid id, [FromBody] MarkPaidRequest request)
    {
        var userId = _tenantService.GetCurrentUserId();

        var invoice = await _context.Invoices
            .Include(i => i.Case)
                .ThenInclude(c => c.CaseLawyers)
            .FirstOrDefaultAsync(i =>
                i.Id == id &&
                i.Case != null &&
                i.Case.CaseLawyers.Any(cl => cl.LawyerId == userId && cl.DeletedAt == null));

        if (invoice == null)
            return NotFound(ApiResponse<InvoiceDto>.ErrorResponse("Invoice not found", "NOT_FOUND", 404));

        if (invoice.Status == InvoiceStatus.Cancelled)
            return BadRequest(ApiResponse<InvoiceDto>.ErrorResponse(
                "Cannot record payment on a cancelled invoice", "INVALID_STATUS", 400));

        if (invoice.Status == InvoiceStatus.Paid)
            return BadRequest(ApiResponse<InvoiceDto>.ErrorResponse(
                "This invoice is already fully paid", "ALREADY_PAID", 400));

        var newPaymentAmount = request.Amount ?? invoice.TotalAmount;
        if (newPaymentAmount <= 0)
            return BadRequest(ApiResponse<InvoiceDto>.ErrorResponse(
                "Payment amount must be greater than zero", "INVALID_AMOUNT", 400));

        var totalPaidAfter = invoice.PaidAmount + newPaymentAmount;
        if (totalPaidAfter > invoice.TotalAmount)
            totalPaidAfter = invoice.TotalAmount;

        var actualNewPayment = totalPaidAfter - invoice.PaidAmount;

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var paymentRecord = new Payment
            {
                Id = Guid.NewGuid(),
                InvoiceId = invoice.Id,
                Amount = actualNewPayment,
                Status = "Paid",
                PaidAt = request.PaymentDate.HasValue
                    ? DateTime.SpecifyKind(request.PaymentDate.Value, DateTimeKind.Utc)
                    : DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            await _context.Payments.AddAsync(paymentRecord);

            invoice.PaidAmount = totalPaidAfter;
            invoice.PaymentMode = request.PaymentMode;
            invoice.TxnReference = request.TxnReference;
            invoice.PaymentDate = request.PaymentDate.HasValue
                ? DateTime.SpecifyKind(request.PaymentDate.Value, DateTimeKind.Utc)
                : DateTime.UtcNow;
            invoice.UpdatedAt = DateTime.UtcNow;
            invoice.Status = totalPaidAfter >= invoice.TotalAmount
                ? InvoiceStatus.Paid
                : InvoiceStatus.PartiallyPaid;

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "MarkInvoicePaid failed for InvoiceId {InvoiceId}", id);
            return StatusCode(500, ApiResponse<InvoiceDto>.ErrorResponse(
                "Failed to record payment", "PAYMENT_FAILED", 500));
        }

        await _auditService.LogAsync("INVOICE_PAYMENT_RECORDED", "Invoice", invoice.Id,
            ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());

        var outstandingAmount = invoice.TotalAmount - invoice.PaidAmount;
        var statusMessage = invoice.Status == InvoiceStatus.Paid
            ? "Payment recorded — Invoice fully paid"
            : $"Payment recorded — ₹{outstandingAmount:N2} still outstanding";

        return Ok(ApiResponse<InvoiceDto>.SuccessResponse(
            MapToDto(invoice, invoice.Case!.Title), statusMessage));
    }

    [HttpPatch("invoices/{id:guid}/cancel")]
    [Authorize(Policy = "Lawyer")]
    public async Task<ActionResult<ApiResponse<object>>> CancelInvoice(Guid id)
    {
        var userId = _tenantService.GetCurrentUserId();

        var invoice = await _context.Invoices
            .Include(i => i.Case)
                .ThenInclude(c => c.CaseLawyers)
            .FirstOrDefaultAsync(i =>
                i.Id == id &&
                i.Case != null &&
                i.Case.CaseLawyers.Any(cl => cl.LawyerId == userId && cl.DeletedAt == null));

        if (invoice == null)
            return NotFound(ApiResponse<object>.ErrorResponse("Invoice not found", "NOT_FOUND", 404));

        if (invoice.Status == InvoiceStatus.Paid)
            return BadRequest(ApiResponse<object>.ErrorResponse(
                "Cannot cancel a paid invoice", "INVALID_STATUS", 400));

        invoice.Status = InvoiceStatus.Cancelled;
        invoice.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        await _auditService.LogAsync("INVOICE_CANCELLED", "Invoice", invoice.Id,
            ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());

        return Ok(ApiResponse<object>.SuccessResponse(null!, "Invoice cancelled successfully"));
    }

    [HttpGet("invoices/{id:guid}/pdf")]
    [Authorize(Policy = "Lawyer")]
    public async Task<IActionResult> GetInvoicePdf(Guid id)
    {
        var userId = _tenantService.GetCurrentUserId();

        var invoice = await _context.Invoices
            .Include(i => i.Case)
                .ThenInclude(c => c.CaseLawyers)
            .FirstOrDefaultAsync(i =>
                i.Id == id &&
                i.Case != null &&
                i.Case.CaseLawyers.Any(cl => cl.LawyerId == userId && cl.DeletedAt == null));

        if (invoice == null)
            return NotFound(new { success = false, message = "Invoice not found" });

        var lawyerForPdf = await _context.Users.FindAsync(userId);
        var clientForPdf = new User
        {
            Name = invoice.ClientName ?? "Client",
            Email = invoice.ClientEmail ?? "",
            Phone = invoice.ClientPhone
        };
        var pdfBytes = _pdfService.GenerateInvoicePdf(invoice, clientForPdf, lawyerForPdf);

        return File(pdfBytes, "application/pdf", $"Invoice_{invoice.InvoiceNumber}.pdf");
    }

    [HttpGet("advance-summary")]
    [Authorize(Policy = "Lawyer")]
    public async Task<ActionResult<ApiResponse<object>>> GetAdvanceSummary()
    {
        var userId = _tenantService.GetCurrentUserId();

        var query = _context.Payments
            .Include(p => p.Case)
                .ThenInclude(c => c!.CaseLawyers)
            .Where(p =>
                p.IsAdvancePayment &&
                p.Status == "Completed" &&
                p.DeletedAt == null &&
                p.Case != null &&
                p.Case.CaseLawyers.Any(cl => cl.LawyerId == userId && cl.DeletedAt == null));

        var totalAdvanceCollected = await query.SumAsync(p => (decimal?)p.Amount) ?? 0;
        var advanceCount = await query.CountAsync();

        return Ok(ApiResponse<object>.SuccessResponse(new
        {
            totalAdvanceCollected,
            advanceCount
        }, "Advance summary fetched"));
    }

    // ── HELPERS ──────────────────────────────────────────────────────────────

    private async Task<string> GenerateInvoiceNumber(Guid userId)
    {
        // Indian Financial Year: April 1 to March 31.
        // FY 2026-27 means April 2026 through March 2027.
        var now = DateTime.UtcNow;
        var fyStart = now.Month >= 4 ? now.Year : now.Year - 1;
        var fyEnd = fyStart + 1;
        var fyLabel = $"{fyStart % 100:D2}{fyEnd % 100:D2}"; // e.g. "2627"

        var existing = await _context.Invoices
            .IgnoreQueryFilters()
            .Where(i =>
                i.CreatedAt >= new DateTime(fyStart, 4, 1, 0, 0, 0, DateTimeKind.Utc) &&
                i.CreatedAt < new DateTime(fyEnd, 4, 1, 0, 0, 0, DateTimeKind.Utc) &&
                i.Case != null &&
                i.Case.CaseLawyers.Any(cl => cl.LawyerId == userId && cl.DeletedAt == null))
            .Select(i => i.InvoiceNumber)
            .ToListAsync();

        var maxSeq = 0;
        foreach (var num in existing)
        {
            var parts = num?.Split('-');
            if (parts?.Length == 3 && int.TryParse(parts[2], out var seq))
                maxSeq = Math.Max(maxSeq, seq);
        }

        return $"INV-{fyLabel}-{(maxSeq + 1):D5}";
    }

    private static InvoiceDto MapToDto(Invoice invoice, string? caseTitle) => new InvoiceDto
    {
        Id = invoice.Id,
        InvoiceNumber = invoice.InvoiceNumber,
        CaseId = invoice.CaseId,
        CaseTitle = caseTitle,
        ClientName = invoice.ClientName ?? "",
        ClientEmail = invoice.ClientEmail,
        Amount = invoice.Amount,
        GstAmount = invoice.GstAmount,
        TotalAmount = invoice.TotalAmount,
        Status = invoice.Status.ToString(),
        DueDate = invoice.DueDate,
        Description = invoice.Description,
        LineItems = invoice.LineItems,
        CreatedAt = invoice.CreatedAt,
        UpdatedAt = invoice.UpdatedAt,
        IsInterState = invoice.IsInterState,
        PaidAmount = invoice.PaidAmount,
        OutstandingAmount = invoice.TotalAmount - invoice.PaidAmount,
        PaymentMode = invoice.PaymentMode,
        TxnReference = invoice.TxnReference,
        PaymentDate = invoice.PaymentDate,
    };
}
