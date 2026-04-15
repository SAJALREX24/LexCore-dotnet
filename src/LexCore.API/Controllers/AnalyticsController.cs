using System.Globalization;
using LexCore.Application.DTOs;
using LexCore.Application.DTOs.Analytics;
using LexCore.Application.Interfaces;
using LexCore.Domain.Enums;
using LexCore.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LexCore.API.Controllers;

[ApiController]
[Route("api/analytics")]
[Authorize(Policy = "Lawyer")]
public class AnalyticsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ITenantService _tenantService;

    public AnalyticsController(AppDbContext context, ITenantService tenantService)
    {
        _context = context;
        _tenantService = tenantService;
    }

    [HttpGet("overview")]
    public async Task<ActionResult<ApiResponse<OverviewDto>>> GetOverview()
    {
        var userId = _tenantService.GetCurrentUserId();
        var now = DateTime.UtcNow;
        var startOfMonth = new DateTime(now.Year, now.Month, 1);
        var startOfWeek = now.AddDays(-(int)now.DayOfWeek);
        var endOfWeek = startOfWeek.AddDays(7);

        var overview = new OverviewDto
        {
            TotalCases = await _context.Cases.CountAsync(c =>
                c.CaseLawyers.Any(cl => cl.LawyerId == userId && cl.DeletedAt == null)),

            ActiveCases = await _context.Cases.CountAsync(c =>
                c.Status == CaseStatus.Active &&
                c.CaseLawyers.Any(cl => cl.LawyerId == userId && cl.DeletedAt == null)),

            RevenueThisMonth = await _context.Invoices
                .Where(i =>
                    i.Case != null &&
                    i.Case.CaseLawyers.Any(cl => cl.LawyerId == userId && cl.DeletedAt == null) &&
                    (i.Status == InvoiceStatus.Paid || i.Status == InvoiceStatus.PartiallyPaid) &&
                    i.PaymentDate != null &&
                    i.PaymentDate.Value >= startOfMonth)
                .SumAsync(i => (decimal?)i.PaidAmount) ?? 0,

            HearingsThisWeek = await _context.Hearings.CountAsync(h =>
                h.HearingDate >= startOfWeek &&
                h.HearingDate < endOfWeek &&
                h.Case!.CaseLawyers.Any(cl => cl.LawyerId == userId && cl.DeletedAt == null)),

            // Solo: count unique client names across the lawyer's cases
            TotalClients = await _context.Cases
                .Where(c =>
                    c.CaseLawyers.Any(cl => cl.LawyerId == userId && cl.DeletedAt == null) &&
                    c.ClientName != null)
                .Select(c => c.ClientName!)
                .Distinct()
                .CountAsync(),

            TotalLawyers = 1, // Solo lawyer — always 1

            PendingInvoices = await _context.Invoices.CountAsync(i =>
                i.Case != null &&
                i.Case.CaseLawyers.Any(cl => cl.LawyerId == userId && cl.DeletedAt == null) &&
                (i.Status == InvoiceStatus.Sent ||
                 i.Status == InvoiceStatus.Overdue ||
                 i.Status == InvoiceStatus.PartiallyPaid))
        };

        return Ok(ApiResponse<OverviewDto>.SuccessResponse(overview));
    }

    [HttpGet("cases")]
    public async Task<ActionResult<ApiResponse<CasesAnalyticsDto>>> GetCasesAnalytics()
    {
        var userId = _tenantService.GetCurrentUserId();

        var byStatus = await _context.Cases
            .Where(c => c.CaseLawyers.Any(cl => cl.LawyerId == userId && cl.DeletedAt == null))
            .GroupBy(c => c.Status)
            .Select(g => new StatusBreakdown
            {
                Status = g.Key.ToString(),
                Count = g.Count()
            })
            .ToListAsync();

        var byType = await _context.Cases
            .Where(c =>
                c.CaseLawyers.Any(cl => cl.LawyerId == userId && cl.DeletedAt == null) &&
                c.CaseType != null)
            .GroupBy(c => c.CaseType!)
            .Select(g => new TypeBreakdown
            {
                Type = g.Key,
                Count = g.Count()
            })
            .OrderByDescending(t => t.Count)
            .Take(10)
            .ToListAsync();

        return Ok(ApiResponse<CasesAnalyticsDto>.SuccessResponse(new CasesAnalyticsDto
        {
            ByStatus = byStatus,
            ByType = byType
        }));
    }

    [HttpGet("revenue")]
    public async Task<ActionResult<ApiResponse<RevenueAnalyticsDto>>> GetRevenueAnalytics()
    {
        var userId = _tenantService.GetCurrentUserId();
        var twelveMonthsAgo = DateTime.UtcNow.AddMonths(-12);

        var monthlyData = await _context.Invoices
            .Where(i =>
                i.Case != null &&
                i.Case.CaseLawyers.Any(cl => cl.LawyerId == userId && cl.DeletedAt == null) &&
                (i.Status == InvoiceStatus.Paid || i.Status == InvoiceStatus.PartiallyPaid) &&
                i.PaymentDate != null &&
                i.PaymentDate.Value >= twelveMonthsAgo)
            .GroupBy(i => new { i.PaymentDate!.Value.Year, i.PaymentDate.Value.Month })
            .Select(g => new MonthlyRevenue
            {
                Year = g.Key.Year,
                Month = g.Key.Month,
                Revenue = g.Sum(i => i.PaidAmount)
            })
            .OrderBy(m => m.Year)
            .ThenBy(m => m.Month)
            .ToListAsync();

        foreach (var m in monthlyData)
        {
            m.MonthName = CultureInfo.CurrentCulture.DateTimeFormat.GetAbbreviatedMonthName(m.Month);
        }

        return Ok(ApiResponse<RevenueAnalyticsDto>.SuccessResponse(new RevenueAnalyticsDto
        {
            MonthlyData = monthlyData,
            TotalRevenue = monthlyData.Sum(m => m.Revenue)
        }));
    }

    [HttpGet("hearings")]
    public async Task<ActionResult<ApiResponse<HearingsAnalyticsDto>>> GetHearingsAnalytics()
    {
        var userId = _tenantService.GetCurrentUserId();
        var twelveMonthsAgo = DateTime.UtcNow.AddMonths(-12);
        var now = DateTime.UtcNow;

        var ownerFilter = (IQueryable<Domain.Entities.Hearing> q) =>
            q.Where(h => h.Case!.CaseLawyers.Any(cl => cl.LawyerId == userId && cl.DeletedAt == null));

        var monthlyData = await ownerFilter(_context.Hearings)
            .Where(h => h.HearingDate >= twelveMonthsAgo)
            .GroupBy(h => new { h.HearingDate.Year, h.HearingDate.Month })
            .Select(g => new MonthlyHearings
            {
                Year = g.Key.Year,
                Month = g.Key.Month,
                Count = g.Count()
            })
            .OrderBy(m => m.Year)
            .ThenBy(m => m.Month)
            .ToListAsync();

        foreach (var m in monthlyData)
        {
            m.MonthName = CultureInfo.CurrentCulture.DateTimeFormat.GetAbbreviatedMonthName(m.Month);
        }

        var totalHearings = await ownerFilter(_context.Hearings).CountAsync();
        var completedHearings = await ownerFilter(_context.Hearings)
            .CountAsync(h => h.Status == HearingStatus.Completed);
        var pendingHearings = await ownerFilter(_context.Hearings)
            .CountAsync(h => h.Status == HearingStatus.Scheduled && h.HearingDate >= now);

        return Ok(ApiResponse<HearingsAnalyticsDto>.SuccessResponse(new HearingsAnalyticsDto
        {
            MonthlyData = monthlyData,
            TotalHearings = totalHearings,
            CompletedHearings = completedHearings,
            PendingHearings = pendingHearings
        }));
    }
}
