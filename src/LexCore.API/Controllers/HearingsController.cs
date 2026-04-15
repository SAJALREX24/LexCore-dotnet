using LexCore.Application.DTOs;
using LexCore.Application.DTOs.Hearings;
using LexCore.Application.Interfaces;
using LexCore.Domain.Entities;
using LexCore.Domain.Enums;
using LexCore.Infrastructure.Data;
using LexCore.Infrastructure.Jobs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LexCore.API.Controllers;

[ApiController]
[Route("api/hearings")]
[Authorize(Policy = "Lawyer")]
public class HearingsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ITenantService _tenantService;
    private readonly IAuditService _auditService;

    public HearingsController(AppDbContext context, ITenantService tenantService, IAuditService auditService)
    {
        _context = context;
        _tenantService = tenantService;
        _auditService = auditService;
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<HearingDto>>> CreateHearing([FromBody] CreateHearingRequest request)
    {
        var userId = _tenantService.GetCurrentUserId();

        var caseEntity = await _context.Cases
            .Include(c => c.CaseLawyers)
            .FirstOrDefaultAsync(c =>
                c.Id == request.CaseId &&
                c.CaseLawyers.Any(cl => cl.LawyerId == userId && cl.DeletedAt == null));

        if (caseEntity == null)
            return BadRequest(ApiResponse<HearingDto>.ErrorResponse("Case not found", "CASE_NOT_FOUND", 400));

        var hearing = new Hearing
        {
            CaseId = request.CaseId,
            HearingDate = DateTime.SpecifyKind(request.HearingDate.Date, DateTimeKind.Utc),
            HearingTime = request.HearingTime,
            CourtName = request.CourtName,
            JudgeName = request.JudgeName,
            Notes = request.Notes,
            Status = HearingStatus.Scheduled
        };

        await _context.Hearings.AddAsync(hearing);
        await _context.SaveChangesAsync();

        try
        {
            var hearingDateTime = hearing.HearingDate.Add(hearing.HearingTime);
            HearingReminderJob.ScheduleReminder(hearing.Id, hearingDateTime);
        }
        catch (Exception) { /* Reminder scheduling is best-effort */ }

        await _auditService.LogAsync("HEARING_CREATED", "Hearing", hearing.Id,
            ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());

        return CreatedAtAction(nameof(GetHearing), new { id = hearing.Id },
            ApiResponse<HearingDto>.SuccessResponse(MapToDto(hearing, caseEntity), "Hearing scheduled successfully"));
    }

    [HttpGet]
    public async Task<ActionResult<PagedResponse<HearingListDto>>> GetHearings([FromQuery] HearingFilterRequest filter)
    {
        var userId = _tenantService.GetCurrentUserId();

        var query = _context.Hearings
            .Include(h => h.Case)
            .Where(h => h.Case!.CaseLawyers.Any(cl => cl.LawyerId == userId && cl.DeletedAt == null))
            .AsQueryable();

        if (filter.CaseId.HasValue)
            query = query.Where(h => h.CaseId == filter.CaseId.Value);

        if (filter.Status.HasValue)
            query = query.Where(h => h.Status == filter.Status.Value);

        if (filter.FromDate.HasValue)
            query = query.Where(h => h.HearingDate >= filter.FromDate.Value.Date);

        if (filter.ToDate.HasValue)
            query = query.Where(h => h.HearingDate <= filter.ToDate.Value.Date);

        var totalCount = await query.CountAsync();

        var hearings = await query
            .OrderBy(h => h.HearingDate)
            .ThenBy(h => h.HearingTime)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(h => new HearingListDto
            {
                Id = h.Id,
                CaseId = h.CaseId,
                CaseNumber = h.Case!.CaseNumber,
                CaseTitle = h.Case.Title,
                HearingDate = h.HearingDate,
                HearingTime = h.HearingTime,
                CourtName = h.CourtName,
                Status = h.Status.ToString()
            })
            .ToListAsync();

        return Ok(new PagedResponse<HearingListDto>
        {
            Data = hearings,
            Page = filter.Page,
            PageSize = filter.PageSize,
            TotalCount = totalCount
        });
    }

    [HttpGet("calendar")]
    public async Task<ActionResult<ApiResponse<HearingCalendarDto>>> GetCalendar([FromQuery] int month, [FromQuery] int year)
    {
        var userId = _tenantService.GetCurrentUserId();

        var startDate = new DateTime(year, month, 1);
        var endDate = startDate.AddMonths(1).AddDays(-1);

        var hearings = await _context.Hearings
            .Include(h => h.Case)
            .Where(h =>
                h.HearingDate >= startDate &&
                h.HearingDate <= endDate &&
                h.Case!.CaseLawyers.Any(cl => cl.LawyerId == userId && cl.DeletedAt == null))
            .ToListAsync();

        var calendar = new HearingCalendarDto
        {
            Month = month,
            Year = year,
            Days = Enumerable.Range(1, DateTime.DaysInMonth(year, month))
                .Select(day => new HearingCalendarDay
                {
                    Day = day,
                    Hearings = hearings
                        .Where(h => h.HearingDate.Day == day)
                        .Select(h => new HearingListDto
                        {
                            Id = h.Id,
                            CaseId = h.CaseId,
                            CaseNumber = h.Case!.CaseNumber,
                            CaseTitle = h.Case.Title,
                            HearingDate = h.HearingDate,
                            HearingTime = h.HearingTime,
                            CourtName = h.CourtName,
                            Status = h.Status.ToString()
                        })
                        .ToList()
                })
                .Where(d => d.Hearings.Any())
                .ToList()
        };

        return Ok(ApiResponse<HearingCalendarDto>.SuccessResponse(calendar));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<HearingDto>>> GetHearing(Guid id)
    {
        var userId = _tenantService.GetCurrentUserId();

        var hearing = await _context.Hearings
            .Include(h => h.Case)
                .ThenInclude(c => c!.CaseLawyers)
            .FirstOrDefaultAsync(h =>
                h.Id == id &&
                h.Case!.CaseLawyers.Any(cl => cl.LawyerId == userId && cl.DeletedAt == null));

        if (hearing == null)
            return NotFound(ApiResponse<HearingDto>.ErrorResponse("Hearing not found", "NOT_FOUND", 404));

        return Ok(ApiResponse<HearingDto>.SuccessResponse(MapToDto(hearing, hearing.Case!)));
    }

    [HttpPatch("{id:guid}")]
    public async Task<ActionResult<ApiResponse<HearingDto>>> UpdateHearing(Guid id, [FromBody] UpdateHearingRequest request)
    {
        var userId = _tenantService.GetCurrentUserId();

        var hearing = await _context.Hearings
            .Include(h => h.Case)
                .ThenInclude(c => c!.CaseLawyers)
            .FirstOrDefaultAsync(h =>
                h.Id == id &&
                h.Case!.CaseLawyers.Any(cl => cl.LawyerId == userId && cl.DeletedAt == null));

        if (hearing == null)
            return NotFound(ApiResponse<HearingDto>.ErrorResponse("Hearing not found", "NOT_FOUND", 404));

        if (request.HearingDate.HasValue)
            hearing.HearingDate = DateTime.SpecifyKind(request.HearingDate.Value.Date, DateTimeKind.Utc);
        if (request.HearingTime.HasValue)
            hearing.HearingTime = request.HearingTime.Value;
        if (request.CourtName != null)
            hearing.CourtName = request.CourtName;
        if (request.JudgeName != null)
            hearing.JudgeName = request.JudgeName;
        if (request.Notes != null)
            hearing.Notes = request.Notes;

        if (request.HearingDate.HasValue || request.HearingTime.HasValue)
        {
            hearing.ReminderSent = false;
            try
            {
                var hearingDateTime = hearing.HearingDate.Add(hearing.HearingTime);
                HearingReminderJob.ScheduleReminder(hearing.Id, hearingDateTime);
            }
            catch (Exception) { /* Reminder scheduling is best-effort */ }
        }

        await _context.SaveChangesAsync();
        await _auditService.LogAsync("HEARING_UPDATED", "Hearing", id,
            ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());

        return Ok(ApiResponse<HearingDto>.SuccessResponse(MapToDto(hearing, hearing.Case!)));
    }

    [HttpPatch("{id:guid}/status")]
    public async Task<ActionResult<ApiResponse<HearingDto>>> UpdateHearingStatus(Guid id, [FromBody] UpdateHearingStatusRequest request)
    {
        var userId = _tenantService.GetCurrentUserId();

        var hearing = await _context.Hearings
            .Include(h => h.Case)
                .ThenInclude(c => c!.CaseLawyers)
            .FirstOrDefaultAsync(h =>
                h.Id == id &&
                h.Case!.CaseLawyers.Any(cl => cl.LawyerId == userId && cl.DeletedAt == null));

        if (hearing == null)
            return NotFound(ApiResponse<HearingDto>.ErrorResponse("Hearing not found", "NOT_FOUND", 404));

        var oldStatus = hearing.Status.ToString();
        hearing.Status = request.Status;

        await _context.SaveChangesAsync();
        await _auditService.LogAsync("HEARING_STATUS_CHANGED", "Hearing", id, oldStatus, request.Status.ToString(),
            HttpContext.Connection.RemoteIpAddress?.ToString());

        return Ok(ApiResponse<HearingDto>.SuccessResponse(MapToDto(hearing, hearing.Case!)));
    }

    [HttpPatch("{id:guid}/outcome")]
    public async Task<ActionResult<ApiResponse<HearingDto>>> RecordHearingOutcome(
        Guid id, [FromBody] PostHearingUpdateRequest request)
    {
        var userId = _tenantService.GetCurrentUserId();

        var hearing = await _context.Hearings
            .Include(h => h.Case)
                .ThenInclude(c => c!.CaseLawyers)
            .FirstOrDefaultAsync(h =>
                h.Id == id &&
                h.Case!.CaseLawyers.Any(cl => cl.LawyerId == userId && cl.DeletedAt == null));

        if (hearing == null)
            return NotFound(ApiResponse<HearingDto>.ErrorResponse("Hearing not found", "NOT_FOUND", 404));

        hearing.Status = request.Outcome switch
        {
            "Completed" => HearingStatus.Completed,
            "Adjourned"  => HearingStatus.Adjourned,
            "PartHeard"  => HearingStatus.Adjourned,
            "Stayed"     => HearingStatus.Adjourned,
            _            => HearingStatus.Completed
        };

        hearing.Outcome = request.Outcome;
        hearing.JudgeOrder = request.JudgeOrder;
        hearing.ActionRequired = request.ActionRequired;
        hearing.NextHearingDate = request.NextHearingDate.HasValue
            ? DateTime.SpecifyKind(request.NextHearingDate.Value.Date, DateTimeKind.Utc)
            : null;
        hearing.NextHearingTime = request.NextHearingTime;
        hearing.UpdatedAfterHearing = true;
        hearing.UpdatedAfterAt = DateTime.UtcNow;

        Hearing? nextHearing = null;
        if ((request.Outcome == "Adjourned" || request.Outcome == "PartHeard")
            && request.CreateNextHearing
            && request.NextHearingDate.HasValue)
        {
            nextHearing = new Hearing
            {
                CaseId = hearing.CaseId,
                HearingDate = DateTime.SpecifyKind(request.NextHearingDate.Value.Date, DateTimeKind.Utc),
                HearingTime = request.NextHearingTime ?? new TimeSpan(10, 30, 0),
                CourtName = hearing.CourtName,
                JudgeName = hearing.JudgeName,
                Status = HearingStatus.Scheduled
            };
            await _context.Hearings.AddAsync(nextHearing);

            try
            {
                var dt = nextHearing.HearingDate.Add(nextHearing.HearingTime);
                HearingReminderJob.ScheduleReminder(nextHearing.Id, dt);
            }
            catch (Exception) { }
        }

        await _context.SaveChangesAsync();
        await _auditService.LogAsync("HEARING_OUTCOME_RECORDED", "Hearing", id,
            ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());

        return Ok(ApiResponse<HearingDto>.SuccessResponse(
            MapToDto(hearing, hearing.Case!),
            nextHearing != null ? "Outcome recorded. Next hearing scheduled." : "Outcome recorded successfully."));
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteHearing(Guid id)
    {
        var userId = _tenantService.GetCurrentUserId();

        var hearing = await _context.Hearings
            .Include(h => h.Case)
                .ThenInclude(c => c!.CaseLawyers)
            .FirstOrDefaultAsync(h =>
                h.Id == id &&
                h.Case!.CaseLawyers.Any(cl => cl.LawyerId == userId && cl.DeletedAt == null));

        if (hearing == null)
            return NotFound(ApiResponse<object>.ErrorResponse("Hearing not found", "NOT_FOUND", 404));

        hearing.DeletedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        await _auditService.LogAsync("HEARING_DELETED", "Hearing", id,
            ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());

        return Ok(ApiResponse<object>.SuccessResponse(null!, "Hearing deleted successfully"));
    }

    // ── HELPERS ──────────────────────────────────────────────────────────────

    private static HearingDto MapToDto(Hearing h, Case c) => new HearingDto
    {
        Id = h.Id,
        CaseId = h.CaseId,
        CaseNumber = c.CaseNumber,
        CaseTitle = c.Title,
        HearingDate = h.HearingDate,
        HearingTime = h.HearingTime,
        CourtName = h.CourtName,
        JudgeName = h.JudgeName,
        Notes = h.Notes,
        Status = h.Status.ToString(),
        ReminderSent = h.ReminderSent,
        CreatedAt = h.CreatedAt,
        UpdatedAt = h.UpdatedAt,
        Outcome = h.Outcome,
        JudgeOrder = h.JudgeOrder,
        NextHearingDate = h.NextHearingDate,
        NextHearingTime = h.NextHearingTime,
        ActionRequired = h.ActionRequired,
        UpdatedAfterHearing = h.UpdatedAfterHearing,
        UpdatedAfterAt = h.UpdatedAfterAt,
    };
}
