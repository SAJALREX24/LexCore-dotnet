using LexCore.Application.DTOs;
using LexCore.Application.DTOs.Documents;
using LexCore.Application.Interfaces;
using LexCore.Domain.Entities;
using LexCore.Domain.Enums;
using LexCore.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LexCore.API.Controllers;

[ApiController]
[Route("api/documents")]
[Authorize]
public class DocumentsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ITenantService _tenantService;
    private readonly IStorageService _storageService;
    private readonly IAuditService _auditService;

    public DocumentsController(
        AppDbContext context,
        ITenantService tenantService,
        IStorageService storageService,
        IAuditService auditService)
    {
        _context = context;
        _tenantService = tenantService;
        _storageService = storageService;
        _auditService = auditService;
    }

    [HttpPost("upload")]
    [Authorize(Policy = "Lawyer")]
    public async Task<ActionResult<ApiResponse<DocumentDto>>> UploadDocument([FromForm] UploadDocumentRequest request, IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(ApiResponse<DocumentDto>.ErrorResponse("No file uploaded", "NO_FILE", 400));
        }

        if (file.Length > 50 * 1024 * 1024)
        {
            return BadRequest(ApiResponse<DocumentDto>.ErrorResponse("File size must be less than 50MB", "FILE_TOO_LARGE", 400));
        }

        var firmId = _tenantService.GetCurrentFirmId();
        var userId = _tenantService.GetCurrentUserId();

        var caseExists = await _context.Cases.AnyAsync(c => c.Id == request.CaseId && c.FirmId == firmId);
        if (!caseExists)
        {
            return BadRequest(ApiResponse<DocumentDto>.ErrorResponse("Case not found", "CASE_NOT_FOUND", 400));
        }

        using var stream = file.OpenReadStream();
        var fileUrl = await _storageService.UploadFileAsync(stream, file.FileName, file.ContentType, $"documents/{firmId}/{request.CaseId}");

        var document = new Document
        {
            FirmId = firmId,
            CaseId = request.CaseId,
            UploadedBy = userId,
            FileName = file.FileName,
            FileUrl = fileUrl,
            FileSize = file.Length,
            MimeType = file.ContentType,
            Version = 1,
            IsClientVisible = request.IsClientVisible,
            Tags = request.Tags,
            Description = request.Description
        };

        await _context.Documents.AddAsync(document);

        var version = new DocumentVersion
        {
            DocumentId = document.Id,
            VersionNumber = 1,
            FileUrl = fileUrl,
            UploadedBy = userId
        };

        await _context.DocumentVersions.AddAsync(version);
        await _context.SaveChangesAsync();
        await _auditService.LogAsync("DOCUMENT_UPLOADED", "Document", document.Id, ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());

        var uploader = await _context.Users.FindAsync(userId);
        var caseInfo = await _context.Cases.FindAsync(request.CaseId);

        return CreatedAtAction(nameof(DownloadDocument), new { id = document.Id }, ApiResponse<DocumentDto>.SuccessResponse(new DocumentDto
        {
            Id = document.Id,
            CaseId = document.CaseId,
            CaseTitle = caseInfo?.Title ?? "",
            FileName = document.FileName,
            MimeType = document.MimeType,
            FileSize = document.FileSize,
            Version = document.Version,
            IsClientVisible = document.IsClientVisible,
            Tags = document.Tags,
            Description = document.Description,
            UploadedByName = uploader?.Name ?? "",
            CreatedAt = document.CreatedAt,
            UpdatedAt = document.UpdatedAt
        }, "Document uploaded successfully"));
    }

    [HttpGet("case/{caseId:guid}")]
    public async Task<ActionResult<ApiResponse<List<DocumentDto>>>> GetCaseDocuments(Guid caseId)
    {
        var firmId = _tenantService.GetCurrentFirmId();
        var userId = _tenantService.GetCurrentUserId();
        var role = _tenantService.GetCurrentUserRole();

        var caseEntity = await _context.Cases
            .Include(c => c.CaseLawyers)
            .Include(c => c.CaseClients)
            .FirstOrDefaultAsync(c => c.Id == caseId && c.FirmId == firmId);

        if (caseEntity == null)
        {
            return NotFound(ApiResponse<List<DocumentDto>>.ErrorResponse("Case not found", "CASE_NOT_FOUND", 404));
        }

        if (role == UserRole.Lawyer.ToString() && !caseEntity.CaseLawyers.Any(cl => cl.LawyerId == userId && cl.DeletedAt == null))
        {
            return Forbid();
        }

        if (role == UserRole.Client.ToString() && !caseEntity.CaseClients.Any(cc => cc.ClientId == userId && cc.DeletedAt == null))
        {
            return Forbid();
        }

        var query = _context.Documents
            .Include(d => d.Uploader)
            .Where(d => d.CaseId == caseId && d.FirmId == firmId);

        if (role == UserRole.Client.ToString())
        {
            query = query.Where(d => d.IsClientVisible);
        }

        var documents = await query
            .OrderByDescending(d => d.CreatedAt)
            .Select(d => new DocumentDto
            {
                Id = d.Id,
                CaseId = d.CaseId,
                CaseTitle = caseEntity.Title,
                FileName = d.FileName,
                MimeType = d.MimeType,
                FileSize = d.FileSize,
                Version = d.Version,
                IsClientVisible = d.IsClientVisible,
                Tags = d.Tags,
                Description = d.Description,
                UploadedByName = d.Uploader!.Name,
                CreatedAt = d.CreatedAt,
                UpdatedAt = d.UpdatedAt
            })
            .ToListAsync();

        return Ok(ApiResponse<List<DocumentDto>>.SuccessResponse(documents));
    }

    [HttpGet("{id:guid}/download")]
    public async Task<IActionResult> DownloadDocument(Guid id)
    {
        var firmId = _tenantService.GetCurrentFirmId();
        var userId = _tenantService.GetCurrentUserId();
        var role = _tenantService.GetCurrentUserRole();

        var document = await _context.Documents
            .Include(d => d.Case)
                .ThenInclude(c => c!.CaseLawyers)
            .Include(d => d.Case)
                .ThenInclude(c => c!.CaseClients)
            .FirstOrDefaultAsync(d => d.Id == id && d.FirmId == firmId);

        if (document == null)
        {
            return NotFound(new { success = false, message = "Document not found", code = "NOT_FOUND" });
        }

        if (role == UserRole.Client.ToString())
        {
            if (!document.IsClientVisible || !document.Case!.CaseClients.Any(cc => cc.ClientId == userId && cc.DeletedAt == null))
            {
                return Forbid();
            }
        }
        else if (role == UserRole.Lawyer.ToString())
        {
            if (!document.Case!.CaseLawyers.Any(cl => cl.LawyerId == userId && cl.DeletedAt == null))
            {
                return Forbid();
            }
        }

        var stream = await _storageService.DownloadFileAsync(document.FileUrl);
        if (stream == null)
        {
            return NotFound(new { success = false, message = "File not found", code = "FILE_NOT_FOUND" });
        }

        return File(stream, document.MimeType ?? "application/octet-stream", document.FileName);
    }

    [HttpPatch("{id:guid}")]
    [Authorize(Policy = "Lawyer")]
    public async Task<ActionResult<ApiResponse<DocumentDto>>> UpdateDocument(Guid id, [FromBody] UpdateDocumentRequest request)
    {
        var firmId = _tenantService.GetCurrentFirmId();

        var document = await _context.Documents
            .Include(d => d.Uploader)
            .Include(d => d.Case)
            .FirstOrDefaultAsync(d => d.Id == id && d.FirmId == firmId);

        if (document == null)
        {
            return NotFound(ApiResponse<DocumentDto>.ErrorResponse("Document not found", "NOT_FOUND", 404));
        }

        if (request.Description != null)
            document.Description = request.Description;
        if (request.Tags != null)
            document.Tags = request.Tags;
        if (request.IsClientVisible.HasValue)
            document.IsClientVisible = request.IsClientVisible.Value;

        await _context.SaveChangesAsync();
        await _auditService.LogAsync("DOCUMENT_UPDATED", "Document", id, ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());

        return Ok(ApiResponse<DocumentDto>.SuccessResponse(new DocumentDto
        {
            Id = document.Id,
            CaseId = document.CaseId,
            CaseTitle = document.Case?.Title ?? "",
            FileName = document.FileName,
            MimeType = document.MimeType,
            FileSize = document.FileSize,
            Version = document.Version,
            IsClientVisible = document.IsClientVisible,
            Tags = document.Tags,
            Description = document.Description,
            UploadedByName = document.Uploader?.Name ?? "",
            CreatedAt = document.CreatedAt,
            UpdatedAt = document.UpdatedAt
        }));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "Lawyer")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteDocument(Guid id)
    {
        var firmId = _tenantService.GetCurrentFirmId();

        var document = await _context.Documents.FirstOrDefaultAsync(d => d.Id == id && d.FirmId == firmId);

        if (document == null)
        {
            return NotFound(ApiResponse<object>.ErrorResponse("Document not found", "NOT_FOUND", 404));
        }

        document.DeletedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        await _auditService.LogAsync("DOCUMENT_DELETED", "Document", id, ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());

        return Ok(ApiResponse<object>.SuccessResponse(null!, "Document deleted successfully"));
    }

    [HttpPost("{id:guid}/version")]
    [Authorize(Policy = "Lawyer")]
    public async Task<ActionResult<ApiResponse<DocumentDto>>> UploadNewVersion(Guid id, IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(ApiResponse<DocumentDto>.ErrorResponse("No file uploaded", "NO_FILE", 400));
        }

        var firmId = _tenantService.GetCurrentFirmId();
        var userId = _tenantService.GetCurrentUserId();

        var document = await _context.Documents
            .Include(d => d.Uploader)
            .Include(d => d.Case)
            .FirstOrDefaultAsync(d => d.Id == id && d.FirmId == firmId);

        if (document == null)
        {
            return NotFound(ApiResponse<DocumentDto>.ErrorResponse("Document not found", "NOT_FOUND", 404));
        }

        using var stream = file.OpenReadStream();
        var fileUrl = await _storageService.UploadFileAsync(stream, file.FileName, file.ContentType, $"documents/{firmId}/{document.CaseId}");

        document.Version++;
        document.FileUrl = fileUrl;
        document.FileName = file.FileName;
        document.FileSize = file.Length;
        document.MimeType = file.ContentType;

        var version = new DocumentVersion
        {
            DocumentId = document.Id,
            VersionNumber = document.Version,
            FileUrl = fileUrl,
            UploadedBy = userId
        };

        await _context.DocumentVersions.AddAsync(version);
        await _context.SaveChangesAsync();
        await _auditService.LogAsync("DOCUMENT_VERSION_UPLOADED", "Document", id, ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());

        return Ok(ApiResponse<DocumentDto>.SuccessResponse(new DocumentDto
        {
            Id = document.Id,
            CaseId = document.CaseId,
            CaseTitle = document.Case?.Title ?? "",
            FileName = document.FileName,
            MimeType = document.MimeType,
            FileSize = document.FileSize,
            Version = document.Version,
            IsClientVisible = document.IsClientVisible,
            Tags = document.Tags,
            Description = document.Description,
            UploadedByName = document.Uploader?.Name ?? "",
            CreatedAt = document.CreatedAt,
            UpdatedAt = document.UpdatedAt
        }, "New version uploaded successfully"));
    }

    [HttpGet("{id:guid}/versions")]
    public async Task<ActionResult<ApiResponse<List<DocumentVersionDto>>>> GetVersionHistory(Guid id)
    {
        var firmId = _tenantService.GetCurrentFirmId();

        var document = await _context.Documents.FirstOrDefaultAsync(d => d.Id == id && d.FirmId == firmId);
        if (document == null)
        {
            return NotFound(ApiResponse<List<DocumentVersionDto>>.ErrorResponse("Document not found", "NOT_FOUND", 404));
        }

        var versions = await _context.DocumentVersions
            .Where(v => v.DocumentId == id)
            .OrderByDescending(v => v.VersionNumber)
            .Select(v => new DocumentVersionDto
            {
                Id = v.Id,
                VersionNumber = v.VersionNumber,
                UploadedByName = _context.Users.Where(u => u.Id == v.UploadedBy).Select(u => u.Name).FirstOrDefault() ?? "",
                UploadedAt = v.UploadedAt
            })
            .ToListAsync();

        return Ok(ApiResponse<List<DocumentVersionDto>>.SuccessResponse(versions));
    }
}
