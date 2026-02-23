using System.Text.Json;
using LexCore.Application.DTOs;
using LexCore.Application.DTOs.Cases;
using LexCore.Application.Interfaces;
using LexCore.Domain.Entities;
using LexCore.Domain.Enums;
using LexCore.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LexCore.API.Controllers;

[ApiController]
[Route("api/cases")]
[Authorize]
public class CasesController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ITenantService _tenantService;
    private readonly IAuditService _auditService;

    public CasesController(AppDbContext context, ITenantService tenantService, IAuditService auditService)
    {
        _context = context;
        _tenantService = tenantService;
        _auditService = auditService;
    }

    [HttpPost]
    [Authorize(Policy = "Lawyer")]
    public async Task<ActionResult<ApiResponse<CaseDto>>> CreateCase([FromBody] CreateCaseRequest request)
    {
        var firmId = _tenantService.GetCurrentFirmId();

        var caseNumber = await GenerateCaseNumber(firmId);

        var newCase = new Case
        {
            FirmId = firmId,
            CaseNumber = caseNumber,
            Title = request.Title,
            Description = request.Description,
            CaseType = request.CaseType,
            CourtName = request.CourtName,
            FiledDate = request.FiledDate,
            InternalNotes = request.InternalNotes,
            ClientVisibleNotes = request.ClientVisibleNotes,
            Status = CaseStatus.Pending
        };

        await _context.Cases.AddAsync(newCase);
        await _context.SaveChangesAsync();
        await _auditService.LogAsync("CASE_CREATED", "Case", newCase.Id, ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());

        return CreatedAtAction(nameof(GetCase), new { id = newCase.Id }, ApiResponse<CaseDto>.SuccessResponse(MapToCaseDto(newCase), "Case created successfully"));
    }

    [HttpGet]
    public async Task<ActionResult<PagedResponse<CaseListDto>>> GetCases([FromQuery] CaseFilterRequest filter)
    {
        var firmId = _tenantService.GetCurrentFirmId();
        var userId = _tenantService.GetCurrentUserId();
        var role = _tenantService.GetCurrentUserRole();

        var query = _context.Cases
            .Where(c => c.FirmId == firmId)
            .AsQueryable();

        if (role == UserRole.Lawyer.ToString())
        {
            query = query.Where(c => c.CaseLawyers.Any(cl => cl.LawyerId == userId && cl.DeletedAt == null));
        }
        else if (role == UserRole.Client.ToString())
        {
            query = query.Where(c => c.CaseClients.Any(cc => cc.ClientId == userId && cc.DeletedAt == null));
        }

        if (filter.Status.HasValue)
            query = query.Where(c => c.Status == filter.Status.Value);

        if (!string.IsNullOrEmpty(filter.CaseType))
            query = query.Where(c => c.CaseType == filter.CaseType);

        if (filter.LawyerId.HasValue)
            query = query.Where(c => c.CaseLawyers.Any(cl => cl.LawyerId == filter.LawyerId.Value && cl.DeletedAt == null));

        if (!string.IsNullOrEmpty(filter.Search))
            query = query.Where(c => c.Title.Contains(filter.Search) || c.CaseNumber.Contains(filter.Search));

        var totalCount = await query.CountAsync();

        var cases = await query
            .OrderByDescending(c => c.CreatedAt)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(c => new CaseListDto
            {
                Id = c.Id,
                CaseNumber = c.CaseNumber,
                Title = c.Title,
                CaseType = c.CaseType,
                CourtName = c.CourtName,
                Status = c.Status.ToString(),
                FiledDate = c.FiledDate,
                CreatedAt = c.CreatedAt,
                LawyersCount = c.CaseLawyers.Count(cl => cl.DeletedAt == null),
                ClientsCount = c.CaseClients.Count(cc => cc.DeletedAt == null)
            })
            .ToListAsync();

        return Ok(new PagedResponse<CaseListDto>
        {
            Data = cases,
            Page = filter.Page,
            PageSize = filter.PageSize,
            TotalCount = totalCount
        });
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<CaseDto>>> GetCase(Guid id)
    {
        var firmId = _tenantService.GetCurrentFirmId();
        var userId = _tenantService.GetCurrentUserId();
        var role = _tenantService.GetCurrentUserRole();

        var caseEntity = await _context.Cases
            .Include(c => c.CaseLawyers.Where(cl => cl.DeletedAt == null))
                .ThenInclude(cl => cl.Lawyer)
            .Include(c => c.CaseClients.Where(cc => cc.DeletedAt == null))
                .ThenInclude(cc => cc.Client)
            .FirstOrDefaultAsync(c => c.Id == id && c.FirmId == firmId);

        if (caseEntity == null)
        {
            return NotFound(ApiResponse<CaseDto>.ErrorResponse("Case not found", "CASE_NOT_FOUND", 404));
        }

        if (role == UserRole.Lawyer.ToString() && !caseEntity.CaseLawyers.Any(cl => cl.LawyerId == userId))
        {
            return Forbid();
        }

        if (role == UserRole.Client.ToString() && !caseEntity.CaseClients.Any(cc => cc.ClientId == userId))
        {
            return Forbid();
        }

        var dto = MapToCaseDto(caseEntity);
        dto.AssignedLawyers = caseEntity.CaseLawyers.Select(cl => new AssignedUserDto
        {
            Id = cl.Lawyer!.Id,
            Name = cl.Lawyer.Name,
            Email = cl.Lawyer.Email,
            AssignedAt = cl.AssignedAt
        }).ToList();
        dto.AssignedClients = caseEntity.CaseClients.Select(cc => new AssignedUserDto
        {
            Id = cc.Client!.Id,
            Name = cc.Client.Name,
            Email = cc.Client.Email,
            AssignedAt = cc.AssignedAt
        }).ToList();

        dto.DocumentsCount = await _context.Documents.CountAsync(d => d.CaseId == id && d.DeletedAt == null);
        dto.HearingsCount = await _context.Hearings.CountAsync(h => h.CaseId == id && h.DeletedAt == null);

        if (role == UserRole.Client.ToString())
        {
            dto.InternalNotes = null;
        }

        return Ok(ApiResponse<CaseDto>.SuccessResponse(dto));
    }

    [HttpPatch("{id:guid}")]
    [Authorize(Policy = "Lawyer")]
    public async Task<ActionResult<ApiResponse<CaseDto>>> UpdateCase(Guid id, [FromBody] UpdateCaseRequest request)
    {
        var firmId = _tenantService.GetCurrentFirmId();

        var caseEntity = await _context.Cases.FirstOrDefaultAsync(c => c.Id == id && c.FirmId == firmId);

        if (caseEntity == null)
        {
            return NotFound(ApiResponse<CaseDto>.ErrorResponse("Case not found", "CASE_NOT_FOUND", 404));
        }

        var oldValues = JsonSerializer.Serialize(new { caseEntity.Title, caseEntity.Description, caseEntity.CaseType, caseEntity.CourtName });

        if (!string.IsNullOrEmpty(request.Title))
            caseEntity.Title = request.Title;
        if (request.Description != null)
            caseEntity.Description = request.Description;
        if (request.CaseType != null)
            caseEntity.CaseType = request.CaseType;
        if (request.CourtName != null)
            caseEntity.CourtName = request.CourtName;
        if (request.FiledDate.HasValue)
            caseEntity.FiledDate = request.FiledDate;
        if (request.InternalNotes != null)
            caseEntity.InternalNotes = request.InternalNotes;
        if (request.ClientVisibleNotes != null)
            caseEntity.ClientVisibleNotes = request.ClientVisibleNotes;

        await _context.SaveChangesAsync();
        await _auditService.LogAsync("CASE_UPDATED", "Case", id, oldValues, ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());

        return Ok(ApiResponse<CaseDto>.SuccessResponse(MapToCaseDto(caseEntity)));
    }

    [HttpPatch("{id:guid}/status")]
    [Authorize(Policy = "Lawyer")]
    public async Task<ActionResult<ApiResponse<CaseDto>>> UpdateCaseStatus(Guid id, [FromBody] UpdateCaseStatusRequest request)
    {
        var firmId = _tenantService.GetCurrentFirmId();

        var caseEntity = await _context.Cases.FirstOrDefaultAsync(c => c.Id == id && c.FirmId == firmId);

        if (caseEntity == null)
        {
            return NotFound(ApiResponse<CaseDto>.ErrorResponse("Case not found", "CASE_NOT_FOUND", 404));
        }

        var oldStatus = caseEntity.Status.ToString();
        caseEntity.Status = request.Status;

        await _context.SaveChangesAsync();
        await _auditService.LogAsync("CASE_STATUS_CHANGED", "Case", id, oldStatus, request.Status.ToString(), HttpContext.Connection.RemoteIpAddress?.ToString());

        return Ok(ApiResponse<CaseDto>.SuccessResponse(MapToCaseDto(caseEntity)));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "FirmAdmin")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteCase(Guid id)
    {
        var firmId = _tenantService.GetCurrentFirmId();

        var caseEntity = await _context.Cases.FirstOrDefaultAsync(c => c.Id == id && c.FirmId == firmId);

        if (caseEntity == null)
        {
            return NotFound(ApiResponse<object>.ErrorResponse("Case not found", "CASE_NOT_FOUND", 404));
        }

        caseEntity.DeletedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        await _auditService.LogAsync("CASE_DELETED", "Case", id, ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());

        return Ok(ApiResponse<object>.SuccessResponse(null!, "Case deleted successfully"));
    }

    [HttpPost("{id:guid}/lawyers")]
    [Authorize(Policy = "FirmAdmin")]
    public async Task<ActionResult<ApiResponse<object>>> AssignLawyer(Guid id, [FromBody] AssignUserRequest request)
    {
        var firmId = _tenantService.GetCurrentFirmId();

        var caseEntity = await _context.Cases.FirstOrDefaultAsync(c => c.Id == id && c.FirmId == firmId);
        if (caseEntity == null)
        {
            return NotFound(ApiResponse<object>.ErrorResponse("Case not found", "CASE_NOT_FOUND", 404));
        }

        var lawyer = await _context.Users.FirstOrDefaultAsync(u => u.Id == request.UserId && u.FirmId == firmId && u.Role == UserRole.Lawyer);
        if (lawyer == null)
        {
            return BadRequest(ApiResponse<object>.ErrorResponse("Lawyer not found", "LAWYER_NOT_FOUND", 400));
        }

        var exists = await _context.CaseLawyers.AnyAsync(cl => cl.CaseId == id && cl.LawyerId == request.UserId && cl.DeletedAt == null);
        if (exists)
        {
            return BadRequest(ApiResponse<object>.ErrorResponse("Lawyer already assigned", "ALREADY_ASSIGNED", 400));
        }

        await _context.CaseLawyers.AddAsync(new CaseLawyer { CaseId = id, LawyerId = request.UserId });
        await _context.SaveChangesAsync();
        await _auditService.LogAsync("LAWYER_ASSIGNED", "Case", id, newValues: request.UserId.ToString(), ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());

        return Ok(ApiResponse<object>.SuccessResponse(null!, "Lawyer assigned successfully"));
    }

    [HttpDelete("{id:guid}/lawyers/{lawyerId:guid}")]
    [Authorize(Policy = "FirmAdmin")]
    public async Task<ActionResult<ApiResponse<object>>> RemoveLawyer(Guid id, Guid lawyerId)
    {
        var firmId = _tenantService.GetCurrentFirmId();

        var assignment = await _context.CaseLawyers.FirstOrDefaultAsync(cl => 
            cl.CaseId == id && 
            cl.LawyerId == lawyerId && 
            cl.DeletedAt == null &&
            cl.Case!.FirmId == firmId);

        if (assignment == null)
        {
            return NotFound(ApiResponse<object>.ErrorResponse("Assignment not found", "NOT_FOUND", 404));
        }

        assignment.DeletedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        await _auditService.LogAsync("LAWYER_REMOVED", "Case", id, oldValues: lawyerId.ToString(), ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());

        return Ok(ApiResponse<object>.SuccessResponse(null!, "Lawyer removed successfully"));
    }

    [HttpPost("{id:guid}/clients")]
    [Authorize(Policy = "FirmAdmin")]
    public async Task<ActionResult<ApiResponse<object>>> AssignClient(Guid id, [FromBody] AssignUserRequest request)
    {
        var firmId = _tenantService.GetCurrentFirmId();

        var caseEntity = await _context.Cases.FirstOrDefaultAsync(c => c.Id == id && c.FirmId == firmId);
        if (caseEntity == null)
        {
            return NotFound(ApiResponse<object>.ErrorResponse("Case not found", "CASE_NOT_FOUND", 404));
        }

        var client = await _context.Users.FirstOrDefaultAsync(u => u.Id == request.UserId && u.FirmId == firmId && u.Role == UserRole.Client);
        if (client == null)
        {
            return BadRequest(ApiResponse<object>.ErrorResponse("Client not found", "CLIENT_NOT_FOUND", 400));
        }

        var exists = await _context.CaseClients.AnyAsync(cc => cc.CaseId == id && cc.ClientId == request.UserId && cc.DeletedAt == null);
        if (exists)
        {
            return BadRequest(ApiResponse<object>.ErrorResponse("Client already assigned", "ALREADY_ASSIGNED", 400));
        }

        await _context.CaseClients.AddAsync(new CaseClient { CaseId = id, ClientId = request.UserId });
        await _context.SaveChangesAsync();
        await _auditService.LogAsync("CLIENT_ASSIGNED", "Case", id, newValues: request.UserId.ToString(), ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());

        return Ok(ApiResponse<object>.SuccessResponse(null!, "Client assigned successfully"));
    }

    [HttpDelete("{id:guid}/clients/{clientId:guid}")]
    [Authorize(Policy = "FirmAdmin")]
    public async Task<ActionResult<ApiResponse<object>>> RemoveClient(Guid id, Guid clientId)
    {
        var firmId = _tenantService.GetCurrentFirmId();

        var assignment = await _context.CaseClients.FirstOrDefaultAsync(cc => 
            cc.CaseId == id && 
            cc.ClientId == clientId && 
            cc.DeletedAt == null &&
            cc.Case!.FirmId == firmId);

        if (assignment == null)
        {
            return NotFound(ApiResponse<object>.ErrorResponse("Assignment not found", "NOT_FOUND", 404));
        }

        assignment.DeletedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        await _auditService.LogAsync("CLIENT_REMOVED", "Case", id, oldValues: clientId.ToString(), ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());

        return Ok(ApiResponse<object>.SuccessResponse(null!, "Client removed successfully"));
    }

    [HttpGet("{id:guid}/timeline")]
    public async Task<ActionResult<ApiResponse<List<CaseTimelineDto>>>> GetCaseTimeline(Guid id)
    {
        var firmId = _tenantService.GetCurrentFirmId();

        var caseExists = await _context.Cases.AnyAsync(c => c.Id == id && c.FirmId == firmId);
        if (!caseExists)
        {
            return NotFound(ApiResponse<List<CaseTimelineDto>>.ErrorResponse("Case not found", "CASE_NOT_FOUND", 404));
        }

        var timeline = await _context.AuditLogs
            .Where(a => a.FirmId == firmId && a.EntityType == "Case" && a.EntityId == id)
            .OrderByDescending(a => a.Timestamp)
            .Take(50)
            .Select(a => new CaseTimelineDto
            {
                Id = a.Id,
                Action = a.Action,
                UserName = a.User != null ? a.User.Name : null,
                Timestamp = a.Timestamp,
                Details = a.NewValues
            })
            .ToListAsync();

        return Ok(ApiResponse<List<CaseTimelineDto>>.SuccessResponse(timeline));
    }

    [HttpPost("{id:guid}/notes")]
    [Authorize(Policy = "Lawyer")]
    public async Task<ActionResult<ApiResponse<CaseDto>>> AddCaseNote(Guid id, [FromBody] AddCaseNoteRequest request)
    {
        var firmId = _tenantService.GetCurrentFirmId();

        var caseEntity = await _context.Cases.FirstOrDefaultAsync(c => c.Id == id && c.FirmId == firmId);

        if (caseEntity == null)
        {
            return NotFound(ApiResponse<CaseDto>.ErrorResponse("Case not found", "CASE_NOT_FOUND", 404));
        }

        var timestamp = $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm}] ";

        if (request.IsClientVisible)
        {
            caseEntity.ClientVisibleNotes = string.IsNullOrEmpty(caseEntity.ClientVisibleNotes)
                ? timestamp + request.Note
                : caseEntity.ClientVisibleNotes + "\n" + timestamp + request.Note;
        }
        else
        {
            caseEntity.InternalNotes = string.IsNullOrEmpty(caseEntity.InternalNotes)
                ? timestamp + request.Note
                : caseEntity.InternalNotes + "\n" + timestamp + request.Note;
        }

        await _context.SaveChangesAsync();
        await _auditService.LogAsync("CASE_NOTE_ADDED", "Case", id, ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());

        return Ok(ApiResponse<CaseDto>.SuccessResponse(MapToCaseDto(caseEntity)));
    }

    private async Task<string> GenerateCaseNumber(Guid firmId)
    {
        var year = DateTime.UtcNow.Year;
        var count = await _context.Cases
            .IgnoreQueryFilters()
            .CountAsync(c => c.FirmId == firmId && c.CreatedAt.Year == year);

        return $"LEX-{year}-{(count + 1):D5}";
    }

    private static CaseDto MapToCaseDto(Case c)
    {
        return new CaseDto
        {
            Id = c.Id,
            CaseNumber = c.CaseNumber,
            Title = c.Title,
            Description = c.Description,
            CaseType = c.CaseType,
            CourtName = c.CourtName,
            Status = c.Status.ToString(),
            FiledDate = c.FiledDate,
            InternalNotes = c.InternalNotes,
            ClientVisibleNotes = c.ClientVisibleNotes,
            CreatedAt = c.CreatedAt,
            UpdatedAt = c.UpdatedAt
        };
    }
}
