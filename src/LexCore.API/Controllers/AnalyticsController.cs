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
[Authorize]
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
        var firmId = _tenantService.GetCurrentFirmId();
        var userId = _tenantService.GetCurrentUserId();
        var now = DateTime.UtcNow;
        var startOfMonth = new DateTime(now.Year, now.Month, 1);
        var startOfWeek = now.AddDays(-(int)now.DayOfWeek);
        var endOfWeek = startOfWeek.AddDays(7);

        var overview = new OverviewDto
        {
            TotalCases = firmId.HasValue
                ? await _context.Cases.CountAsync(c => c.FirmId == firmId.Value)
                : await _context.Cases.CountAsync(c =>
                    c.FirmId == null &&
                    c.CaseLawyers.Any(cl => cl.LawyerId == userId && cl.DeletedAt == null)),

            ActiveCases = firmId.HasValue
                ? await _context.Cases.CountAsync(c =>
                    c.FirmId == firmId.Value && c.Status == CaseStatus.Active)
                : await _context.Cases.CountAsync(c =>
                    c.FirmId == null &&
                    c.Status == CaseStatus.Active &&
                    c.CaseLawyers.Any(cl => cl.LawyerId == userId && cl.DeletedAt == null)),
            RevenueThisMonth = await _context.Invoices
                .Where(i => (firmId.HasValue
                        ? i.FirmId == firmId.Value
                        : i.FirmId == null &&
                          i.Case != null &&
                          i.Case.CaseLawyers.Any(cl =>
                              cl.LawyerId == userId &&
                              cl.DeletedAt == null))
                    && (i.Status == InvoiceStatus.Paid || i.Status == InvoiceStatus.PartiallyPaid)
                    && i.PaymentDate != null
                    && i.PaymentDate.Value >= startOfMonth)
                .SumAsync(i => i.PaidAmount)
                + await _context.Payments
                    .Where(p => p.IsAdvancePayment &&
                           (firmId.HasValue ? p.FirmId == firmId.Value : p.FirmId == null) &&
                           p.PaidAt != null && p.PaidAt.Value >= startOfMonth)
                    .SumAsync(p => p.Amount),
            HearingsThisWeek = await _context.Hearings
                .CountAsync(h =>
                    (firmId.HasValue ? h.FirmId == firmId.Value : h.FirmId == null)
                    && h.HearingDate >= startOfWeek
                    && h.HearingDate < endOfWeek),
            TotalClients = firmId.HasValue
                // Firm lawyers: count registered app users with Client role
                ? await _context.Users.CountAsync(u =>
                    u.FirmId == firmId.Value && u.Role == UserRole.Client)
                // Solo lawyers: count unique client names across their cases
                // since solo lawyers don't have registered app clients
                : await _context.Cases
                    .Where(c => c.FirmId == null &&
                        c.CaseLawyers.Any(cl =>
                            cl.LawyerId == userId &&
                            cl.DeletedAt == null) &&
                        c.ClientName != null)
                    .Select(c => c.ClientName!)
                    .Distinct()
                    .CountAsync(),
            TotalLawyers = await _context.Users.CountAsync(u =>
                firmId.HasValue
                    ? u.FirmId == firmId.Value && u.Role == UserRole.Lawyer
                    : u.FirmId == null && u.Role == UserRole.Lawyer),
            PendingInvoices = await _context.Invoices.CountAsync(i =>
                (firmId.HasValue
                    ? i.FirmId == firmId.Value
                    : i.FirmId == null &&
                      i.Case != null &&
                      i.Case.CaseLawyers.Any(cl =>
                          cl.LawyerId == userId &&
                          cl.DeletedAt == null))
                && (i.Status == InvoiceStatus.Sent ||
                    i.Status == InvoiceStatus.Overdue ||
                    i.Status == InvoiceStatus.PartiallyPaid))
        };

        return Ok(ApiResponse<OverviewDto>.SuccessResponse(overview));
    }

    [HttpGet("cases")]
    public async Task<ActionResult<ApiResponse<CasesAnalyticsDto>>> GetCasesAnalytics()
    {
        var firmId = _tenantService.GetCurrentFirmId();
        var userId = _tenantService.GetCurrentUserId();

        var byStatus = await _context.Cases
            .Where(c => firmId.HasValue
                ? c.FirmId == firmId.Value
                : c.FirmId == null &&
                  c.CaseLawyers.Any(cl =>
                      cl.LawyerId == userId &&
                      cl.DeletedAt == null))
            .GroupBy(c => c.Status)
            .Select(g => new StatusBreakdown
            {
                Status = g.Key.ToString(),
                Count = g.Count()
            })
            .ToListAsync();

        var byType = await _context.Cases
            .Where(c => (firmId.HasValue
                    ? c.FirmId == firmId.Value
                    : c.FirmId == null &&
                      c.CaseLawyers.Any(cl =>
                          cl.LawyerId == userId &&
                          cl.DeletedAt == null))
                && c.CaseType != null)
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
        var firmId = _tenantService.GetCurrentFirmId();
        var twelveMonthsAgo = DateTime.UtcNow.AddMonths(-12);

        var userId = _tenantService.GetCurrentUserId();

        var monthlyData = await _context.Invoices
            .Where(i => (firmId.HasValue
                    ? i.FirmId == firmId.Value
                    : i.FirmId == null &&
                      i.Case != null &&
                      i.Case.CaseLawyers.Any(cl =>
                          cl.LawyerId == userId &&
                          cl.DeletedAt == null))
                && (i.Status == InvoiceStatus.Paid || i.Status == InvoiceStatus.PartiallyPaid)
                && i.PaymentDate != null
                && i.PaymentDate.Value >= twelveMonthsAgo)
            .GroupBy(i => new { i.PaymentDate!.Value.Year, i.PaymentDate.Value.Month })
            .Select(g => new MonthlyRevenue
            {
                Year = g.Key.Year,
                Month = g.Key.Month,
                Revenue = g.Sum(i => i.PaidAmount)  // actual money received
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

    [HttpGet("lawyers")]
    public async Task<ActionResult<ApiResponse<List<LawyerPerformanceDto>>>> GetLawyerPerformance()
    {
        var firmId = _tenantService.GetCurrentFirmId();
        var now = DateTime.UtcNow;

        // Fetch lawyers with navigation data — avoids N+1
        var lawyerData = await _context.Users
            .Where(u => u.FirmId == firmId && u.Role == UserRole.Lawyer)
            .Include(u => u.CaseLawyers)
                .ThenInclude(cl => cl.Case)
            .ToListAsync();

        // Fetch all hearing counts in two bulk queries — not N+1
        var totalHearingCounts = await _context.Hearings
            .Where(h => h.FirmId == firmId)
            .GroupBy(h => h.Case!.CaseLawyers
                .Where(cl => cl.DeletedAt == null)
                .Select(cl => cl.LawyerId)
                .FirstOrDefault())
            .Select(g => new { LawyerId = g.Key, Count = g.Count() })
            .ToListAsync();

        var upcomingHearingCounts = await _context.Hearings
            .Where(h => h.FirmId == firmId &&
                        h.HearingDate >= now &&
                        h.Status == HearingStatus.Scheduled)
            .GroupBy(h => h.Case!.CaseLawyers
                .Where(cl => cl.DeletedAt == null)
                .Select(cl => cl.LawyerId)
                .FirstOrDefault())
            .Select(g => new { LawyerId = g.Key, Count = g.Count() })
            .ToListAsync();

        var lawyers = lawyerData.Select(u => new LawyerPerformanceDto
        {
            LawyerId = u.Id,
            Name = u.Name,
            ActiveCases = u.CaseLawyers.Count(cl =>
                cl.DeletedAt == null && cl.Case?.Status == CaseStatus.Active),
            ClosedCases = u.CaseLawyers.Count(cl =>
                cl.DeletedAt == null && cl.Case?.Status == CaseStatus.Closed),
            TotalHearings = totalHearingCounts
                .FirstOrDefault(h => h.LawyerId == u.Id)?.Count ?? 0,
            UpcomingHearings = upcomingHearingCounts
                .FirstOrDefault(h => h.LawyerId == u.Id)?.Count ?? 0
        }).ToList();

        return Ok(ApiResponse<List<LawyerPerformanceDto>>.SuccessResponse(lawyers));
    }

    [HttpGet("hearings")]
    public async Task<ActionResult<ApiResponse<HearingsAnalyticsDto>>> GetHearingsAnalytics()
    {
        var firmId = _tenantService.GetCurrentFirmId();
        var userId = _tenantService.GetCurrentUserId();
        var twelveMonthsAgo = DateTime.UtcNow.AddMonths(-12);
        var now = DateTime.UtcNow;

        var monthlyData = await _context.Hearings
            .Where(h => (firmId.HasValue
                    ? h.FirmId == firmId.Value
                    : h.FirmId == null &&
                      h.Case!.CaseLawyers.Any(cl =>
                          cl.LawyerId == userId &&
                          cl.DeletedAt == null))
                && h.HearingDate >= twelveMonthsAgo)
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

        var totalHearings = await _context.Hearings.CountAsync(h =>
            firmId.HasValue
                ? h.FirmId == firmId.Value
                : h.FirmId == null &&
                  h.Case!.CaseLawyers.Any(cl =>
                      cl.LawyerId == userId &&
                      cl.DeletedAt == null));
        var completedHearings = await _context.Hearings.CountAsync(h =>
            (firmId.HasValue
                ? h.FirmId == firmId.Value
                : h.FirmId == null &&
                  h.Case!.CaseLawyers.Any(cl =>
                      cl.LawyerId == userId &&
                      cl.DeletedAt == null))
            && h.Status == HearingStatus.Completed);
        var pendingHearings = await _context.Hearings.CountAsync(h =>
            (firmId.HasValue
                ? h.FirmId == firmId.Value
                : h.FirmId == null &&
                  h.Case!.CaseLawyers.Any(cl =>
                      cl.LawyerId == userId &&
                      cl.DeletedAt == null))
            && h.Status == HearingStatus.Scheduled
            && h.HearingDate >= now);

        return Ok(ApiResponse<HearingsAnalyticsDto>.SuccessResponse(new HearingsAnalyticsDto
        {
            MonthlyData = monthlyData,
            TotalHearings = totalHearings,
            CompletedHearings = completedHearings,
            PendingHearings = pendingHearings
        }));
    }
}
