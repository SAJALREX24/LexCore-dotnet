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
[Authorize]
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
    [Authorize(Policy = "Lawyer")]
    public async Task<ActionResult<ApiResponse<HearingDto>>> CreateHearing([FromBody] CreateHearingRequest request)
    {
        var firmId = _tenantService.GetCurrentFirmId();
        var userId = _tenantService.GetCurrentUserId();

        // Security: solo lawyers must own the case via CaseLawyers
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
            return BadRequest(ApiResponse<HearingDto>.ErrorResponse(
                "Case not found", "CASE_NOT_FOUND", 400));
        }

        var hearing = new Hearing
        {
            FirmId = firmId,
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

        // Schedule reminder 24 hours before (non-fatal if Hangfire is unavailable)
        try
        {
            var hearingDateTime = hearing.HearingDate.Add(hearing.HearingTime);
            HearingReminderJob.ScheduleReminder(hearing.Id, hearingDateTime);
        }
        catch (Exception) { /* Reminder scheduling is best-effort */ }

        await _auditService.LogAsync("HEARING_CREATED", "Hearing", hearing.Id, ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());

        return CreatedAtAction(nameof(GetHearing), new { id = hearing.Id }, ApiResponse<HearingDto>.SuccessResponse(new HearingDto
        {
            Id = hearing.Id,
            CaseId = hearing.CaseId,
            CaseNumber = caseEntity.CaseNumber,
            CaseTitle = caseEntity.Title,
            HearingDate = hearing.HearingDate,
            HearingTime = hearing.HearingTime,
            CourtName = hearing.CourtName,
            JudgeName = hearing.JudgeName,
            Notes = hearing.Notes,
            Status = hearing.Status.ToString(),
            ReminderSent = hearing.ReminderSent,
            CreatedAt = hearing.CreatedAt,
            UpdatedAt = hearing.UpdatedAt,
            Outcome = hearing.Outcome,
            JudgeOrder = hearing.JudgeOrder,
            NextHearingDate = hearing.NextHearingDate,
            NextHearingTime = hearing.NextHearingTime,
            ActionRequired = hearing.ActionRequired,
            UpdatedAfterHearing = hearing.UpdatedAfterHearing,
            UpdatedAfterAt = hearing.UpdatedAfterAt,
        }, "Hearing scheduled successfully"));
    }

    [HttpGet]
    public async Task<ActionResult<PagedResponse<HearingListDto>>> GetHearings([FromQuery] HearingFilterRequest filter)
    {
        var firmId = _tenantService.GetCurrentFirmId();
        var userId = _tenantService.GetCurrentUserId();
        var role = _tenantService.GetCurrentUserRole();

        var query = _context.Hearings
            .Include(h => h.Case)
            .Where(h => firmId.HasValue ? h.FirmId == firmId.Value : h.FirmId == null)
            .AsQueryable();

        if (role == UserRole.Lawyer.ToString())
        {
            query = query.Where(h => h.Case!.CaseLawyers.Any(cl => cl.LawyerId == userId && cl.DeletedAt == null));
        }
        else if (role == UserRole.Client.ToString())
        {
            query = query.Where(h => h.Case!.CaseClients.Any(cc => cc.ClientId == userId && cc.DeletedAt == null));
        }

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
        var firmId = _tenantService.GetCurrentFirmId();
        var userId = _tenantService.GetCurrentUserId();
        var role = _tenantService.GetCurrentUserRole();

        var startDate = new DateTime(year, month, 1);
        var endDate = startDate.AddMonths(1).AddDays(-1);

        var query = _context.Hearings
            .Include(h => h.Case)
            .Where(h => (firmId.HasValue ? h.FirmId == firmId.Value : h.FirmId == null) && h.HearingDate >= startDate && h.HearingDate <= endDate);

        if (role == UserRole.Lawyer.ToString())
        {
            query = query.Where(h => h.Case!.CaseLawyers.Any(cl => cl.LawyerId == userId && cl.DeletedAt == null));
        }
        else if (role == UserRole.Client.ToString())
        {
            query = query.Where(h => h.Case!.CaseClients.Any(cc => cc.ClientId == userId && cc.DeletedAt == null));
        }

        var hearings = await query.ToListAsync();

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
        var firmId = _tenantService.GetCurrentFirmId();
        var userId = _tenantService.GetCurrentUserId();
        var role = _tenantService.GetCurrentUserRole();

        var hearing = await _context.Hearings
            .Include(h => h.Case)
                .ThenInclude(c => c!.CaseLawyers)
            .Include(h => h.Case)
                .ThenInclude(c => c!.CaseClients)
            .FirstOrDefaultAsync(h => h.Id == id && (firmId.HasValue ? h.FirmId == firmId.Value : h.FirmId == null));

        if (hearing == null)
        {
            return NotFound(ApiResponse<HearingDto>.ErrorResponse("Hearing not found", "NOT_FOUND", 404));
        }

        if (role == UserRole.Lawyer.ToString() && !hearing.Case!.CaseLawyers.Any(cl => cl.LawyerId == userId && cl.DeletedAt == null))
        {
            return Forbid();
        }

        if (role == UserRole.Client.ToString() && !hearing.Case!.CaseClients.Any(cc => cc.ClientId == userId && cc.DeletedAt == null))
        {
            return Forbid();
        }

        var dto = new HearingDto
        {
            Id = hearing.Id,
            CaseId = hearing.CaseId,
            CaseNumber = hearing.Case!.CaseNumber,
            CaseTitle = hearing.Case.Title,
            HearingDate = hearing.HearingDate,
            HearingTime = hearing.HearingTime,
            CourtName = hearing.CourtName,
            JudgeName = hearing.JudgeName,
            Notes = role == UserRole.Client.ToString() ? null : hearing.Notes,
            Status = hearing.Status.ToString(),
            ReminderSent = hearing.ReminderSent,
            CreatedAt = hearing.CreatedAt,
            UpdatedAt = hearing.UpdatedAt,
            Outcome = hearing.Outcome,
            JudgeOrder = hearing.JudgeOrder,
            NextHearingDate = hearing.NextHearingDate,
            NextHearingTime = hearing.NextHearingTime,
            ActionRequired = hearing.ActionRequired,
            UpdatedAfterHearing = hearing.UpdatedAfterHearing,
            UpdatedAfterAt = hearing.UpdatedAfterAt,
        };

        return Ok(ApiResponse<HearingDto>.SuccessResponse(dto));
    }

    [HttpPatch("{id:guid}")]
    [Authorize(Policy = "Lawyer")]
    public async Task<ActionResult<ApiResponse<HearingDto>>> UpdateHearing(Guid id, [FromBody] UpdateHearingRequest request)
    {
        var firmId = _tenantService.GetCurrentFirmId();
        var userId = _tenantService.GetCurrentUserId();

        // Security: solo lawyers can only update hearings from their own cases
        var hearing = await _context.Hearings
            .Include(h => h.Case)
                .ThenInclude(c => c!.CaseLawyers)
            .FirstOrDefaultAsync(h =>
                h.Id == id &&
                (firmId.HasValue
                    ? h.FirmId == firmId.Value
                    : h.FirmId == null &&
                      h.Case!.CaseLawyers.Any(cl =>
                          cl.LawyerId == userId &&
                          cl.DeletedAt == null)));

        if (hearing == null)
        {
            return NotFound(ApiResponse<HearingDto>.ErrorResponse("Hearing not found", "NOT_FOUND", 404));
        }

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

        // If date or time changed, reset reminder and reschedule
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
        await _auditService.LogAsync("HEARING_UPDATED", "Hearing", id, ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());

        return Ok(ApiResponse<HearingDto>.SuccessResponse(new HearingDto
        {
            Id = hearing.Id,
            CaseId = hearing.CaseId,
            CaseNumber = hearing.Case!.CaseNumber,
            CaseTitle = hearing.Case.Title,
            HearingDate = hearing.HearingDate,
            HearingTime = hearing.HearingTime,
            CourtName = hearing.CourtName,
            JudgeName = hearing.JudgeName,
            Notes = hearing.Notes,
            Status = hearing.Status.ToString(),
            ReminderSent = hearing.ReminderSent,
            CreatedAt = hearing.CreatedAt,
            UpdatedAt = hearing.UpdatedAt,
            Outcome = hearing.Outcome,
            JudgeOrder = hearing.JudgeOrder,
            NextHearingDate = hearing.NextHearingDate,
            NextHearingTime = hearing.NextHearingTime,
            ActionRequired = hearing.ActionRequired,
            UpdatedAfterHearing = hearing.UpdatedAfterHearing,
            UpdatedAfterAt = hearing.UpdatedAfterAt,
        }));
    }

    [HttpPatch("{id:guid}/status")]
    [Authorize(Policy = "Lawyer")]
    public async Task<ActionResult<ApiResponse<HearingDto>>> UpdateHearingStatus(Guid id, [FromBody] UpdateHearingStatusRequest request)
    {
        var firmId = _tenantService.GetCurrentFirmId();
        var userId = _tenantService.GetCurrentUserId();

        // Security: solo lawyers can only update status of their own hearings
        var hearing = await _context.Hearings
            .Include(h => h.Case)
                .ThenInclude(c => c!.CaseLawyers)
            .FirstOrDefaultAsync(h =>
                h.Id == id &&
                (firmId.HasValue
                    ? h.FirmId == firmId.Value
                    : h.FirmId == null &&
                      h.Case!.CaseLawyers.Any(cl =>
                          cl.LawyerId == userId &&
                          cl.DeletedAt == null)));

        if (hearing == null)
        {
            return NotFound(ApiResponse<HearingDto>.ErrorResponse("Hearing not found", "NOT_FOUND", 404));
        }

        var oldStatus = hearing.Status.ToString();
        hearing.Status = request.Status;

        await _context.SaveChangesAsync();
        await _auditService.LogAsync("HEARING_STATUS_CHANGED", "Hearing", id, oldStatus, request.Status.ToString(), HttpContext.Connection.RemoteIpAddress?.ToString());

        return Ok(ApiResponse<HearingDto>.SuccessResponse(new HearingDto
        {
            Id = hearing.Id,
            CaseId = hearing.CaseId,
            CaseNumber = hearing.Case!.CaseNumber,
            CaseTitle = hearing.Case.Title,
            HearingDate = hearing.HearingDate,
            HearingTime = hearing.HearingTime,
            CourtName = hearing.CourtName,
            JudgeName = hearing.JudgeName,
            Notes = hearing.Notes,
            Status = hearing.Status.ToString(),
            ReminderSent = hearing.ReminderSent,
            CreatedAt = hearing.CreatedAt,
            UpdatedAt = hearing.UpdatedAt,
            Outcome = hearing.Outcome,
            JudgeOrder = hearing.JudgeOrder,
            NextHearingDate = hearing.NextHearingDate,
            NextHearingTime = hearing.NextHearingTime,
            ActionRequired = hearing.ActionRequired,
            UpdatedAfterHearing = hearing.UpdatedAfterHearing,
            UpdatedAfterAt = hearing.UpdatedAfterAt,
        }));
    }

    [HttpPatch("{id:guid}/outcome")]
    [Authorize(Policy = "Lawyer")]
    public async Task<ActionResult<ApiResponse<HearingDto>>> RecordHearingOutcome(
        Guid id, [FromBody] PostHearingUpdateRequest request)
    {
        var firmId = _tenantService.GetCurrentFirmId();
        var userId = _tenantService.GetCurrentUserId();

        // Security: solo lawyers can only record outcome for their own hearings
        var hearing = await _context.Hearings
            .Include(h => h.Case)
                .ThenInclude(c => c!.CaseLawyers)
            .FirstOrDefaultAsync(h =>
                h.Id == id &&
                (firmId.HasValue
                    ? h.FirmId == firmId.Value
                    : h.FirmId == null &&
                      h.Case!.CaseLawyers.Any(cl =>
                          cl.LawyerId == userId &&
                          cl.DeletedAt == null)));

        if (hearing == null)
            return NotFound(ApiResponse<HearingDto>.ErrorResponse(
                "Hearing not found", "NOT_FOUND", 404));

        hearing.Status = request.Outcome switch
        {
            "Completed" => HearingStatus.Completed,
            "Adjourned" => HearingStatus.Adjourned,
            "PartHeard" => HearingStatus.Adjourned,
            "Stayed"    => HearingStatus.Adjourned,
            _           => HearingStatus.Completed
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
                FirmId = firmId,
                CaseId = hearing.CaseId,
                HearingDate = DateTime.SpecifyKind(
                    request.NextHearingDate.Value.Date, DateTimeKind.Utc),
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

        return Ok(ApiResponse<HearingDto>.SuccessResponse(new HearingDto
        {
            Id = hearing.Id,
            CaseId = hearing.CaseId,
            CaseNumber = hearing.Case!.CaseNumber,
            CaseTitle = hearing.Case.Title,
            HearingDate = hearing.HearingDate,
            HearingTime = hearing.HearingTime,
            CourtName = hearing.CourtName,
            JudgeName = hearing.JudgeName,
            Notes = hearing.Notes,
            Status = hearing.Status.ToString(),
            ReminderSent = hearing.ReminderSent,
            CreatedAt = hearing.CreatedAt,
            UpdatedAt = hearing.UpdatedAt,
            Outcome = hearing.Outcome,
            JudgeOrder = hearing.JudgeOrder,
            NextHearingDate = hearing.NextHearingDate,
            NextHearingTime = hearing.NextHearingTime,
            ActionRequired = hearing.ActionRequired,
            UpdatedAfterHearing = hearing.UpdatedAfterHearing,
            UpdatedAfterAt = hearing.UpdatedAfterAt,
        }, nextHearing != null
            ? "Outcome recorded. Next hearing scheduled."
            : "Outcome recorded successfully."));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "Lawyer")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteHearing(Guid id)
    {
        var firmId = _tenantService.GetCurrentFirmId();
        var userId = _tenantService.GetCurrentUserId();
        var role = _tenantService.GetCurrentUserRole();

        // Security: solo lawyers can only delete hearings from their own cases
        var hearing = await _context.Hearings
            .Include(h => h.Case)
                .ThenInclude(c => c!.CaseLawyers)
            .FirstOrDefaultAsync(h =>
                h.Id == id &&
                (firmId.HasValue
                    ? h.FirmId == firmId.Value
                    : h.FirmId == null &&
                      h.Case!.CaseLawyers.Any(cl =>
                          cl.LawyerId == userId &&
                          cl.DeletedAt == null)));

        if (hearing == null)
        {
            return NotFound(ApiResponse<object>.ErrorResponse(
                "Hearing not found", "NOT_FOUND", 404));
        }

        // Firm lawyers: only FirmAdmin can delete hearings
        if (firmId.HasValue && role != UserRole.FirmAdmin.ToString() && role != UserRole.SuperAdmin.ToString())
        {
            return Forbid();
        }

        hearing.DeletedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        await _auditService.LogAsync("HEARING_DELETED", "Hearing", id,
            ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());

        return Ok(ApiResponse<object>.SuccessResponse(null!, "Hearing deleted successfully"));
    }
}
