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

        var (subscriptionId, paymentLink) = await _razorpayService.CreateSubscriptionAsync(firmId, planId, user.Email, user.Name);

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
    [Authorize(Policy = "FirmAdmin")]
    public async Task<ActionResult<ApiResponse<InvoiceDto>>> CreateInvoice([FromBody] CreateInvoiceRequest request)
    {
        var firmId = _tenantService.GetCurrentFirmId();

        var caseEntity = await _context.Cases.FirstOrDefaultAsync(c => c.Id == request.CaseId && c.FirmId == firmId);
        if (caseEntity == null)
        {
            return BadRequest(ApiResponse<InvoiceDto>.ErrorResponse("Case not found", "CASE_NOT_FOUND", 400));
        }

        var client = await _context.Users.FirstOrDefaultAsync(u => u.Id == request.ClientId && u.FirmId == firmId && u.Role == UserRole.Client);
        if (client == null)
        {
            return BadRequest(ApiResponse<InvoiceDto>.ErrorResponse("Client not found", "CLIENT_NOT_FOUND", 400));
        }

        var invoiceNumber = await GenerateInvoiceNumber(firmId);
        var gstAmount = request.Amount * 0.18m;
        var totalAmount = request.Amount + gstAmount;

        var invoice = new Invoice
        {
            FirmId = firmId,
            CaseId = request.CaseId,
            ClientId = request.ClientId,
            Amount = request.Amount,
            GstAmount = gstAmount,
            TotalAmount = totalAmount,
            Status = InvoiceStatus.Draft,
            DueDate = request.DueDate ?? DateTime.UtcNow.AddDays(30),
            InvoiceNumber = invoiceNumber,
            Description = request.Description,
            LineItems = request.LineItems
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
            ClientName = client.Name,
            ClientEmail = client.Email,
            Amount = invoice.Amount,
            GstAmount = invoice.GstAmount,
            TotalAmount = invoice.TotalAmount,
            Status = invoice.Status.ToString(),
            DueDate = invoice.DueDate,
            Description = invoice.Description,
            LineItems = invoice.LineItems,
            CreatedAt = invoice.CreatedAt,
            UpdatedAt = invoice.UpdatedAt
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
            .Include(i => i.Client)
            .Where(i => i.FirmId == firmId);

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
                CaseTitle = i.Case!.Title,
                ClientName = i.Client!.Name,
                TotalAmount = i.TotalAmount,
                Status = i.Status.ToString(),
                DueDate = i.DueDate,
                CreatedAt = i.CreatedAt
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
            .Include(i => i.Client)
            .FirstOrDefaultAsync(i => i.Id == id && i.FirmId == firmId);

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
            ClientName = invoice.Client!.Name,
            ClientEmail = invoice.Client.Email,
            Amount = invoice.Amount,
            GstAmount = invoice.GstAmount,
            TotalAmount = invoice.TotalAmount,
            Status = invoice.Status.ToString(),
            DueDate = invoice.DueDate,
            Description = invoice.Description,
            LineItems = invoice.LineItems,
            CreatedAt = invoice.CreatedAt,
            UpdatedAt = invoice.UpdatedAt
        }));
    }

    [HttpPatch("invoices/{id:guid}/send")]
    [Authorize(Policy = "FirmAdmin")]
    public async Task<ActionResult<ApiResponse<InvoiceDto>>> SendInvoice(Guid id)
    {
        var firmId = _tenantService.GetCurrentFirmId();

        var invoice = await _context.Invoices
            .Include(i => i.Case)
            .Include(i => i.Client)
            .Include(i => i.Firm)
            .FirstOrDefaultAsync(i => i.Id == id && i.FirmId == firmId);

        if (invoice == null)
        {
            return NotFound(ApiResponse<InvoiceDto>.ErrorResponse("Invoice not found", "NOT_FOUND", 404));
        }

        var pdfBytes = _pdfService.GenerateInvoicePdf(invoice, invoice.Firm!, invoice.Client!);

        try
        {
            await _emailService.SendInvoiceEmailAsync(
                invoice.Client!.Email,
                invoice.Client.Name,
                invoice.InvoiceNumber,
                invoice.TotalAmount,
                invoice.DueDate ?? DateTime.UtcNow.AddDays(30),
                pdfBytes);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send invoice email for {InvoiceId}", id);
        }

        invoice.Status = InvoiceStatus.Sent;
        await _context.SaveChangesAsync();
        await _auditService.LogAsync("INVOICE_SENT", "Invoice", id, ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());

        return Ok(ApiResponse<InvoiceDto>.SuccessResponse(new InvoiceDto
        {
            Id = invoice.Id,
            InvoiceNumber = invoice.InvoiceNumber,
            CaseId = invoice.CaseId,
            CaseTitle = invoice.Case!.Title,
            ClientId = invoice.ClientId,
            ClientName = invoice.Client!.Name,
            ClientEmail = invoice.Client.Email,
            Amount = invoice.Amount,
            GstAmount = invoice.GstAmount,
            TotalAmount = invoice.TotalAmount,
            Status = invoice.Status.ToString(),
            DueDate = invoice.DueDate,
            Description = invoice.Description,
            LineItems = invoice.LineItems,
            CreatedAt = invoice.CreatedAt,
            UpdatedAt = invoice.UpdatedAt
        }, "Invoice sent successfully"));
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
            .Include(i => i.Client)
            .Include(i => i.Firm)
            .FirstOrDefaultAsync(i => i.Id == id && i.FirmId == firmId);

        if (invoice == null)
        {
            return NotFound(new { success = false, message = "Invoice not found" });
        }

        if (role == UserRole.Client.ToString() && invoice.ClientId != userId)
        {
            return Forbid();
        }

        var pdfBytes = _pdfService.GenerateInvoicePdf(invoice, invoice.Firm!, invoice.Client!);

        return File(pdfBytes, "application/pdf", $"Invoice_{invoice.InvoiceNumber}.pdf");
    }

    private async Task<string> GenerateInvoiceNumber(Guid firmId)
    {
        var year = DateTime.UtcNow.Year;
        var count = await _context.Invoices
            .IgnoreQueryFilters()
            .CountAsync(i => i.FirmId == firmId && i.CreatedAt.Year == year);

        return $"INV-{year}-{(count + 1):D5}";
    }
}
