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

        var caseEntity = await _context.Cases.FirstOrDefaultAsync(c => c.Id == request.CaseId && c.FirmId == firmId);
        if (caseEntity == null)
        {
            return BadRequest(ApiResponse<HearingDto>.ErrorResponse("Case not found", "CASE_NOT_FOUND", 400));
        }

        var hearing = new Hearing
        {
            FirmId = firmId,
            CaseId = request.CaseId,
            HearingDate = request.HearingDate.Date,
            HearingTime = request.HearingTime,
            CourtName = request.CourtName,
            JudgeName = request.JudgeName,
            Notes = request.Notes,
            Status = HearingStatus.Scheduled
        };

        await _context.Hearings.AddAsync(hearing);
        await _context.SaveChangesAsync();

        // Schedule reminder 24 hours before
        var hearingDateTime = hearing.HearingDate.Add(hearing.HearingTime);
        HearingReminderJob.ScheduleReminder(hearing.Id, hearingDateTime);

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
            UpdatedAt = hearing.UpdatedAt
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
            .Where(h => h.FirmId == firmId)
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
            .Where(h => h.FirmId == firmId && h.HearingDate >= startDate && h.HearingDate <= endDate);

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
            .FirstOrDefaultAsync(h => h.Id == id && h.FirmId == firmId);

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
            UpdatedAt = hearing.UpdatedAt
        };

        return Ok(ApiResponse<HearingDto>.SuccessResponse(dto));
    }

    [HttpPatch("{id:guid}")]
    [Authorize(Policy = "Lawyer")]
    public async Task<ActionResult<ApiResponse<HearingDto>>> UpdateHearing(Guid id, [FromBody] UpdateHearingRequest request)
    {
        var firmId = _tenantService.GetCurrentFirmId();

        var hearing = await _context.Hearings
            .Include(h => h.Case)
            .FirstOrDefaultAsync(h => h.Id == id && h.FirmId == firmId);

        if (hearing == null)
        {
            return NotFound(ApiResponse<HearingDto>.ErrorResponse("Hearing not found", "NOT_FOUND", 404));
        }

        if (request.HearingDate.HasValue)
            hearing.HearingDate = request.HearingDate.Value.Date;
        if (request.HearingTime.HasValue)
            hearing.HearingTime = request.HearingTime.Value;
        if (request.CourtName != null)
            hearing.CourtName = request.CourtName;
        if (request.JudgeName != null)
            hearing.JudgeName = request.JudgeName;
        if (request.Notes != null)
            hearing.Notes = request.Notes;

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
            UpdatedAt = hearing.UpdatedAt
        }));
    }

    [HttpPatch("{id:guid}/status")]
    [Authorize(Policy = "Lawyer")]
    public async Task<ActionResult<ApiResponse<HearingDto>>> UpdateHearingStatus(Guid id, [FromBody] UpdateHearingStatusRequest request)
    {
        var firmId = _tenantService.GetCurrentFirmId();

        var hearing = await _context.Hearings
            .Include(h => h.Case)
            .FirstOrDefaultAsync(h => h.Id == id && h.FirmId == firmId);

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
            UpdatedAt = hearing.UpdatedAt
        }));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "FirmAdmin")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteHearing(Guid id)
    {
        var firmId = _tenantService.GetCurrentFirmId();

        var hearing = await _context.Hearings.FirstOrDefaultAsync(h => h.Id == id && h.FirmId == firmId);

        if (hearing == null)
        {
            return NotFound(ApiResponse<object>.ErrorResponse("Hearing not found", "NOT_FOUND", 404));
        }

        hearing.DeletedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        await _auditService.LogAsync("HEARING_DELETED", "Hearing", id, ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());

        return Ok(ApiResponse<object>.SuccessResponse(null!, "Hearing deleted successfully"));
    }
}
