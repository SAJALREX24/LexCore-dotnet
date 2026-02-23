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
[Authorize(Policy = "FirmAdmin")]
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
        var now = DateTime.UtcNow;
        var startOfMonth = new DateTime(now.Year, now.Month, 1);
        var startOfWeek = now.AddDays(-(int)now.DayOfWeek);
        var endOfWeek = startOfWeek.AddDays(7);

        var overview = new OverviewDto
        {
            TotalCases = await _context.Cases.CountAsync(c => c.FirmId == firmId),
            ActiveCases = await _context.Cases.CountAsync(c => c.FirmId == firmId && c.Status == CaseStatus.Active),
            RevenueThisMonth = await _context.Invoices
                .Where(i => i.FirmId == firmId && i.Status == InvoiceStatus.Paid && i.UpdatedAt >= startOfMonth)
                .SumAsync(i => i.TotalAmount),
            HearingsThisWeek = await _context.Hearings
                .CountAsync(h => h.FirmId == firmId && h.HearingDate >= startOfWeek && h.HearingDate < endOfWeek),
            TotalClients = await _context.Users.CountAsync(u => u.FirmId == firmId && u.Role == UserRole.Client),
            TotalLawyers = await _context.Users.CountAsync(u => u.FirmId == firmId && u.Role == UserRole.Lawyer),
            PendingInvoices = await _context.Invoices.CountAsync(i => i.FirmId == firmId && (i.Status == InvoiceStatus.Sent || i.Status == InvoiceStatus.Overdue))
        };

        return Ok(ApiResponse<OverviewDto>.SuccessResponse(overview));
    }

    [HttpGet("cases")]
    public async Task<ActionResult<ApiResponse<CasesAnalyticsDto>>> GetCasesAnalytics()
    {
        var firmId = _tenantService.GetCurrentFirmId();

        var byStatus = await _context.Cases
            .Where(c => c.FirmId == firmId)
            .GroupBy(c => c.Status)
            .Select(g => new StatusBreakdown
            {
                Status = g.Key.ToString(),
                Count = g.Count()
            })
            .ToListAsync();

        var byType = await _context.Cases
            .Where(c => c.FirmId == firmId && c.CaseType != null)
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

        var monthlyData = await _context.Invoices
            .Where(i => i.FirmId == firmId && i.Status == InvoiceStatus.Paid && i.UpdatedAt >= twelveMonthsAgo)
            .GroupBy(i => new { i.UpdatedAt.Year, i.UpdatedAt.Month })
            .Select(g => new MonthlyRevenue
            {
                Year = g.Key.Year,
                Month = g.Key.Month,
                Revenue = g.Sum(i => i.TotalAmount)
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

        var lawyers = await _context.Users
            .Where(u => u.FirmId == firmId && u.Role == UserRole.Lawyer)
            .Select(u => new LawyerPerformanceDto
            {
                LawyerId = u.Id,
                Name = u.Name,
                ActiveCases = u.CaseLawyers.Count(cl => cl.DeletedAt == null && cl.Case!.Status == CaseStatus.Active),
                ClosedCases = u.CaseLawyers.Count(cl => cl.DeletedAt == null && cl.Case!.Status == CaseStatus.Closed),
                TotalHearings = _context.Hearings.Count(h => 
                    h.Case!.CaseLawyers.Any(cl => cl.LawyerId == u.Id && cl.DeletedAt == null) && h.FirmId == firmId),
                UpcomingHearings = _context.Hearings.Count(h => 
                    h.Case!.CaseLawyers.Any(cl => cl.LawyerId == u.Id && cl.DeletedAt == null) && 
                    h.FirmId == firmId && 
                    h.HearingDate >= now && 
                    h.Status == HearingStatus.Scheduled)
            })
            .ToListAsync();

        return Ok(ApiResponse<List<LawyerPerformanceDto>>.SuccessResponse(lawyers));
    }

    [HttpGet("hearings")]
    public async Task<ActionResult<ApiResponse<HearingsAnalyticsDto>>> GetHearingsAnalytics()
    {
        var firmId = _tenantService.GetCurrentFirmId();
        var twelveMonthsAgo = DateTime.UtcNow.AddMonths(-12);
        var now = DateTime.UtcNow;

        var monthlyData = await _context.Hearings
            .Where(h => h.FirmId == firmId && h.HearingDate >= twelveMonthsAgo)
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

        var totalHearings = await _context.Hearings.CountAsync(h => h.FirmId == firmId);
        var completedHearings = await _context.Hearings.CountAsync(h => h.FirmId == firmId && h.Status == HearingStatus.Completed);
        var pendingHearings = await _context.Hearings.CountAsync(h => h.FirmId == firmId && h.Status == HearingStatus.Scheduled && h.HearingDate >= now);

        return Ok(ApiResponse<HearingsAnalyticsDto>.SuccessResponse(new HearingsAnalyticsDto
        {
            MonthlyData = monthlyData,
            TotalHearings = totalHearings,
            CompletedHearings = completedHearings,
            PendingHearings = pendingHearings
        }));
    }
}
