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

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<UserDetailDto>>> GetUser(Guid id)
    {
        var currentUserId = _tenantService.GetCurrentUserId();

        var user = await _context.Users
            .Where(u => u.Id == id)
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
                AssignedCasesCount = u.CaseLawyers.Count(cl => cl.DeletedAt == null)
            })
            .FirstOrDefaultAsync();

        if (user == null)
            return NotFound(ApiResponse<UserDetailDto>.ErrorResponse("User not found", "USER_NOT_FOUND", 404));

        return Ok(ApiResponse<UserDetailDto>.SuccessResponse(user));
    }

    // PATCH /api/users/me
    // Lets any logged-in user update their own profile
    // CAT-13: email and phone changes are disabled in v1 solo-only.
    // Email is used as login identity — changing it without re-verification
    // would break auth. Phone change requires OTP re-verification flow
    // not yet implemented. Both are blocked until those flows are built.
    [HttpPatch("me")]
    public async Task<ActionResult<ApiResponse<UserDto>>> UpdateMyProfile(
        [FromBody] UpdateUserRequest request)
    {
        var userId = _tenantService.GetCurrentUserId();

        // CAT-13: block email changes
        if (!string.IsNullOrEmpty(request.Email))
            return BadRequest(ApiResponse<UserDto>.ErrorResponse(
                "Email changes are not supported in this version. Contact support.",
                "EMAIL_CHANGE_DISABLED", 400));

        // CAT-13: block phone changes
        if (request.Phone != null)
            return BadRequest(ApiResponse<UserDto>.ErrorResponse(
                "Phone changes are not supported in this version. Contact support.",
                "PHONE_CHANGE_DISABLED", 400));

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null)
            return NotFound(ApiResponse<UserDto>.ErrorResponse("User not found", "USER_NOT_FOUND", 404));

        if (!string.IsNullOrEmpty(request.Name))
            user.Name = request.Name.Trim();

        if (request.BarEnrollmentNumber != null)
            user.BarEnrollmentNumber = string.IsNullOrWhiteSpace(request.BarEnrollmentNumber)
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
        await _auditService.LogAsync("USER_PROFILE_UPDATED", "User", userId,
            ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());

        return Ok(ApiResponse<UserDto>.SuccessResponse(new UserDto
        {
            Id = user.Id,
            Name = user.Name,
            Email = user.Email,
            Role = user.Role.ToString(),
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

    // PUT /api/users/fcm-token
    // Flutter calls this on every app login to register device push token
    [HttpPut("fcm-token")]
    public async Task<IActionResult> UpdateFcmToken([FromBody] UpdateFcmTokenRequest request)
    {
        var userId = _tenantService.GetCurrentUserId();
        var user = await _context.Users.FindAsync(userId);
        if (user == null) return NotFound();
        user.FcmToken = request.Token;
        await _context.SaveChangesAsync();
        return Ok(new { message = "FCM token updated" });
    }
}
