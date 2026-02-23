using System.Text.RegularExpressions;
using BCrypt.Net;
using LexCore.Application.DTOs;
using LexCore.Application.DTOs.Auth;
using LexCore.Application.Interfaces;
using LexCore.Domain.Entities;
using LexCore.Domain.Enums;
using LexCore.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LexCore.API.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ITokenService _tokenService;
    private readonly IEmailService _emailService;
    private readonly IAuditService _auditService;
    private readonly ITenantService _tenantService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        AppDbContext context,
        ITokenService tokenService,
        IEmailService emailService,
        IAuditService auditService,
        ITenantService tenantService,
        ILogger<AuthController> logger)
    {
        _context = context;
        _tokenService = tokenService;
        _emailService = emailService;
        _auditService = auditService;
        _tenantService = tenantService;
        _logger = logger;
    }

    [HttpPost("register-firm")]
    public async Task<ActionResult<ApiResponse<AuthResponse>>> RegisterFirm([FromBody] RegisterFirmRequest request)
    {
        if (await _context.Users.AnyAsync(u => u.Email == request.Email))
        {
            return BadRequest(ApiResponse<AuthResponse>.ErrorResponse("Email already registered", "EMAIL_EXISTS", 400));
        }

        var slug = GenerateSlug(request.FirmName);
        var existingSlug = await _context.Firms.AnyAsync(f => f.Slug == slug);
        if (existingSlug)
        {
            slug = $"{slug}-{Guid.NewGuid().ToString()[..8]}";
        }

        var firm = new Firm
        {
            Name = request.FirmName,
            Slug = slug,
            GstNumber = request.GstNumber,
            Address = request.Address,
            SubscriptionStatus = SubscriptionStatus.Active,
            Plan = SubscriptionPlan.Trial
        };

        var verificationToken = Guid.NewGuid().ToString();
        var user = new User
        {
            FirmId = firm.Id,
            Name = request.AdminName,
            Email = request.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password, 12),
            Role = UserRole.FirmAdmin,
            IsVerified = false,
            VerificationToken = verificationToken,
            VerificationTokenExpiry = DateTime.UtcNow.AddHours(24)
        };

        firm.OwnerId = user.Id;

        // Create trial subscription
        var subscription = new Subscription
        {
            FirmId = firm.Id,
            Plan = SubscriptionPlan.Trial,
            Status = SubscriptionStatus.Active,
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddDays(14)
        };

        await _context.Firms.AddAsync(firm);
        await _context.Users.AddAsync(user);
        await _context.Subscriptions.AddAsync(subscription);
        await _context.SaveChangesAsync();

        try
        {
            await _emailService.SendVerificationEmailAsync(user.Email, user.Name, verificationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send verification email to {Email}", user.Email);
        }

        var accessToken = _tokenService.GenerateAccessToken(user.Id, firm.Id, user.Email, user.Role.ToString(), user.Name);
        var refreshToken = _tokenService.GenerateRefreshToken();

        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiry = DateTime.UtcNow.AddDays(7);
        await _context.SaveChangesAsync();

        return Ok(ApiResponse<AuthResponse>.SuccessResponse(new AuthResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresAt = DateTime.UtcNow.AddMinutes(15),
            User = MapUserToDto(user, firm.Name)
        }, "Firm registered successfully. Please verify your email."));
    }

    [HttpPost("login")]
    public async Task<ActionResult<ApiResponse<AuthResponse>>> Login([FromBody] LoginRequest request)
    {
        var user = await _context.Users
            .Include(u => u.Firm)
            .FirstOrDefaultAsync(u => u.Email == request.Email);

        if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            return Unauthorized(ApiResponse<AuthResponse>.ErrorResponse("Invalid email or password", "INVALID_CREDENTIALS", 401));
        }

        var accessToken = _tokenService.GenerateAccessToken(user.Id, user.FirmId, user.Email, user.Role.ToString(), user.Name);
        var refreshToken = _tokenService.GenerateRefreshToken();

        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiry = DateTime.UtcNow.AddDays(7);
        user.LastLogin = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return Ok(ApiResponse<AuthResponse>.SuccessResponse(new AuthResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresAt = DateTime.UtcNow.AddMinutes(15),
            User = MapUserToDto(user, user.Firm?.Name)
        }, "Login successful"));
    }

    [HttpPost("refresh")]
    public async Task<ActionResult<ApiResponse<AuthResponse>>> RefreshToken([FromBody] RefreshTokenRequest request)
    {
        var user = await _context.Users
            .Include(u => u.Firm)
            .FirstOrDefaultAsync(u => u.RefreshToken == request.RefreshToken);

        if (user == null || user.RefreshTokenExpiry < DateTime.UtcNow)
        {
            return Unauthorized(ApiResponse<AuthResponse>.ErrorResponse("Invalid or expired refresh token", "INVALID_REFRESH_TOKEN", 401));
        }

        var accessToken = _tokenService.GenerateAccessToken(user.Id, user.FirmId, user.Email, user.Role.ToString(), user.Name);
        var newRefreshToken = _tokenService.GenerateRefreshToken();

        user.RefreshToken = newRefreshToken;
        user.RefreshTokenExpiry = DateTime.UtcNow.AddDays(7);
        await _context.SaveChangesAsync();

        return Ok(ApiResponse<AuthResponse>.SuccessResponse(new AuthResponse
        {
            AccessToken = accessToken,
            RefreshToken = newRefreshToken,
            ExpiresAt = DateTime.UtcNow.AddMinutes(15),
            User = MapUserToDto(user, user.Firm?.Name)
        }, "Token refreshed successfully"));
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<ActionResult<ApiResponse<object>>> Logout()
    {
        var userId = _tenantService.GetCurrentUserId();
        var user = await _context.Users.FindAsync(userId);

        if (user != null)
        {
            user.RefreshToken = null;
            user.RefreshTokenExpiry = null;
            await _context.SaveChangesAsync();
        }

        return Ok(ApiResponse<object>.SuccessResponse(null!, "Logged out successfully"));
    }

    [HttpPost("forgot-password")]
    public async Task<ActionResult<ApiResponse<object>>> ForgotPassword([FromBody] ForgotPasswordRequest request)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email);

        if (user != null)
        {
            var resetToken = Guid.NewGuid().ToString();
            user.PasswordResetToken = resetToken;
            user.PasswordResetTokenExpiry = DateTime.UtcNow.AddHours(1);
            await _context.SaveChangesAsync();

            try
            {
                await _emailService.SendPasswordResetEmailAsync(user.Email, user.Name, resetToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send password reset email to {Email}", user.Email);
            }
        }

        return Ok(ApiResponse<object>.SuccessResponse(null!, "If an account exists with this email, a password reset link has been sent."));
    }

    [HttpPost("reset-password")]
    public async Task<ActionResult<ApiResponse<object>>> ResetPassword([FromBody] ResetPasswordRequest request)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => 
            u.PasswordResetToken == request.Token && 
            u.PasswordResetTokenExpiry > DateTime.UtcNow);

        if (user == null)
        {
            return BadRequest(ApiResponse<object>.ErrorResponse("Invalid or expired reset token", "INVALID_TOKEN", 400));
        }

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword, 12);
        user.PasswordResetToken = null;
        user.PasswordResetTokenExpiry = null;
        user.RefreshToken = null;
        user.RefreshTokenExpiry = null;
        await _context.SaveChangesAsync();

        return Ok(ApiResponse<object>.SuccessResponse(null!, "Password reset successfully"));
    }

    [HttpGet("verify-email")]
    public async Task<ActionResult<ApiResponse<object>>> VerifyEmail([FromQuery] string token)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => 
            u.VerificationToken == token && 
            u.VerificationTokenExpiry > DateTime.UtcNow);

        if (user == null)
        {
            return BadRequest(ApiResponse<object>.ErrorResponse("Invalid or expired verification token", "INVALID_TOKEN", 400));
        }

        user.IsVerified = true;
        user.VerificationToken = null;
        user.VerificationTokenExpiry = null;
        await _context.SaveChangesAsync();

        return Ok(ApiResponse<object>.SuccessResponse(null!, "Email verified successfully"));
    }

    [Authorize(Policy = "FirmAdmin")]
    [HttpPost("invite")]
    public async Task<ActionResult<ApiResponse<object>>> InviteUser([FromBody] InviteUserRequest request)
    {
        var firmId = _tenantService.GetCurrentFirmId();
        var currentUserId = _tenantService.GetCurrentUserId();

        if (await _context.Users.AnyAsync(u => u.Email == request.Email))
        {
            return BadRequest(ApiResponse<object>.ErrorResponse("Email already registered", "EMAIL_EXISTS", 400));
        }

        var firm = await _context.Firms.FindAsync(firmId);
        var inviter = await _context.Users.FindAsync(currentUserId);

        var inviteToken = Guid.NewGuid().ToString();
        var user = new User
        {
            FirmId = firmId,
            Name = request.Name,
            Email = request.Email,
            PasswordHash = "",
            Role = request.Role,
            IsVerified = false,
            InviteToken = inviteToken,
            InviteTokenExpiry = DateTime.UtcNow.AddDays(7)
        };

        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();

        try
        {
            await _emailService.SendInviteEmailAsync(
                request.Email, 
                firm!.Name, 
                inviter!.Name, 
                inviteToken, 
                request.Role.ToString());
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send invite email to {Email}", request.Email);
        }

        await _auditService.LogAsync("USER_INVITED", "User", user.Id, ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());

        return Ok(ApiResponse<object>.SuccessResponse(null!, "Invitation sent successfully"));
    }

    [HttpPost("accept-invite")]
    public async Task<ActionResult<ApiResponse<AuthResponse>>> AcceptInvite([FromBody] AcceptInviteRequest request)
    {
        var user = await _context.Users
            .Include(u => u.Firm)
            .FirstOrDefaultAsync(u => 
                u.InviteToken == request.Token && 
                u.InviteTokenExpiry > DateTime.UtcNow);

        if (user == null)
        {
            return BadRequest(ApiResponse<AuthResponse>.ErrorResponse("Invalid or expired invitation token", "INVALID_TOKEN", 400));
        }

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password, 12);
        user.IsVerified = true;
        user.InviteToken = null;
        user.InviteTokenExpiry = null;

        var accessToken = _tokenService.GenerateAccessToken(user.Id, user.FirmId, user.Email, user.Role.ToString(), user.Name);
        var refreshToken = _tokenService.GenerateRefreshToken();

        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiry = DateTime.UtcNow.AddDays(7);
        await _context.SaveChangesAsync();

        return Ok(ApiResponse<AuthResponse>.SuccessResponse(new AuthResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresAt = DateTime.UtcNow.AddMinutes(15),
            User = MapUserToDto(user, user.Firm?.Name)
        }, "Account activated successfully"));
    }

    private static string GenerateSlug(string name)
    {
        var slug = name.ToLower().Trim();
        slug = Regex.Replace(slug, @"[^a-z0-9\s-]", "");
        slug = Regex.Replace(slug, @"\s+", "-");
        slug = Regex.Replace(slug, @"-+", "-");
        return slug.Trim('-');
    }

    private static UserDto MapUserToDto(User user, string? firmName)
    {
        return new UserDto
        {
            Id = user.Id,
            Name = user.Name,
            Email = user.Email,
            Role = user.Role.ToString(),
            FirmId = user.FirmId,
            FirmName = firmName,
            IsVerified = user.IsVerified,
            LastLogin = user.LastLogin,
            CreatedAt = user.CreatedAt
        };
    }
}
