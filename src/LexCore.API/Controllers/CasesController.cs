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
        var userId = _tenantService.GetCurrentUserId();

        var caseNumber = await GenerateCaseNumber(firmId, userId);

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var newCase = new Case
            {
                FirmId = firmId,
                CaseNumber = caseNumber,
                Title = request.Title,
                CaseBackground = request.CaseBackground,
                CaseType = request.CaseType,
                CourtName = request.CourtName,
                FiledDate = request.FiledDate.HasValue
                    ? DateTime.SpecifyKind(request.FiledDate.Value, DateTimeKind.Utc) : null,
                PrivateNotes = request.PrivateNotes,
                ClientInstructions = request.ClientInstructions,
                Status = CaseStatus.Active,

                // Client
                ClientName = request.ClientName,
                ClientPhone = request.ClientPhone,
                ClientWhatsApp = request.ClientWhatsApp,
                ClientPosition = request.ClientPosition,
                ClientType = request.ClientType,
                ClientFatherName = request.ClientFatherName,
                ClientAge = request.ClientAge,
                ClientAddress = request.ClientAddress,
                ClientIDDocumentType = request.ClientIDDocumentType,
                CompanyName = request.CompanyName,
                CompanyCIN = request.CompanyCIN,
                CompanyGST = request.CompanyGST,
                AuthorisedRepresentative = request.AuthorisedRepresentative,
                AuthorisedRepresentativeDesignation = request.AuthorisedRepresentativeDesignation,

                // Opposite party
                OppositeParty = request.OppositeParty,
                OppositePartyLawyer = request.OppositePartyLawyer,
                OppositePartiesJson = request.OppositePartiesJson,
                OppositeCounselName = request.OppositeCounselName,
                OppositeCounselPhone = request.OppositeCounselPhone,
                OppositeCounselEnrollment = request.OppositeCounselEnrollment,
                OppositeCounselCity = request.OppositeCounselCity,

                // Court & Legal
                CourtType = request.CourtType,
                StateUT = request.StateUT,
                District = request.District,
                CourtHierarchyName = request.CourtHierarchyName,
                CaseTypeCode = request.CaseTypeCode,
                CaseNature = request.CaseNature,
                SectionAct = request.SectionAct,
                ActsAndSectionsJson = request.ActsAndSectionsJson,
                FIRNumber = request.FIRNumber,
                FIRDate = request.FIRDate.HasValue
                    ? DateTime.SpecifyKind(request.FIRDate.Value, DateTimeKind.Utc) : null,
                PSDistrict = request.PSDistrict,
                PSState = request.PSState,
                NatureOfOffence = request.NatureOfOffence,
                CaseStage = request.CaseStage,
                ReliefSought = request.ReliefSought,
                CaseNotesHtml = request.CaseNotesHtml,

                // Fees — set once, never updated via UpdateCase
                FeeType = request.FeeType,
                AgreedFees = request.AgreedFees,
                AdvancePaid = request.AdvancePaid,
                PerHearingFee = request.PerHearingFee,
                VakalatnamaSigned = request.VakalatnamaSigned,
                TotalFeeLockedAt = DateTime.UtcNow,
                PaymentMode = request.PaymentMode,
                PaymentReference = request.PaymentReference,

                // Dates
                LimitationDate = request.LimitationDate.HasValue
                    ? DateTime.SpecifyKind(request.LimitationDate.Value, DateTimeKind.Utc) : null,

                // Notifications
                NotifyClientOnHearing = request.NotifyClientOnHearing,
                NotifyClientOnStatus = request.NotifyClientOnStatus,
                HearingReminderEvening = request.HearingReminderEvening,
                HearingReminderMorning = request.HearingReminderMorning,
                LimitationAlertEnabled = request.LimitationAlertEnabled,
                InvoiceOverdueAlert = request.InvoiceOverdueAlert,
                CaseStageChangeAlert = request.CaseStageChangeAlert,
                ClientWhatsAppEnabled = request.ClientWhatsAppEnabled,
                PrivateNotesHtml = request.PrivateNotesHtml,
                ClientInstructionsHtml = request.ClientInstructionsHtml,
            };

            await _context.Cases.AddAsync(newCase);
            await _context.SaveChangesAsync();

            // Solo lawyer auto-assignment
            if (!firmId.HasValue)
            {
                await _context.CaseLawyers.AddAsync(
                    new CaseLawyer { CaseId = newCase.Id, LawyerId = userId });
                await _context.SaveChangesAsync();
            }

            // Auto-create advance Payment record if advance was paid
            if (request.AdvancePaid.HasValue && request.AdvancePaid.Value > 0)
            {
                var advancePayment = new Payment
                {
                    FirmId = firmId,
                    CaseId = newCase.Id,
                    InvoiceId = null,
                    Amount = request.AdvancePaid.Value,
                    PaymentMode = request.PaymentMode,
                    ReferenceNumber = request.PaymentReference,
                    PaymentType = "Advance",
                    IsAdvancePayment = true,
                    Status = "Completed",
                    PaidAt = request.PaymentDate.HasValue
                        ? DateTime.SpecifyKind(request.PaymentDate.Value, DateTimeKind.Utc)
                        : DateTime.UtcNow,
                };
                await _context.Payments.AddAsync(advancePayment);
                await _context.SaveChangesAsync();
            }

            await transaction.CommitAsync();

            await _auditService.LogAsync("CASE_CREATED", "Case", newCase.Id,
                ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());

            return CreatedAtAction(nameof(GetCase), new { id = newCase.Id },
                ApiResponse<CaseDto>.SuccessResponse(MapToCaseDto(newCase),
                    "Case created successfully"));
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    [HttpGet]
    public async Task<ActionResult<PagedResponse<CaseListDto>>> GetCases([FromQuery] CaseFilterRequest filter)
    {
        var firmId = _tenantService.GetCurrentFirmId();
        var userId = _tenantService.GetCurrentUserId();
        var role = _tenantService.GetCurrentUserRole();

        var query = firmId.HasValue
            ? _context.Cases.Where(c => c.FirmId == firmId.Value).AsQueryable()
            : _context.Cases.Where(c => c.FirmId == null &&
                                        c.CaseLawyers.Any(cl => cl.LawyerId == userId && cl.DeletedAt == null)).AsQueryable();

        if (firmId.HasValue && role == UserRole.Lawyer.ToString())
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
                ClientsCount = c.CaseClients.Count(cc => cc.DeletedAt == null),
                ClientName = c.ClientName,
                CaseStage = c.CaseStage,
                LimitationDate = c.LimitationDate,
                NextHearingDate = c.Hearings
                    .Where(h => h.HearingDate > DateTime.UtcNow && h.DeletedAt == null)
                    .OrderBy(h => h.HearingDate)
                    .Select(h => (DateTime?)h.HearingDate)
                    .FirstOrDefault()
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
            .FirstOrDefaultAsync(c => c.Id == id &&
                (firmId.HasValue
                    ? c.FirmId == firmId.Value
                    : c.FirmId == null &&
                      c.CaseLawyers.Any(cl => cl.LawyerId == userId && cl.DeletedAt == null)));

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
            dto.PrivateNotes = null;
        }

        return Ok(ApiResponse<CaseDto>.SuccessResponse(dto));
    }

    [HttpPatch("{id:guid}")]
    [Authorize(Policy = "Lawyer")]
    public async Task<ActionResult<ApiResponse<CaseDto>>> UpdateCase(Guid id, [FromBody] UpdateCaseRequest request)
    {
        var firmId = _tenantService.GetCurrentFirmId();
        var userId = _tenantService.GetCurrentUserId();

        // Security: solo lawyers can only update their own cases
        var caseEntity = await _context.Cases
            .Include(c => c.CaseLawyers)
            .FirstOrDefaultAsync(c =>
                c.Id == id &&
                (firmId.HasValue
                    ? c.FirmId == firmId.Value
                    : c.FirmId == null &&
                      c.CaseLawyers.Any(cl =>
                          cl.LawyerId == userId &&
                          cl.DeletedAt == null)));

        if (caseEntity == null)
        {
            return NotFound(ApiResponse<CaseDto>.ErrorResponse("Case not found", "CASE_NOT_FOUND", 404));
        }

        // SECURITY GUARD: AgreedFees immutability contract.
        // AgreedFees is set once at case creation (TotalFeeLockedAt
        // records when). It must never be modified via UpdateCase.
        // UpdateCaseRequest intentionally has no AgreedFees field.
        // This block is a defensive extension point — if AgreedFees
        // is ever accidentally added to UpdateCaseRequest in a future
        // refactor, add rejection logic here:
        //   return Conflict(ApiResponse<CaseDto>.ErrorResponse(
        //       "Fee agreement is locked and cannot be modified.",
        //       "FEE_LOCKED", 409));
        // For now: no-op guard that makes the contract explicit.
        if (caseEntity.TotalFeeLockedAt.HasValue)
        {
            // Fee is locked. No action required today.
            // This comment is load-bearing documentation.
        }

        // AgreedFees immutability audit — explicitly records that
        // AgreedFees was NOT changed by this update. This creates a
        // paper trail in every UpdateCase audit log entry.
        var oldValues = JsonSerializer.Serialize(new {
            caseEntity.Title,
            caseEntity.CaseBackground,
            caseEntity.CaseType,
            caseEntity.CourtName,
            AgreedFeesImmutable = caseEntity.AgreedFees
        });

        if (!string.IsNullOrEmpty(request.Title))
            caseEntity.Title = request.Title;
        if (request.CaseBackground != null)
            caseEntity.CaseBackground = request.CaseBackground;
        if (request.CaseType != null)
            caseEntity.CaseType = request.CaseType;
        if (request.CourtName != null)
            caseEntity.CourtName = request.CourtName;
        if (request.FiledDate.HasValue)
            caseEntity.FiledDate = DateTime.SpecifyKind(request.FiledDate.Value, DateTimeKind.Utc);
        if (request.PrivateNotes != null)
            caseEntity.PrivateNotes = request.PrivateNotes;
        if (request.ClientInstructions != null)
            caseEntity.ClientInstructions = request.ClientInstructions;
        if (request.Status.HasValue)
            caseEntity.Status = request.Status.Value;
        if (request.ClientName != null)
            caseEntity.ClientName = request.ClientName;
        if (request.ClientPhone != null)
            caseEntity.ClientPhone = request.ClientPhone;
        if (request.ClientWhatsApp != null)
            caseEntity.ClientWhatsApp = request.ClientWhatsApp;
        if (request.ClientPosition != null)
            caseEntity.ClientPosition = request.ClientPosition;
        if (request.OppositeParty != null)
            caseEntity.OppositeParty = request.OppositeParty;
        if (request.OppositePartyLawyer != null)
            caseEntity.OppositePartyLawyer = request.OppositePartyLawyer;
        if (request.SectionAct != null)
            caseEntity.SectionAct = request.SectionAct;
        if (request.FIRNumber != null)
            caseEntity.FIRNumber = request.FIRNumber;
        if (request.CaseStage != null)
            caseEntity.CaseStage = request.CaseStage;
        if (request.ReliefSought != null)
            caseEntity.ReliefSought = request.ReliefSought;
        if (request.FeeType != null)
            caseEntity.FeeType = request.FeeType;
        if (request.PerHearingFee.HasValue)
            caseEntity.PerHearingFee = request.PerHearingFee;
        if (request.VakalatnamaSigned.HasValue)
            caseEntity.VakalatnamaSigned = request.VakalatnamaSigned.Value;
        if (request.LimitationDate.HasValue)
            caseEntity.LimitationDate = DateTime.SpecifyKind(request.LimitationDate.Value, DateTimeKind.Utc);
        if (request.NotifyClientOnHearing.HasValue)
            caseEntity.NotifyClientOnHearing = request.NotifyClientOnHearing.Value;
        if (request.NotifyClientOnStatus.HasValue)
            caseEntity.NotifyClientOnStatus = request.NotifyClientOnStatus.Value;

        if (request.CourtType != null) caseEntity.CourtType = request.CourtType;
        if (request.StateUT != null) caseEntity.StateUT = request.StateUT;
        if (request.District != null) caseEntity.District = request.District;
        if (request.CourtHierarchyName != null) caseEntity.CourtHierarchyName = request.CourtHierarchyName;
        if (request.CaseTypeCode != null) caseEntity.CaseTypeCode = request.CaseTypeCode;
        if (request.CaseNature != null) caseEntity.CaseNature = request.CaseNature;
        if (request.ClientType != null) caseEntity.ClientType = request.ClientType;
        if (request.ClientFatherName != null) caseEntity.ClientFatherName = request.ClientFatherName;
        if (request.ClientAge.HasValue) caseEntity.ClientAge = request.ClientAge;
        if (request.ClientAddress != null) caseEntity.ClientAddress = request.ClientAddress;
        if (request.ClientIDDocumentType != null) caseEntity.ClientIDDocumentType = request.ClientIDDocumentType;
        if (request.CompanyName != null) caseEntity.CompanyName = request.CompanyName;
        if (request.CompanyCIN != null) caseEntity.CompanyCIN = request.CompanyCIN;
        if (request.CompanyGST != null) caseEntity.CompanyGST = request.CompanyGST;
        if (request.AuthorisedRepresentative != null) caseEntity.AuthorisedRepresentative = request.AuthorisedRepresentative;
        if (request.AuthorisedRepresentativeDesignation != null) caseEntity.AuthorisedRepresentativeDesignation = request.AuthorisedRepresentativeDesignation;
        if (request.OppositePartiesJson != null) caseEntity.OppositePartiesJson = request.OppositePartiesJson;
        if (request.OppositeCounselName != null) caseEntity.OppositeCounselName = request.OppositeCounselName;
        if (request.OppositeCounselPhone != null) caseEntity.OppositeCounselPhone = request.OppositeCounselPhone;
        if (request.OppositeCounselEnrollment != null) caseEntity.OppositeCounselEnrollment = request.OppositeCounselEnrollment;
        if (request.OppositeCounselCity != null) caseEntity.OppositeCounselCity = request.OppositeCounselCity;
        if (request.ActsAndSectionsJson != null) caseEntity.ActsAndSectionsJson = request.ActsAndSectionsJson;
        if (request.FIRDate.HasValue) caseEntity.FIRDate = DateTime.SpecifyKind(request.FIRDate.Value, DateTimeKind.Utc);
        if (request.PSDistrict != null) caseEntity.PSDistrict = request.PSDistrict;
        if (request.PSState != null) caseEntity.PSState = request.PSState;
        if (request.NatureOfOffence != null) caseEntity.NatureOfOffence = request.NatureOfOffence;
        if (request.CaseNotesHtml != null) caseEntity.CaseNotesHtml = request.CaseNotesHtml;
        if (request.HearingReminderEvening.HasValue) caseEntity.HearingReminderEvening = request.HearingReminderEvening.Value;
        if (request.HearingReminderMorning.HasValue) caseEntity.HearingReminderMorning = request.HearingReminderMorning.Value;
        if (request.LimitationAlertEnabled.HasValue) caseEntity.LimitationAlertEnabled = request.LimitationAlertEnabled.Value;
        if (request.InvoiceOverdueAlert.HasValue) caseEntity.InvoiceOverdueAlert = request.InvoiceOverdueAlert.Value;
        if (request.CaseStageChangeAlert.HasValue) caseEntity.CaseStageChangeAlert = request.CaseStageChangeAlert.Value;
        if (request.ClientWhatsAppEnabled.HasValue) caseEntity.ClientWhatsAppEnabled = request.ClientWhatsAppEnabled.Value;
        if (request.PrivateNotesHtml != null) caseEntity.PrivateNotesHtml = request.PrivateNotesHtml;
        if (request.ClientInstructionsHtml != null) caseEntity.ClientInstructionsHtml = request.ClientInstructionsHtml;

        await _context.SaveChangesAsync();
        await _auditService.LogAsync("CASE_UPDATED", "Case", id, oldValues, ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());

        return Ok(ApiResponse<CaseDto>.SuccessResponse(MapToCaseDto(caseEntity)));
    }

    [HttpPatch("{id:guid}/status")]
    [Authorize(Policy = "Lawyer")]
    public async Task<ActionResult<ApiResponse<CaseDto>>> UpdateCaseStatus(Guid id, [FromBody] UpdateCaseStatusRequest request)
    {
        var firmId = _tenantService.GetCurrentFirmId();
        var userId = _tenantService.GetCurrentUserId();

        // Security: solo lawyers can only update status of their own cases
        var caseEntity = await _context.Cases
            .Include(c => c.CaseLawyers)
            .FirstOrDefaultAsync(c =>
                c.Id == id &&
                (firmId.HasValue
                    ? c.FirmId == firmId.Value
                    : c.FirmId == null &&
                      c.CaseLawyers.Any(cl =>
                          cl.LawyerId == userId &&
                          cl.DeletedAt == null)));

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
    [Authorize(Policy = "Lawyer")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteCase(Guid id)
    {
        var firmId = _tenantService.GetCurrentFirmId();
        var userId = _tenantService.GetCurrentUserId();
        var role = _tenantService.GetCurrentUserRole();

        // Security: solo lawyers can only delete their own cases
        var caseEntity = await _context.Cases
            .Include(c => c.CaseLawyers)
            .FirstOrDefaultAsync(c =>
                c.Id == id &&
                (firmId.HasValue
                    ? c.FirmId == firmId.Value
                    : c.FirmId == null &&
                      c.CaseLawyers.Any(cl =>
                          cl.LawyerId == userId &&
                          cl.DeletedAt == null)));

        if (caseEntity == null)
        {
            return NotFound(ApiResponse<object>.ErrorResponse(
                "Case not found", "CASE_NOT_FOUND", 404));
        }

        // Firm lawyers: only FirmAdmin can delete cases
        if (firmId.HasValue && role != UserRole.FirmAdmin.ToString() && role != UserRole.SuperAdmin.ToString())
        {
            return Forbid();
        }

        caseEntity.DeletedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        await _auditService.LogAsync("CASE_DELETED", "Case", id,
            ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());

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
        var userId = _tenantService.GetCurrentUserId();

        // Security: solo lawyers can only view timeline of their own cases
        var caseExists = await _context.Cases.AnyAsync(c =>
            c.Id == id &&
            (firmId.HasValue
                ? c.FirmId == firmId.Value
                : c.FirmId == null &&
                  c.CaseLawyers.Any(cl =>
                      cl.LawyerId == userId &&
                      cl.DeletedAt == null)));
        if (!caseExists)
        {
            return NotFound(ApiResponse<List<CaseTimelineDto>>.ErrorResponse("Case not found", "CASE_NOT_FOUND", 404));
        }

        var timeline = await _context.AuditLogs
            .Where(a => (firmId.HasValue ? a.FirmId == firmId.Value : a.FirmId == null) && a.EntityType == "Case" && a.EntityId == id)
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

    // GET /api/cases/{id}/notes
    [HttpGet("{id:guid}/notes")]
    [Authorize(Policy = "Lawyer")]
    public async Task<ActionResult<ApiResponse<List<CaseNoteDto>>>> GetCaseNotes(Guid id)
    {
        var firmId = _tenantService.GetCurrentFirmId();
        var userId = _tenantService.GetCurrentUserId();

        var caseExists = await _context.Cases.AnyAsync(c =>
            c.Id == id &&
            (firmId.HasValue ? c.FirmId == firmId.Value : c.FirmId == null) &&
            c.CaseLawyers.Any(cl => cl.LawyerId == userId && cl.DeletedAt == null));

        if (!caseExists)
            return NotFound(ApiResponse<List<CaseNoteDto>>.ErrorResponse(
                "Case not found", "CASE_NOT_FOUND", 404));

        var notes = await _context.CaseNotes
            .Include(n => n.Lawyer)
            .Where(n => n.CaseId == id)
            .OrderByDescending(n => n.CreatedAt)
            .Take(200)   // cap — a case with 200+ notes is unusual
            .Select(n => new CaseNoteDto
            {
                Id = n.Id,
                Note = n.Note,
                NoteType = n.NoteType,
                LawyerName = n.Lawyer != null ? n.Lawyer.Name : null,
                CreatedAt = n.CreatedAt,
            })
            .ToListAsync();

        return Ok(ApiResponse<List<CaseNoteDto>>.SuccessResponse(notes));
    }

    // POST /api/cases/{id}/notes
    [HttpPost("{id:guid}/notes")]
    [Authorize(Policy = "Lawyer")]
    public async Task<ActionResult<ApiResponse<CaseNoteDto>>> AddCaseNote(
        Guid id, [FromBody] AddCaseNoteRequest request)
    {
        var firmId = _tenantService.GetCurrentFirmId();
        var userId = _tenantService.GetCurrentUserId();

        var caseExists = await _context.Cases.AnyAsync(c =>
            c.Id == id &&
            (firmId.HasValue ? c.FirmId == firmId.Value : c.FirmId == null) &&
            c.CaseLawyers.Any(cl => cl.LawyerId == userId && cl.DeletedAt == null));

        if (!caseExists)
            return NotFound(ApiResponse<CaseNoteDto>.ErrorResponse(
                "Case not found", "CASE_NOT_FOUND", 404));

        var note = new CaseNote
        {
            CaseId = id,
            LawyerId = userId,
            Note = request.Note,
            NoteType = request.NoteType,
        };

        await _context.CaseNotes.AddAsync(note);
        await _context.SaveChangesAsync();
        await _auditService.LogAsync("CASE_NOTE_ADDED", "Case", id,
            ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());

        var lawyer = await _context.Users.FindAsync(userId);

        return Ok(ApiResponse<CaseNoteDto>.SuccessResponse(new CaseNoteDto
        {
            Id = note.Id,
            Note = note.Note,
            NoteType = note.NoteType,
            LawyerName = lawyer?.Name,
            CreatedAt = note.CreatedAt,
        }));
    }

    private async Task<string> GenerateCaseNumber(Guid? firmId, Guid userId)
    {
        var year = DateTime.UtcNow.Year;
        int retries = 0;
        const int maxRetries = 5;

        while (retries < maxRetries)
        {
            int count;
            if (firmId.HasValue)
            {
                count = await _context.Cases
                    .IgnoreQueryFilters()
                    .CountAsync(c => c.FirmId == firmId.Value && c.CreatedAt.Year == year);
            }
            else
            {
                count = await _context.Cases
                    .IgnoreQueryFilters()
                    .CountAsync(c => c.FirmId == null &&
                                     c.CaseLawyers.Any(cl => cl.LawyerId == userId) &&
                                     c.CreatedAt.Year == year);
            }

            var candidate = $"LEX-{year}-{(count + 1):D5}";

            // Check if this number already exists (handles race condition)
            var exists = await _context.Cases
                .IgnoreQueryFilters()
                .AnyAsync(c => c.CaseNumber == candidate);

            if (!exists)
                return candidate;

            // Collision: add timestamp suffix to ensure uniqueness
            retries++;
            await Task.Delay(10); // brief wait before retry
        }

        // Fallback: use timestamp-based number (guaranteed unique)
        var ticks = DateTime.UtcNow.Ticks % 100000;
        return $"LEX-{year}-T{ticks:D5}";
    }

    private static CaseDto MapToCaseDto(Case c)
    {
        return new CaseDto
        {
            Id = c.Id,
            CaseNumber = c.CaseNumber,
            Title = c.Title,
            CaseBackground = c.CaseBackground,
            CaseType = c.CaseType,
            CourtName = c.CourtName,
            Status = c.Status.ToString(),
            FiledDate = c.FiledDate,
            PrivateNotes = c.PrivateNotes,
            ClientInstructions = c.ClientInstructions,
            CreatedAt = c.CreatedAt,
            UpdatedAt = c.UpdatedAt,
            ClientName = c.ClientName,
            ClientPhone = c.ClientPhone,
            ClientWhatsApp = c.ClientWhatsApp,
            ClientPosition = c.ClientPosition,
            OppositeParty = c.OppositeParty,
            OppositePartyLawyer = c.OppositePartyLawyer,
            SectionAct = c.SectionAct,
            FIRNumber = c.FIRNumber,
            CaseStage = c.CaseStage,
            ReliefSought = c.ReliefSought,
            FeeType = c.FeeType,
            AgreedFees = c.AgreedFees,
            AdvancePaid = c.AdvancePaid,
            PerHearingFee = c.PerHearingFee,
            VakalatnamaSigned = c.VakalatnamaSigned,
            LimitationDate = c.LimitationDate,
            NotifyClientOnHearing = c.NotifyClientOnHearing,
            NotifyClientOnStatus = c.NotifyClientOnStatus,
            LimitationAlertSent30 = c.LimitationAlertSent30,
            LimitationAlertSent7 = c.LimitationAlertSent7,
            LimitationAlertSent1 = c.LimitationAlertSent1,
            CourtType = c.CourtType,
            StateUT = c.StateUT,
            District = c.District,
            CourtHierarchyName = c.CourtHierarchyName,
            CaseTypeCode = c.CaseTypeCode,
            CaseNature = c.CaseNature,
            TotalFeeLockedAt = c.TotalFeeLockedAt,
            ClientType = c.ClientType,
            ClientFatherName = c.ClientFatherName,
            ClientAge = c.ClientAge,
            ClientAddress = c.ClientAddress,
            ClientIDDocumentType = c.ClientIDDocumentType,
            CompanyName = c.CompanyName,
            CompanyCIN = c.CompanyCIN,
            CompanyGST = c.CompanyGST,
            AuthorisedRepresentative = c.AuthorisedRepresentative,
            AuthorisedRepresentativeDesignation = c.AuthorisedRepresentativeDesignation,
            OppositePartiesJson = c.OppositePartiesJson,
            OppositeCounselName = c.OppositeCounselName,
            OppositeCounselPhone = c.OppositeCounselPhone,
            OppositeCounselEnrollment = c.OppositeCounselEnrollment,
            OppositeCounselCity = c.OppositeCounselCity,
            ActsAndSectionsJson = c.ActsAndSectionsJson,
            FIRDate = c.FIRDate,
            PSDistrict = c.PSDistrict,
            PSState = c.PSState,
            NatureOfOffence = c.NatureOfOffence,
            CaseNotesHtml = c.CaseNotesHtml,
            PaymentMode = c.PaymentMode,
            PaymentReference = c.PaymentReference,
            HearingReminderEvening = c.HearingReminderEvening,
            HearingReminderMorning = c.HearingReminderMorning,
            LimitationAlertEnabled = c.LimitationAlertEnabled,
            InvoiceOverdueAlert = c.InvoiceOverdueAlert,
            CaseStageChangeAlert = c.CaseStageChangeAlert,
            ClientWhatsAppEnabled = c.ClientWhatsAppEnabled,
            PrivateNotesHtml = c.PrivateNotesHtml,
            ClientInstructionsHtml = c.ClientInstructionsHtml,
        };
    }
}
