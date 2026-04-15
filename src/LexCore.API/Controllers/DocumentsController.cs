using LexCore.Application.DTOs;
using LexCore.Application.DTOs.Documents;
using LexCore.Application.Interfaces;
using LexCore.Domain.Entities;
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

    private readonly IConfiguration _configuration;

    public DocumentsController(
        AppDbContext context,
        ITenantService tenantService,
        IStorageService storageService,
        IAuditService auditService,
        IConfiguration configuration)
    {
        _context = context;
        _tenantService = tenantService;
        _storageService = storageService;
        _auditService = auditService;
        _configuration = configuration;
    }

    [HttpPost("upload")]
    [Authorize(Policy = "Lawyer")]
    public async Task<ActionResult<ApiResponse<DocumentDto>>> UploadDocument([FromForm] UploadDocumentRequest request, IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(ApiResponse<DocumentDto>.ErrorResponse("No file uploaded", "NO_FILE", 400));
        }

        if (file.Length > 10 * 1024 * 1024)
        {
            return BadRequest(ApiResponse<DocumentDto>.ErrorResponse("File size must be less than 10MB", "FILE_TOO_LARGE", 400));
        }

        if (!IsAllowedFileType(file))
        {
            return BadRequest(ApiResponse<DocumentDto>.ErrorResponse(
                "Only JPG, PNG and PDF files are allowed",
                "INVALID_FILE_TYPE", 400));
        }

        var userId = _tenantService.GetCurrentUserId();

        var caseExists = await _context.Cases.AnyAsync(c =>
            c.Id == request.CaseId &&
            c.CaseLawyers.Any(cl => cl.LawyerId == userId && cl.DeletedAt == null));
        if (!caseExists)
        {
            return BadRequest(ApiResponse<DocumentDto>.ErrorResponse("Case not found", "CASE_NOT_FOUND", 400));
        }

        using var stream = file.OpenReadStream();
        var fileUrl = await _storageService.UploadFileAsync(stream, file.FileName, file.ContentType, $"documents/{userId}/{request.CaseId}");

        var document = new Document
        {
            CaseId = request.CaseId,
            UploadedBy = userId,
            FileName = file.FileName,
            FileUrl = fileUrl,
            FileSize = file.Length,
            MimeType = file.ContentType,
            Version = 1,
            IsClientVisible = request.IsClientVisible,
            Tags = request.Tags,
            Description = request.Description,
            DocumentCategory = request.DocumentCategory,
            DocumentTag = request.DocumentTag,
            DocumentSource = request.DocumentSource ?? "DirectUpload",
            HearingId = request.HearingId,
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
    public async Task<ActionResult<ApiResponse<List<DocumentDto>>>> GetCaseDocuments(
        Guid caseId,
        [FromQuery] string? category = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        // Cap page size — never return more than 100 at once
        if (pageSize > 100) pageSize = 100;
        if (page < 1) page = 1;
        var userId = _tenantService.GetCurrentUserId();

        var caseEntity = await _context.Cases
            .Include(c => c.CaseLawyers)
            .FirstOrDefaultAsync(c =>
                c.Id == caseId &&
                c.CaseLawyers.Any(cl => cl.LawyerId == userId && cl.DeletedAt == null));

        if (caseEntity == null)
            return NotFound(ApiResponse<List<DocumentDto>>.ErrorResponse("Case not found", "CASE_NOT_FOUND", 404));

        var query = _context.Documents
            .Include(d => d.Uploader)
            .Where(d => d.CaseId == caseId);

        if (!string.IsNullOrEmpty(category))
        {
            query = query.Where(d => d.DocumentCategory == category);
        }

        var totalCount = await query.CountAsync();

        var documents = await query
            .OrderByDescending(d => d.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
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
                DocumentCategory = d.DocumentCategory,
                DocumentTag = d.DocumentTag,
                DocumentSource = d.DocumentSource,
                HearingId = d.HearingId,
                AIDraftStatus = d.AIDraftStatus,
                UploadedByName = d.Uploader!.Name,
                CreatedAt = d.CreatedAt,
                UpdatedAt = d.UpdatedAt
            })
            .ToListAsync();

        return Ok(new
        {
            success = true,
            data = documents,
            page = page,
            pageSize = pageSize,
            totalCount = totalCount,
            totalPages = (int)Math.Ceiling((double)totalCount / pageSize)
        });
    }

    [HttpGet("{id:guid}/download")]
    public async Task<IActionResult> DownloadDocument(Guid id)
    {
        var userId = _tenantService.GetCurrentUserId();

        var document = await _context.Documents
            .Include(d => d.Case)
                .ThenInclude(c => c!.CaseLawyers)
            .FirstOrDefaultAsync(d =>
                d.Id == id &&
                d.Case!.CaseLawyers.Any(cl => cl.LawyerId == userId && cl.DeletedAt == null));

        if (document == null)
            return NotFound(new { success = false, message = "Document not found", code = "NOT_FOUND" });

        // For S3 — redirect to signed URL (no data proxied through API)
        if (document.FileUrl.StartsWith("s3://"))
        {
            var signedUrl = await _storageService.GetSignedUrlAsync(document.FileUrl, 60);
            return Redirect(signedUrl);
        }

        // For local storage — stream directly (development only)
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
        var userId = _tenantService.GetCurrentUserId();

        var document = await _context.Documents
            .Include(d => d.Uploader)
            .Include(d => d.Case)
                .ThenInclude(c => c!.CaseLawyers)
            .FirstOrDefaultAsync(d =>
                d.Id == id &&
                d.Case!.CaseLawyers.Any(cl => cl.LawyerId == userId && cl.DeletedAt == null));

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
        var userId = _tenantService.GetCurrentUserId();

        var document = await _context.Documents
            .Include(d => d.Case)
                .ThenInclude(c => c!.CaseLawyers)
            .FirstOrDefaultAsync(d =>
                d.Id == id &&
                d.Case!.CaseLawyers.Any(cl => cl.LawyerId == userId && cl.DeletedAt == null));

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
            return BadRequest(ApiResponse<DocumentDto>.ErrorResponse(
                "No file uploaded", "NO_FILE", 400));
        }

        // File size limit — same as original upload
        if (file.Length > 10 * 1024 * 1024)
        {
            return BadRequest(ApiResponse<DocumentDto>.ErrorResponse(
                "File size must be less than 10MB", "FILE_TOO_LARGE", 400));
        }

        // File type validation — magic bytes check, same as original upload
        if (!IsAllowedFileType(file))
        {
            return BadRequest(ApiResponse<DocumentDto>.ErrorResponse(
                "Only JPG, PNG and PDF files are allowed",
                "INVALID_FILE_TYPE", 400));
        }

        var userId = _tenantService.GetCurrentUserId();

        var document = await _context.Documents
            .Include(d => d.Uploader)
            .Include(d => d.Case)
                .ThenInclude(c => c!.CaseLawyers)
            .FirstOrDefaultAsync(d =>
                d.Id == id &&
                d.Case!.CaseLawyers.Any(cl => cl.LawyerId == userId && cl.DeletedAt == null));

        if (document == null)
        {
            return NotFound(ApiResponse<DocumentDto>.ErrorResponse(
                "Document not found", "NOT_FOUND", 404));
        }

        using var stream = file.OpenReadStream();
        var fileUrl = await _storageService.UploadFileAsync(
            stream, file.FileName, file.ContentType,
            $"documents/{userId}/{document.CaseId}");

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
        await _auditService.LogAsync("DOCUMENT_VERSION_UPLOADED", "Document", id,
            ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());

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

    // Serves locally-stored files using HMAC-signed tokens
    // Only active when Storage:UseLocal = true
    // In production, S3 signed URLs are used instead
    [HttpGet("serve")]
    [AllowAnonymous]  // Token itself is the auth — verified below
    public async Task<IActionResult> ServeLocalFile(
        [FromQuery] string path,
        [FromQuery] long exp,
        [FromQuery] string sig)
    {
        // Reject if missing params
        if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(sig))
            return BadRequest("Invalid request");

        // Check expiry
        if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() > exp)
            return Unauthorized("Link expired");

        // Verify HMAC signature
        var secret = _configuration["Storage:LocalSigningSecret"] ?? "dev-secret-change-in-prod";
        var tokenData = $"{path}:{exp}";
        using var hmac = new System.Security.Cryptography.HMACSHA256(
            System.Text.Encoding.UTF8.GetBytes(secret));
        var expectedHash = Convert.ToBase64String(
            hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(tokenData)));
        var safeExpected = expectedHash.Replace("+", "-").Replace("/", "_").Replace("=", "");

        if (!string.Equals(sig, safeExpected, StringComparison.Ordinal))
            return Unauthorized("Invalid signature");

        // Path traversal protection — reject any path with ..
        if (path.Contains("..") || path.Contains("\\"))
            return BadRequest("Invalid path");

        // Serve the file
        var fileUrl = $"local://{path}";
        var stream = await _storageService.DownloadFileAsync(fileUrl);
        if (stream == null)
            return NotFound("File not found");

        // Determine content type from extension
        var ext = System.IO.Path.GetExtension(path).ToLowerInvariant();
        var contentType = ext switch
        {
            ".pdf" => "application/pdf",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            _ => "application/octet-stream"
        };

        var fileName = System.IO.Path.GetFileName(path);
        return File(stream, contentType, fileName);
    }

    private static readonly Dictionary<string, byte[]> _allowedMagicBytes = new()
    {
        { "image/jpeg", new byte[] { 0xFF, 0xD8, 0xFF } },
        { "image/png",  new byte[] { 0x89, 0x50, 0x4E, 0x47 } },
        { "application/pdf", new byte[] { 0x25, 0x50, 0x44, 0x46 } },
    };

    private static bool IsAllowedFileType(IFormFile file)
    {
        using var stream = file.OpenReadStream();
        var header = new byte[4];
        var read = stream.Read(header, 0, 4);
        if (read < 3) return false;

        foreach (var kvp in _allowedMagicBytes)
        {
            var magic = kvp.Value;
            if (read >= magic.Length)
            {
                bool match = true;
                for (int i = 0; i < magic.Length; i++)
                {
                    if (header[i] != magic[i]) { match = false; break; }
                }
                if (match) return true;
            }
        }
        return false;
    }

    [HttpGet("{id:guid}/versions")]
    public async Task<ActionResult<ApiResponse<List<DocumentVersionDto>>>> GetVersionHistory(Guid id)
    {
        var userId = _tenantService.GetCurrentUserId();

        var document = await _context.Documents
            .Include(d => d.Case)
                .ThenInclude(c => c!.CaseLawyers)
            .FirstOrDefaultAsync(d =>
                d.Id == id &&
                d.Case!.CaseLawyers.Any(cl => cl.LawyerId == userId && cl.DeletedAt == null));
        if (document == null)
        {
            return NotFound(ApiResponse<List<DocumentVersionDto>>.ErrorResponse("Document not found", "NOT_FOUND", 404));
        }

        var versions = await _context.DocumentVersions
            .Include(v => v.Document)
            .Where(v => v.DocumentId == id)
            .OrderByDescending(v => v.VersionNumber)
            .Take(50)   // documents rarely have more than 50 versions
            .Join(_context.Users,
                v => v.UploadedBy,
                u => u.Id,
                (v, u) => new DocumentVersionDto
                {
                    Id = v.Id,
                    VersionNumber = v.VersionNumber,
                    UploadedByName = u.Name,
                    UploadedAt = v.UploadedAt
                })
            .ToListAsync();

        return Ok(ApiResponse<List<DocumentVersionDto>>.SuccessResponse(versions));
    }
}
