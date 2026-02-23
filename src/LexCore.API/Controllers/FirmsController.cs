using LexCore.Application.DTOs;
using LexCore.Application.DTOs.Firms;
using LexCore.Application.Interfaces;
using LexCore.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LexCore.API.Controllers;

[ApiController]
[Route("api/firms")]
[Authorize]
public class FirmsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ITenantService _tenantService;
    private readonly IStorageService _storageService;
    private readonly IAuditService _auditService;

    public FirmsController(
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

    [HttpGet("me")]
    public async Task<ActionResult<ApiResponse<FirmDto>>> GetCurrentFirm()
    {
        var firmId = _tenantService.GetCurrentFirmId();

        var firm = await _context.Firms
            .Where(f => f.Id == firmId)
            .Select(f => new FirmDto
            {
                Id = f.Id,
                Name = f.Name,
                Slug = f.Slug,
                SubscriptionStatus = f.SubscriptionStatus.ToString(),
                Plan = f.Plan.ToString(),
                GstNumber = f.GstNumber,
                Address = f.Address,
                LogoUrl = f.LogoUrl,
                CreatedAt = f.CreatedAt
            })
            .FirstOrDefaultAsync();

        if (firm == null)
        {
            return NotFound(ApiResponse<FirmDto>.ErrorResponse("Firm not found", "FIRM_NOT_FOUND", 404));
        }

        return Ok(ApiResponse<FirmDto>.SuccessResponse(firm));
    }

    [HttpPatch("me")]
    [Authorize(Policy = "FirmAdmin")]
    public async Task<ActionResult<ApiResponse<FirmDto>>> UpdateFirm([FromBody] UpdateFirmRequest request)
    {
        var firmId = _tenantService.GetCurrentFirmId();

        var firm = await _context.Firms.FindAsync(firmId);

        if (firm == null)
        {
            return NotFound(ApiResponse<FirmDto>.ErrorResponse("Firm not found", "FIRM_NOT_FOUND", 404));
        }

        if (!string.IsNullOrEmpty(request.Name))
            firm.Name = request.Name;

        if (request.GstNumber != null)
            firm.GstNumber = request.GstNumber;

        if (request.Address != null)
            firm.Address = request.Address;

        await _context.SaveChangesAsync();
        await _auditService.LogAsync("FIRM_UPDATED", "Firm", firmId, ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());

        return Ok(ApiResponse<FirmDto>.SuccessResponse(new FirmDto
        {
            Id = firm.Id,
            Name = firm.Name,
            Slug = firm.Slug,
            SubscriptionStatus = firm.SubscriptionStatus.ToString(),
            Plan = firm.Plan.ToString(),
            GstNumber = firm.GstNumber,
            Address = firm.Address,
            LogoUrl = firm.LogoUrl,
            CreatedAt = firm.CreatedAt
        }));
    }

    [HttpPost("me/logo")]
    [Authorize(Policy = "FirmAdmin")]
    public async Task<ActionResult<ApiResponse<FirmDto>>> UploadLogo(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(ApiResponse<FirmDto>.ErrorResponse("No file uploaded", "NO_FILE", 400));
        }

        var allowedTypes = new[] { "image/jpeg", "image/png", "image/gif", "image/webp" };
        if (!allowedTypes.Contains(file.ContentType))
        {
            return BadRequest(ApiResponse<FirmDto>.ErrorResponse("Invalid file type. Allowed: JPEG, PNG, GIF, WebP", "INVALID_FILE_TYPE", 400));
        }

        if (file.Length > 5 * 1024 * 1024)
        {
            return BadRequest(ApiResponse<FirmDto>.ErrorResponse("File size must be less than 5MB", "FILE_TOO_LARGE", 400));
        }

        var firmId = _tenantService.GetCurrentFirmId();
        var firm = await _context.Firms.FindAsync(firmId);

        if (firm == null)
        {
            return NotFound(ApiResponse<FirmDto>.ErrorResponse("Firm not found", "FIRM_NOT_FOUND", 404));
        }

        using var stream = file.OpenReadStream();
        var fileUrl = await _storageService.UploadFileAsync(stream, file.FileName, file.ContentType, $"firms/{firmId}/logo");

        firm.LogoUrl = _storageService.GetPublicUrl(fileUrl);
        await _context.SaveChangesAsync();
        await _auditService.LogAsync("FIRM_LOGO_UPDATED", "Firm", firmId, ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());

        return Ok(ApiResponse<FirmDto>.SuccessResponse(new FirmDto
        {
            Id = firm.Id,
            Name = firm.Name,
            Slug = firm.Slug,
            SubscriptionStatus = firm.SubscriptionStatus.ToString(),
            Plan = firm.Plan.ToString(),
            GstNumber = firm.GstNumber,
            Address = firm.Address,
            LogoUrl = firm.LogoUrl,
            CreatedAt = firm.CreatedAt
        }, "Logo uploaded successfully"));
    }
}
