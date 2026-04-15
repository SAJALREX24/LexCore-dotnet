using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
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
    private readonly IRazorpayService _razorpayService;
    private readonly IEmailService _emailService;
    private readonly IPdfService _pdfService;
    private readonly IAuditService _auditService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<BillingController> _logger;

    public BillingController(
        AppDbContext context,
        ITenantService tenantService,
        IRazorpayService razorpayService,
        IEmailService emailService,
        IPdfService pdfService,
        IAuditService auditService,
        IConfiguration configuration,
        ILogger<BillingController> logger)
    {
        _context = context;
        _tenantService = tenantService;
        _razorpayService = razorpayService;
        _emailService = emailService;
        _pdfService = pdfService;
        _auditService = auditService;
        _configuration = configuration;
        _logger = logger;
    }

    [HttpPost("subscribe")]
    [Authorize(Policy = "FirmAdmin")]
    public async Task<ActionResult<ApiResponse<object>>> CreateSubscription([FromBody] CreateSubscriptionRequest request)
    {
        var firmId = _tenantService.GetCurrentFirmId();
        var userId = _tenantService.GetCurrentUserId();

        var firm = await _context.Firms.FindAsync(firmId);
        var user = await _context.Users.FindAsync(userId);

        if (firm == null || user == null)
        {
            return NotFound(ApiResponse<object>.ErrorResponse("Firm or user not found", "NOT_FOUND", 404));
        }

        var planId = request.Plan switch
        {
            SubscriptionPlan.Basic => _configuration["Razorpay:Plans:Basic"],
            SubscriptionPlan.Pro => _configuration["Razorpay:Plans:Pro"],
            SubscriptionPlan.Enterprise => _configuration["Razorpay:Plans:Enterprise"],
            _ => null
        };

        if (string.IsNullOrEmpty(planId))
        {
            return BadRequest(ApiResponse<object>.ErrorResponse("Invalid subscription plan", "INVALID_PLAN", 400));
        }

        var (subscriptionId, paymentLink) = await _razorpayService.CreateSubscriptionAsync(firmId!.Value, planId, user.Email, user.Name);

        var subscription = new Subscription
        {
            FirmId = firmId,
            Plan = request.Plan,
            Status = SubscriptionStatus.Active,
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddMonths(1),
            RazorpaySubscriptionId = subscriptionId
        };

        await _context.Subscriptions.AddAsync(subscription);

        firm.Plan = request.Plan;
        firm.SubscriptionStatus = SubscriptionStatus.Active;

        await _context.SaveChangesAsync();
        await _auditService.LogAsync("SUBSCRIPTION_CREATED", "Subscription", subscription.Id, ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());

        return Ok(ApiResponse<object>.SuccessResponse(new { subscriptionId, paymentLink }, "Subscription created successfully"));
    }

    [HttpPost("webhook")]
    [AllowAnonymous]
    public async Task<IActionResult> HandleWebhook()
    {
        using var reader = new StreamReader(Request.Body);
        var payload = await reader.ReadToEndAsync();

        var signature = Request.Headers["X-Razorpay-Signature"].FirstOrDefault();
        var secret = _configuration["Razorpay:WebhookSecret"];

        if (string.IsNullOrEmpty(signature) || string.IsNullOrEmpty(secret))
        {
            return BadRequest(new { success = false, message = "Invalid signature" });
        }

        if (!_razorpayService.VerifyWebhookSignature(payload, signature, secret))
        {
            _logger.LogWarning("Invalid Razorpay webhook signature");
            return BadRequest(new { success = false, message = "Invalid signature" });
        }

        var webhookData = JsonSerializer.Deserialize<RazorpayWebhookPayload>(payload, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (webhookData == null)
        {
            return BadRequest(new { success = false, message = "Invalid payload" });
        }

        _logger.LogInformation("Received Razorpay webhook: {Event}", webhookData.Event);

        switch (webhookData.Event)
        {
            case "subscription.activated":
            case "subscription.charged":
                await HandleSubscriptionActivated(webhookData);
                break;
            case "subscription.cancelled":
            case "subscription.expired":
                await HandleSubscriptionCancelled(webhookData);
                break;
            case "payment.captured":
                await HandlePaymentCaptured(webhookData);
                break;
        }

        return Ok(new { success = true });
    }

    private async Task HandleSubscriptionActivated(RazorpayWebhookPayload payload)
    {
        var subscriptionId = payload.Payload?.Subscription?.Id;
        if (string.IsNullOrEmpty(subscriptionId)) return;

        var subscription = await _context.Subscriptions
            .Include(s => s.Firm)
            .FirstOrDefaultAsync(s => s.RazorpaySubscriptionId == subscriptionId);

        if (subscription != null)
        {
            subscription.Status = SubscriptionStatus.Active;
            subscription.Firm!.SubscriptionStatus = SubscriptionStatus.Active;
            await _context.SaveChangesAsync();
        }
    }

    private async Task HandleSubscriptionCancelled(RazorpayWebhookPayload payload)
    {
        var subscriptionId = payload.Payload?.Subscription?.Id;
        if (string.IsNullOrEmpty(subscriptionId)) return;

        var subscription = await _context.Subscriptions
            .Include(s => s.Firm)
            .FirstOrDefaultAsync(s => s.RazorpaySubscriptionId == subscriptionId);

        if (subscription != null)
        {
            subscription.Status = SubscriptionStatus.Cancelled;
            subscription.Firm!.SubscriptionStatus = SubscriptionStatus.Cancelled;
            await _context.SaveChangesAsync();
        }
    }

    private async Task HandlePaymentCaptured(RazorpayWebhookPayload payload)
    {
        _logger.LogInformation("Payment captured: {PaymentId}", payload.Payload?.Payment?.Entity?.Id);
    }

    [HttpGet("subscription")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<SubscriptionDto>>> GetSubscription()
    {
        var firmId = _tenantService.GetCurrentFirmId();

        var subscription = await _context.Subscriptions
            .Where(s => s.FirmId == firmId)
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync();

        if (subscription == null)
        {
            return NotFound(ApiResponse<SubscriptionDto>.ErrorResponse("No subscription found", "NOT_FOUND", 404));
        }

        return Ok(ApiResponse<SubscriptionDto>.SuccessResponse(new SubscriptionDto
        {
            Id = subscription.Id,
            Plan = subscription.Plan.ToString(),
            Status = subscription.Status.ToString(),
            StartDate = subscription.StartDate,
            EndDate = subscription.EndDate,
            RazorpaySubscriptionId = subscription.RazorpaySubscriptionId
        }));
    }

    [HttpPost("invoices")]
    [Authorize(Policy = "Lawyer")]
    public async Task<ActionResult<ApiResponse<InvoiceDto>>> CreateInvoice([FromBody] CreateInvoiceRequest request)
    {
        var firmId = _tenantService.GetCurrentFirmId();
        var userId = _tenantService.GetCurrentUserId();

        // Security: for solo lawyers verify they are assigned to this case
        // via CaseLawyers — prevents creating invoices on other lawyers' cases
        var caseEntity = await _context.Cases
            .Include(c => c.CaseLawyers)
            .FirstOrDefaultAsync(c =>
                c.Id == request.CaseId &&
                (firmId.HasValue
                    ? c.FirmId == firmId.Value
                    : c.FirmId == null &&
                      c.CaseLawyers.Any(cl =>
                          cl.LawyerId == userId &&
                          cl.DeletedAt == null)));
        if (caseEntity == null)
        {
            return BadRequest(ApiResponse<InvoiceDto>.ErrorResponse("Case not found", "CASE_NOT_FOUND", 400));
        }

        // Client lookup — optional. Use registered client if provided,
        // otherwise fall back to case's ClientName text field.
        User? client = null;
        string resolvedClientName;
        string? resolvedClientEmail = null;
        string? resolvedClientPhone = null;

        if (request.ClientId.HasValue)
        {
            client = await _context.Users.FirstOrDefaultAsync(u =>
                u.Id == request.ClientId.Value &&
                (firmId.HasValue ? u.FirmId == firmId.Value : u.FirmId == null) &&
                u.Role == UserRole.Client);
            if (client == null)
                return BadRequest(ApiResponse<InvoiceDto>.ErrorResponse(
                    "Client not found", "CLIENT_NOT_FOUND", 400));
            resolvedClientName = client.Name;
            resolvedClientEmail = client.Email;
            resolvedClientPhone = client.Phone;
        }
        else
        {
            // Solo lawyer — use case's client name text field
            resolvedClientName = caseEntity.ClientName
                ?? caseEntity.Title
                ?? "Client";
            resolvedClientPhone = caseEntity.ClientWhatsApp
                ?? caseEntity.ClientPhone;
        }

        var invoiceNumber = await GenerateInvoiceNumber(firmId);
        var gstAmount = request.GstAmount.HasValue
            ? request.GstAmount.Value
            : request.Amount * 0.18m;
        var totalAmount = request.Amount + gstAmount;

        var invoice = new Invoice
        {
            FirmId = firmId,
            CaseId = request.CaseId,
            ClientId = request.ClientId,
            ClientName = resolvedClientName,
            ClientEmail = resolvedClientEmail,
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
        await _context.SaveChangesAsync();
        await _auditService.LogAsync("INVOICE_CREATED", "Invoice", invoice.Id, ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());

        return CreatedAtAction(nameof(GetInvoice), new { id = invoice.Id }, ApiResponse<InvoiceDto>.SuccessResponse(new InvoiceDto
        {
            Id = invoice.Id,
            InvoiceNumber = invoice.InvoiceNumber,
            CaseId = invoice.CaseId,
            CaseTitle = caseEntity.Title,
            ClientId = invoice.ClientId,
            ClientName = invoice.ClientName ?? client?.Name ?? "",
            ClientEmail = invoice.ClientEmail ?? client?.Email,
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
        }, "Invoice created successfully"));
    }

    [HttpGet("invoices")]
    [Authorize]
    public async Task<ActionResult<PagedResponse<InvoiceListDto>>> GetInvoices([FromQuery] InvoiceFilterRequest filter)
    {
        var firmId = _tenantService.GetCurrentFirmId();
        var userId = _tenantService.GetCurrentUserId();
        var role = _tenantService.GetCurrentUserRole();

        var query = _context.Invoices
            .Include(i => i.Case)
                .ThenInclude(c => c.CaseLawyers)
            .Include(i => i.Client)
            .Where(i => firmId.HasValue
                ? i.FirmId == firmId.Value
                : i.FirmId == null &&
                  i.Case != null &&
                  i.Case.CaseLawyers.Any(cl =>
                      cl.LawyerId == userId &&
                      cl.DeletedAt == null));

        if (role == UserRole.Client.ToString())
        {
            query = query.Where(i => i.ClientId == userId);
        }

        if (filter.Status.HasValue)
            query = query.Where(i => i.Status == filter.Status.Value);

        if (filter.ClientId.HasValue)
            query = query.Where(i => i.ClientId == filter.ClientId.Value);

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
                ClientName = i.ClientName ?? i.Client!.Name ?? "",
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
    [Authorize]
    public async Task<ActionResult<ApiResponse<InvoiceDto>>> GetInvoice(Guid id)
    {
        var firmId = _tenantService.GetCurrentFirmId();
        var userId = _tenantService.GetCurrentUserId();
        var role = _tenantService.GetCurrentUserRole();

        var invoice = await _context.Invoices
            .Include(i => i.Case)
                .ThenInclude(c => c.CaseLawyers)
            .Include(i => i.Client)
            .Include(i => i.Payments)
            .FirstOrDefaultAsync(i => i.Id == id &&
                (firmId.HasValue
                    ? i.FirmId == firmId.Value
                    : i.FirmId == null &&
                      i.Case != null &&
                      i.Case.CaseLawyers.Any(cl =>
                          cl.LawyerId == userId &&
                          cl.DeletedAt == null)));

        if (invoice == null)
        {
            return NotFound(ApiResponse<InvoiceDto>.ErrorResponse("Invoice not found", "NOT_FOUND", 404));
        }

        if (role == UserRole.Client.ToString() && invoice.ClientId != userId)
        {
            return Forbid();
        }

        return Ok(ApiResponse<InvoiceDto>.SuccessResponse(new InvoiceDto
        {
            Id = invoice.Id,
            InvoiceNumber = invoice.InvoiceNumber,
            CaseId = invoice.CaseId,
            CaseTitle = invoice.Case!.Title,
            ClientId = invoice.ClientId,
            ClientName = invoice.ClientName ?? invoice.Client?.Name ?? "",
            ClientEmail = invoice.ClientEmail ?? invoice.Client?.Email,
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
            PaymentHistory = invoice.Payments
                .OrderBy(p => p.PaidAt)
                .Select(p => new PaymentHistoryDto
                {
                    Id = p.Id,
                    Amount = p.Amount,
                    PaymentMode = invoice.PaymentMode,
                    TxnReference = invoice.TxnReference,
                    PaidAt = p.PaidAt,
                })
                .ToList(),
        }));
    }

    // PATCH /api/billing/invoices/{id}
    // Edits amount, description, due date on Draft invoices only
    [HttpPatch("invoices/{id:guid}")]
    [Authorize(Policy = "Lawyer")]
    public async Task<ActionResult<ApiResponse<InvoiceDto>>> UpdateInvoice(
        Guid id, [FromBody] UpdateInvoiceRequest request)
    {
        var firmId = _tenantService.GetCurrentFirmId();
        var userId = _tenantService.GetCurrentUserId();

        var invoice = await _context.Invoices
            .Include(i => i.Case)
                .ThenInclude(c => c.CaseLawyers)
            .Include(i => i.Client)
            .FirstOrDefaultAsync(i => i.Id == id &&
                (firmId.HasValue
                    ? i.FirmId == firmId.Value
                    : i.FirmId == null &&
                      i.Case != null &&
                      i.Case.CaseLawyers.Any(cl =>
                          cl.LawyerId == userId &&
                          cl.DeletedAt == null)));

        if (invoice == null)
            return NotFound(ApiResponse<InvoiceDto>.ErrorResponse(
                "Invoice not found", "NOT_FOUND", 404));

        // Only Draft invoices can be edited
        if (invoice.Status != InvoiceStatus.Draft)
            return BadRequest(ApiResponse<InvoiceDto>.ErrorResponse(
                "Only Draft invoices can be edited", "INVALID_STATUS", 400));

        if (request.Amount.HasValue && request.Amount.Value > 0)
        {
            invoice.Amount = request.Amount.Value;
            var gst = request.GstAmount.HasValue
                ? request.GstAmount.Value
                : invoice.Amount * 0.18m;
            invoice.GstAmount = gst;
            invoice.TotalAmount = invoice.Amount + gst;
        }

        if (request.Description != null)
            invoice.Description = request.Description;

        if (request.DueDate.HasValue)
            invoice.DueDate = DateTime.SpecifyKind(
                request.DueDate.Value, DateTimeKind.Utc);

        if (request.LineItems != null)
            invoice.LineItems = request.LineItems;

        if (request.IsInterState.HasValue)
        {
            invoice.IsInterState = request.IsInterState.Value;
            if (request.Amount.HasValue)
            {
                // Recalculate GST if interState changed alongside amount
                var gst = request.GstAmount.HasValue
                    ? request.GstAmount.Value
                    : invoice.Amount * 0.18m;
                invoice.GstAmount = gst;
                invoice.TotalAmount = invoice.Amount + gst;
            }
        }

        invoice.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        await _auditService.LogAsync("INVOICE_UPDATED", "Invoice", id,
            ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());

        return Ok(ApiResponse<InvoiceDto>.SuccessResponse(new InvoiceDto
        {
            Id = invoice.Id,
            InvoiceNumber = invoice.InvoiceNumber,
            CaseId = invoice.CaseId,
            CaseTitle = invoice.Case!.Title,
            ClientId = invoice.ClientId,
            ClientName = invoice.ClientName ?? invoice.Client?.Name ?? "",
            ClientEmail = invoice.ClientEmail ?? invoice.Client?.Email,
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
        }, "Invoice updated successfully"));
    }

    [HttpPatch("invoices/{id:guid}/send")]
    [Authorize(Policy = "Lawyer")]
    public async Task<ActionResult<ApiResponse<InvoiceDto>>> SendInvoice(Guid id)
    {
        var firmId = _tenantService.GetCurrentFirmId();
        var userId = _tenantService.GetCurrentUserId();

        var invoice = await _context.Invoices
            .Include(i => i.Case)
                .ThenInclude(c => c.CaseLawyers)
            .Include(i => i.Client)
            .Include(i => i.Firm)
            .FirstOrDefaultAsync(i => i.Id == id &&
                (firmId.HasValue
                    ? i.FirmId == firmId.Value
                    : i.FirmId == null &&
                      i.Case != null &&
                      i.Case.CaseLawyers.Any(cl =>
                          cl.LawyerId == userId &&
                          cl.DeletedAt == null)));

        if (invoice == null)
        {
            return NotFound(ApiResponse<InvoiceDto>.ErrorResponse("Invoice not found", "NOT_FOUND", 404));
        }

        User? lawyerForPdf = null;
        if (invoice.Firm == null)
        {
            lawyerForPdf = await _context.Users
                .FirstOrDefaultAsync(u => u.Id == userId);
        }
        var firmForPdf = invoice.Firm ?? new Firm
        {
            Name = lawyerForPdf?.Name ?? "LexCore",
            GstNumber = null,
            Address = lawyerForPdf != null
                ? string.Join(", ", new[]
                {
                    lawyerForPdf.City,
                    lawyerForPdf.State
                }.Where(s => !string.IsNullOrEmpty(s)))
                : null
        };
        // Build client user for PDF — use registered client
        // or create a minimal User from stored name/email
        var clientForPdf = invoice.Client ?? new User
        {
            Name = invoice.ClientName ?? "Client",
            Email = invoice.ClientEmail ?? "",
            Phone = invoice.ClientPhone
        };
        var pdfBytes = _pdfService.GenerateInvoicePdf(
            invoice, firmForPdf, clientForPdf, lawyerForPdf);

        var emailTo = invoice.ClientEmail ?? invoice.Client?.Email;
        var emailName = invoice.ClientName ?? invoice.Client?.Name ?? "Client";
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
        await _auditService.LogAsync("INVOICE_SENT", "Invoice", id, ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());

        var emailWasSent = !string.IsNullOrEmpty(emailTo);
        var message = emailWasSent
            ? "Invoice sent successfully"
            : "Invoice marked as Sent — no client email on file, email was not sent";

        return Ok(ApiResponse<InvoiceDto>.SuccessResponse(new InvoiceDto
        {
            Id = invoice.Id,
            InvoiceNumber = invoice.InvoiceNumber,
            CaseId = invoice.CaseId,
            CaseTitle = invoice.Case!.Title,
            ClientId = invoice.ClientId,
            ClientName = invoice.ClientName ?? invoice.Client?.Name ?? "",
            ClientEmail = invoice.ClientEmail ?? invoice.Client?.Email,
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
        }, message));
    }

    [HttpPatch("invoices/{id:guid}/mark-paid")]
    [Authorize(Policy = "Lawyer")]
    public async Task<ActionResult<ApiResponse<InvoiceDto>>> MarkInvoicePaid(
        Guid id, [FromBody] MarkPaidRequest request)
    {
        var firmId = _tenantService.GetCurrentFirmId();
        var userId = _tenantService.GetCurrentUserId();

        // Security: verify ownership before any modification
        var invoice = await _context.Invoices
            .Include(i => i.Case)
                .ThenInclude(c => c.CaseLawyers)
            .Include(i => i.Client)
            .FirstOrDefaultAsync(i => i.Id == id &&
                (firmId.HasValue
                    ? i.FirmId == firmId.Value
                    : i.FirmId == null &&
                      i.Case != null &&
                      i.Case.CaseLawyers.Any(cl =>
                          cl.LawyerId == userId &&
                          cl.DeletedAt == null)));

        if (invoice == null)
            return NotFound(ApiResponse<InvoiceDto>.ErrorResponse(
                "Invoice not found", "NOT_FOUND", 404));

        if (invoice.Status == InvoiceStatus.Cancelled)
            return BadRequest(ApiResponse<InvoiceDto>.ErrorResponse(
                "Cannot record payment on a cancelled invoice",
                "INVALID_STATUS", 400));

        if (invoice.Status == InvoiceStatus.Paid)
            return BadRequest(ApiResponse<InvoiceDto>.ErrorResponse(
                "This invoice is already fully paid",
                "ALREADY_PAID", 400));

        // Validate payment amount
        var newPaymentAmount = request.Amount ?? invoice.TotalAmount;
        if (newPaymentAmount <= 0)
            return BadRequest(ApiResponse<InvoiceDto>.ErrorResponse(
                "Payment amount must be greater than zero",
                "INVALID_AMOUNT", 400));

        // Accumulate — do not replace prior payments
        var totalPaidAfter = invoice.PaidAmount + newPaymentAmount;

        // Clamp to total — cannot overpay
        if (totalPaidAfter > invoice.TotalAmount)
            totalPaidAfter = invoice.TotalAmount;

        var actualNewPayment = totalPaidAfter - invoice.PaidAmount;

        // Use transaction for atomicity — payment record + invoice update together
        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            // Record individual payment in Payment table for full audit trail
            var paymentRecord = new Payment
            {
                Id = Guid.NewGuid(),
                InvoiceId = invoice.Id,
                FirmId = invoice.FirmId,
                Amount = actualNewPayment,
                Status = "Paid",
                PaidAt = request.PaymentDate.HasValue
                    ? DateTime.SpecifyKind(request.PaymentDate.Value, DateTimeKind.Utc)
                    : DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            await _context.Payments.AddAsync(paymentRecord);

            // Update invoice fields
            invoice.PaidAmount = totalPaidAfter;
            invoice.PaymentMode = request.PaymentMode;
            invoice.TxnReference = request.TxnReference;
            invoice.PaymentDate = request.PaymentDate.HasValue
                ? DateTime.SpecifyKind(request.PaymentDate.Value, DateTimeKind.Utc)
                : DateTime.UtcNow;
            invoice.UpdatedAt = DateTime.UtcNow;

            // Set correct status based on actual amount paid
            if (totalPaidAfter >= invoice.TotalAmount)
                invoice.Status = InvoiceStatus.Paid;
            else
                invoice.Status = InvoiceStatus.PartiallyPaid;

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

        return Ok(ApiResponse<InvoiceDto>.SuccessResponse(new InvoiceDto
        {
            Id = invoice.Id,
            InvoiceNumber = invoice.InvoiceNumber,
            CaseId = invoice.CaseId,
            CaseTitle = invoice.Case!.Title,
            ClientId = invoice.ClientId,
            ClientName = invoice.ClientName ?? invoice.Client?.Name ?? "",
            ClientEmail = invoice.ClientEmail ?? invoice.Client?.Email,
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
            OutstandingAmount = outstandingAmount,
            PaymentMode = invoice.PaymentMode,
            TxnReference = invoice.TxnReference,
            PaymentDate = invoice.PaymentDate,
        }, invoice.Status == InvoiceStatus.Paid
            ? "Payment recorded — Invoice fully paid"
            : $"Payment recorded — ₹{outstandingAmount:N2} still outstanding"));
    }

    [HttpPatch("invoices/{id:guid}/cancel")]
    [Authorize(Policy = "Lawyer")]
    public async Task<ActionResult<ApiResponse<object>>> CancelInvoice(Guid id)
    {
        var firmId = _tenantService.GetCurrentFirmId();
        var userId = _tenantService.GetCurrentUserId();

        var invoice = await _context.Invoices
            .Include(i => i.Case)
                .ThenInclude(c => c.CaseLawyers)
            .FirstOrDefaultAsync(i => i.Id == id &&
                (firmId.HasValue
                    ? i.FirmId == firmId.Value
                    : i.FirmId == null &&
                      i.Case != null &&
                      i.Case.CaseLawyers.Any(cl =>
                          cl.LawyerId == userId &&
                          cl.DeletedAt == null)));

        if (invoice == null)
            return NotFound(ApiResponse<object>.ErrorResponse(
                "Invoice not found", "NOT_FOUND", 404));

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
    [Authorize]
    public async Task<IActionResult> GetInvoicePdf(Guid id)
    {
        var firmId = _tenantService.GetCurrentFirmId();
        var userId = _tenantService.GetCurrentUserId();
        var role = _tenantService.GetCurrentUserRole();

        var invoice = await _context.Invoices
            .Include(i => i.Case)
                .ThenInclude(c => c.CaseLawyers)
            .Include(i => i.Client)
            .Include(i => i.Firm)
            .FirstOrDefaultAsync(i => i.Id == id &&
                (firmId.HasValue
                    ? i.FirmId == firmId.Value
                    : i.FirmId == null &&
                      i.Case != null &&
                      i.Case.CaseLawyers.Any(cl =>
                          cl.LawyerId == userId &&
                          cl.DeletedAt == null)));

        if (invoice == null)
        {
            return NotFound(new { success = false, message = "Invoice not found" });
        }

        if (role == UserRole.Client.ToString() && invoice.ClientId != userId)
        {
            return Forbid();
        }

        User? lawyerForPdf = null;
        if (invoice.Firm == null)
        {
            lawyerForPdf = await _context.Users
                .FirstOrDefaultAsync(u => u.Id == userId);
        }
        var firmForPdf = invoice.Firm ?? new Firm
        {
            Name = lawyerForPdf?.Name ?? "LexCore",
            GstNumber = null,
            Address = lawyerForPdf != null
                ? string.Join(", ", new[]
                {
                    lawyerForPdf.City,
                    lawyerForPdf.State
                }.Where(s => !string.IsNullOrEmpty(s)))
                : null
        };
        // Build client user for PDF — use registered client
        // or create a minimal User from stored name/email
        var clientForPdf = invoice.Client ?? new User
        {
            Name = invoice.ClientName ?? "Client",
            Email = invoice.ClientEmail ?? "",
            Phone = invoice.ClientPhone
        };
        var pdfBytes = _pdfService.GenerateInvoicePdf(
            invoice, firmForPdf, clientForPdf, lawyerForPdf);

        return File(pdfBytes, "application/pdf", $"Invoice_{invoice.InvoiceNumber}.pdf");
    }

    private async Task<string> GenerateInvoiceNumber(Guid? firmId)
    {
        var year = DateTime.UtcNow.Year;

        // Use MAX-based approach to avoid duplicate numbers under concurrent load
        // Falls back to count+1 if no invoices exist yet this year
        var existing = await _context.Invoices
            .IgnoreQueryFilters()
            .Where(i => (firmId.HasValue ? i.FirmId == firmId.Value : i.FirmId == null)
                     && i.CreatedAt.Year == year)
            .Select(i => i.InvoiceNumber)
            .ToListAsync();

        var maxSeq = 0;
        foreach (var num in existing)
        {
            // Parse the sequence number from "INV-YYYY-NNNNN"
            var parts = num?.Split('-');
            if (parts?.Length == 3 && int.TryParse(parts[2], out var seq))
                maxSeq = Math.Max(maxSeq, seq);
        }

        return $"INV-{year}-{(maxSeq + 1):D5}";
    }

    [HttpGet("advance-summary")]
    [Authorize(Policy = "Lawyer")]
    public async Task<ActionResult<ApiResponse<object>>> GetAdvanceSummary()
    {
        var firmId = _tenantService.GetCurrentFirmId();
        var userId = _tenantService.GetCurrentUserId();

        // Get all advance payments for this lawyer/firm
        IQueryable<Payment> query;
        if (firmId.HasValue)
        {
            query = _context.Payments
                .Where(p => p.FirmId == firmId.Value
                         && p.IsAdvancePayment
                         && p.Status == "Completed"
                         && p.DeletedAt == null);
        }
        else
        {
            query = _context.Payments
                .Include(p => p.Case)
                    .ThenInclude(c => c!.CaseLawyers)
                .Where(p => p.FirmId == null
                         && p.IsAdvancePayment
                         && p.Status == "Completed"
                         && p.DeletedAt == null
                         && p.Case != null
                         && p.Case.CaseLawyers.Any(cl =>
                             cl.LawyerId == userId &&
                             cl.DeletedAt == null));
        }

        var totalAdvanceCollected = await query.SumAsync(p => (decimal?)p.Amount) ?? 0;
        var advanceCount = await query.CountAsync();

        return Ok(ApiResponse<object>.SuccessResponse(new
        {
            totalAdvanceCollected,
            advanceCount
        }, "Advance summary fetched"));
    }
}
