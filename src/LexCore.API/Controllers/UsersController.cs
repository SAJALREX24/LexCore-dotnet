using LexCore.Application.DTOs;
using LexCore.Application.DTOs.Auth;
using LexCore.Application.DTOs.Notifications;
using LexCore.Application.DTOs.Users;
using LexCore.Application.Interfaces;
using LexCore.Domain.Enums;
using LexCore.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LexCore.API.Controllers;

[ApiController]
[Route("api/users")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ITenantService _tenantService;
    private readonly IAuditService _auditService;

    public UsersController(AppDbContext context, ITenantService tenantService, IAuditService auditService)
    {
        _context = context;
        _tenantService = tenantService;
        _auditService = auditService;
    }

    [HttpGet]
    [Authorize(Policy = "FirmAdmin")]
    public async Task<ActionResult<ApiResponse<List<UserListDto>>>> GetUsers([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var firmId = _tenantService.GetCurrentFirmId();

        var query = _context.Users
            .Where(u => u.FirmId == firmId)
            .OrderByDescending(u => u.CreatedAt);

        var totalCount = await query.CountAsync();
        var users = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new UserListDto
            {
                Id = u.Id,
                Name = u.Name,
                Email = u.Email,
                Role = u.Role.ToString(),
                IsVerified = u.IsVerified,
                LastLogin = u.LastLogin,
                CreatedAt = u.CreatedAt
            })
            .ToListAsync();

        return Ok(new PagedResponse<UserListDto>
        {
            Data = users,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        });
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<UserDetailDto>>> GetUser(Guid id)
    {
        var firmId = _tenantService.GetCurrentFirmId();
        var currentUserId = _tenantService.GetCurrentUserId();
        var currentRole = _tenantService.GetCurrentUserRole();

        var user = await _context.Users
            .Where(u => u.Id == id && (firmId.HasValue ? u.FirmId == firmId.Value : u.FirmId == null))
            .Select(u => new UserDetailDto
            {
                Id = u.Id,
                Name = u.Name,
                Email = u.Email,
                Role = u.Role.ToString(),
                IsVerified = u.IsVerified,
                LastLogin = u.LastLogin,
                CreatedAt = u.CreatedAt,
                UpdatedAt = u.UpdatedAt,
                AssignedCasesCount = u.Role == UserRole.Lawyer 
                    ? u.CaseLawyers.Count(cl => cl.DeletedAt == null)
                    : u.CaseClients.Count(cc => cc.DeletedAt == null)
            })
            .FirstOrDefaultAsync();

        if (user == null)
        {
            return NotFound(ApiResponse<UserDetailDto>.ErrorResponse("User not found", "USER_NOT_FOUND", 404));
        }

        if (currentRole == UserRole.Client.ToString() && id != currentUserId)
        {
            return Forbid();
        }

        return Ok(ApiResponse<UserDetailDto>.SuccessResponse(user));
    }

    // PATCH /api/users/me
    // Lets any logged-in user update their own profile
    [HttpPatch("me")]
    public async Task<ActionResult<ApiResponse<UserDto>>> UpdateMyProfile(
        [FromBody] UpdateUserRequest request)
    {
        var userId = _tenantService.GetCurrentUserId();
        var firmId = _tenantService.GetCurrentFirmId();

        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null)
            return NotFound(ApiResponse<UserDto>.ErrorResponse(
                "User not found", "USER_NOT_FOUND", 404));

        if (!string.IsNullOrEmpty(request.Name))
            user.Name = request.Name.Trim();

        if (!string.IsNullOrEmpty(request.Email)
            && request.Email != user.Email)
        {
            if (await _context.Users.AnyAsync(
                u => u.Email == request.Email && u.Id != userId))
                return BadRequest(ApiResponse<UserDto>.ErrorResponse(
                    "Email already in use", "EMAIL_EXISTS", 400));
            user.Email = request.Email.Trim();
        }

        if (request.Phone != null)
            user.Phone = string.IsNullOrWhiteSpace(request.Phone)
                ? null : request.Phone.Trim();

        if (request.BarEnrollmentNumber != null)
            user.BarEnrollmentNumber =
                string.IsNullOrWhiteSpace(request.BarEnrollmentNumber)
                ? null : request.BarEnrollmentNumber.Trim();

        if (request.City != null)
            user.City = string.IsNullOrWhiteSpace(request.City)
                ? null : request.City.Trim();

        if (request.State != null)
            user.State = string.IsNullOrWhiteSpace(request.State)
                ? null : request.State.Trim();

        if (request.CourtType != null)
            user.CourtType = string.IsNullOrWhiteSpace(request.CourtType)
                ? null : request.CourtType.Trim();

        await _context.SaveChangesAsync();
        await _auditService.LogAsync("USER_PROFILE_UPDATED", "User",
            userId, ipAddress:
            HttpContext.Connection.RemoteIpAddress?.ToString());

        // Get firm name for response
        string? firmName = null;
        if (firmId.HasValue)
        {
            firmName = await _context.Firms
                .Where(f => f.Id == firmId.Value)
                .Select(f => f.Name)
                .FirstOrDefaultAsync();
        }

        return Ok(ApiResponse<UserDto>.SuccessResponse(new UserDto
        {
            Id = user.Id,
            Name = user.Name,
            Email = user.Email,
            Role = user.Role.ToString(),
            FirmId = user.FirmId,
            FirmName = firmName,
            IsVerified = user.IsVerified,
            IsPhoneVerified = user.IsPhoneVerified,
            Phone = user.Phone,
            BarEnrollmentNumber = user.BarEnrollmentNumber,
            CourtType = user.CourtType,
            State = user.State,
            City = user.City,
            LastLogin = user.LastLogin,
            CreatedAt = user.CreatedAt
        }, "Profile updated successfully"));
    }

    [HttpPatch("{id:guid}")]
    public async Task<ActionResult<ApiResponse<UserDetailDto>>> UpdateUser(Guid id, [FromBody] UpdateUserRequest request)
    {
        var firmId = _tenantService.GetCurrentFirmId();
        var currentUserId = _tenantService.GetCurrentUserId();
        var currentRole = _tenantService.GetCurrentUserRole();

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == id && (firmId.HasValue ? u.FirmId == firmId.Value : u.FirmId == null));

        if (user == null)
        {
            return NotFound(ApiResponse<UserDetailDto>.ErrorResponse("User not found", "USER_NOT_FOUND", 404));
        }

        if (currentRole != UserRole.FirmAdmin.ToString() && currentRole != UserRole.SuperAdmin.ToString() && id != currentUserId)
        {
            return Forbid();
        }

        if (!string.IsNullOrEmpty(request.Name))
            user.Name = request.Name;

        if (!string.IsNullOrEmpty(request.Email) && request.Email != user.Email)
        {
            if (await _context.Users.AnyAsync(u => u.Email == request.Email && u.Id != id))
            {
                return BadRequest(ApiResponse<UserDetailDto>.ErrorResponse("Email already in use", "EMAIL_EXISTS", 400));
            }
            user.Email = request.Email;
        }

        await _context.SaveChangesAsync();
        await _auditService.LogAsync("USER_UPDATED", "User", id, ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());

        return Ok(ApiResponse<UserDetailDto>.SuccessResponse(new UserDetailDto
        {
            Id = user.Id,
            Name = user.Name,
            Email = user.Email,
            Role = user.Role.ToString(),
            IsVerified = user.IsVerified,
            LastLogin = user.LastLogin,
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt
        }));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "FirmAdmin")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteUser(Guid id)
    {
        var firmId = _tenantService.GetCurrentFirmId();
        var currentUserId = _tenantService.GetCurrentUserId();

        if (id == currentUserId)
        {
            return BadRequest(ApiResponse<object>.ErrorResponse("Cannot delete your own account", "CANNOT_DELETE_SELF", 400));
        }

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == id && u.FirmId == firmId);

        if (user == null)
        {
            return NotFound(ApiResponse<object>.ErrorResponse("User not found", "USER_NOT_FOUND", 404));
        }

        user.DeletedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        await _auditService.LogAsync("USER_DELETED", "User", id, ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());

        return Ok(ApiResponse<object>.SuccessResponse(null!, "User deleted successfully"));
    }

    // PUT /api/users/fcm-token
    // Flutter calls this on every app login to register device push token
    [HttpPut("fcm-token")]
    [Authorize]
    public async Task<IActionResult> UpdateFcmToken(
        [FromBody] UpdateFcmTokenRequest request)
    {
        var userId = _tenantService.GetCurrentUserId();
        var user = await _context.Users.FindAsync(userId);
        if (user == null) return NotFound();
        user.FcmToken = request.Token;
        await _context.SaveChangesAsync();
        return Ok(new { message = "FCM token updated" });
    }
}
