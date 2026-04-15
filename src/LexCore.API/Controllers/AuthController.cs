using System.Security.Cryptography;
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

    // ── INDIVIDUAL LAWYER REGISTRATION ──────────────────────────────────────
    [HttpPost("register")]
    public async Task<ActionResult<ApiResponse<AuthResponse>>> Register(
        [FromBody] RegisterLawyerRequest request)
    {
        if (await _context.Users.AnyAsync(u => u.Email == request.Email))
            return BadRequest(ApiResponse<AuthResponse>.ErrorResponse(
                "Email already registered", "EMAIL_EXISTS", 400));

        var existingByPhone = await _context.Users
            .FirstOrDefaultAsync(u => u.Phone == request.Phone);

        if (existingByPhone != null)
        {
            if (existingByPhone.IsVerified)
                return BadRequest(ApiResponse<AuthResponse>.ErrorResponse(
                    "This phone number is already registered. Please login.",
                    "PHONE_EXISTS", 400));

            // Unverified duplicate — reuse and overwrite with new details
            existingByPhone.Name = request.Name;
            existingByPhone.Email = request.Email;
            existingByPhone.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password, 12);
            existingByPhone.BarEnrollmentNumber = request.BarEnrollmentNumber;
            existingByPhone.CourtType = request.CourtType;
            existingByPhone.State = request.State;
            existingByPhone.City = request.City;
            existingByPhone.IsVerified = true;
            existingByPhone.IsPhoneVerified = true;

            await _context.SaveChangesAsync();

            await SendPhoneOtpInternalAsync(existingByPhone);

            var at = _tokenService.GenerateAccessToken(
                existingByPhone.Id, existingByPhone.FirmId, existingByPhone.Email,
                existingByPhone.Role.ToString(), existingByPhone.Name);
            var rt = _tokenService.GenerateRefreshToken();

            existingByPhone.RefreshToken = rt;
            existingByPhone.RefreshTokenExpiry = DateTime.UtcNow.AddDays(7);
            await _context.SaveChangesAsync();

            return Ok(ApiResponse<AuthResponse>.SuccessResponse(new AuthResponse
            {
                AccessToken = at,
                RefreshToken = rt,
                ExpiresAt = DateTime.UtcNow.AddHours(24),
                User = MapUserToDto(existingByPhone, null)
            }, "Registration successful. Please verify your phone number."));
        }

        var user = new User
        {
            Name = request.Name,
            Email = request.Email,
            Phone = request.Phone,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password, 12),
            Role = UserRole.Lawyer,
            IsVerified = true,
            IsPhoneVerified = true,
            BarEnrollmentNumber = request.BarEnrollmentNumber,
            CourtType = request.CourtType,
            State = request.State,
            City = request.City,
            CreatedAt = DateTime.UtcNow,
        };

        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();

        // Send phone OTP
        await SendPhoneOtpInternalAsync(user);

        var accessToken = _tokenService.GenerateAccessToken(
            user.Id, user.FirmId, user.Email, user.Role.ToString(), user.Name);
        var refreshToken = _tokenService.GenerateRefreshToken();

        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiry = DateTime.UtcNow.AddDays(7);
        await _context.SaveChangesAsync();

        return Ok(ApiResponse<AuthResponse>.SuccessResponse(new AuthResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresAt = DateTime.UtcNow.AddHours(24),
            User = MapUserToDto(user, null)
        }, "Registration successful. Please verify your phone number."));
    }

    // ── SEND OTP ─────────────────────────────────────────────────────────────
    [HttpPost("send-otp")]
    public async Task<ActionResult<ApiResponse<object>>> SendOtp(
        [FromBody] SendOtpRequest request)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Phone == request.Phone);

        if (user == null)
            return NotFound(ApiResponse<object>.ErrorResponse(
                "Phone number not found", "NOT_FOUND", 404));

        await SendPhoneOtpInternalAsync(user);
        return Ok(ApiResponse<object>.SuccessResponse(null!, "OTP sent successfully"));
    }

    // ── VERIFY OTP ───────────────────────────────────────────────────────────
    [HttpPost("verify-otp")]
    public async Task<ActionResult<ApiResponse<object>>> VerifyOtp(
        [FromBody] VerifyOtpRequest request)
    {
        var user = await _context.Users
            .Where(u => u.Phone == request.Phone)
            .OrderByDescending(u => u.PhoneOtpExpiry)
            .FirstOrDefaultAsync();

        if (user == null)
            return NotFound(ApiResponse<object>.ErrorResponse(
                "Phone number not found", "NOT_FOUND", 404));

        // Check OTP lockout — 5 failed attempts = 15 minute lockout
        if (user.OtpLockoutEnd.HasValue && user.OtpLockoutEnd.Value > DateTime.UtcNow)
        {
            var remaining = (int)(user.OtpLockoutEnd.Value - DateTime.UtcNow).TotalMinutes + 1;
            return BadRequest(ApiResponse<object>.ErrorResponse(
                $"Too many failed attempts. Try again in {remaining} minute(s).",
                "OTP_LOCKED", 400));
        }

        // Wrong OTP or expired
        if (user.PhoneOtp != request.Otp || user.PhoneOtpExpiry < DateTime.UtcNow)
        {
            user.OtpFailedAttempts += 1;

            if (user.OtpFailedAttempts >= 5)
            {
                user.OtpLockoutEnd = DateTime.UtcNow.AddMinutes(15);
                user.OtpFailedAttempts = 0;
                await _context.SaveChangesAsync();
                return BadRequest(ApiResponse<object>.ErrorResponse(
                    "Too many failed attempts. Please request a new OTP after 15 minutes.",
                    "OTP_LOCKED", 400));
            }

            await _context.SaveChangesAsync();
            return BadRequest(ApiResponse<object>.ErrorResponse(
                $"Invalid or expired OTP. {5 - user.OtpFailedAttempts} attempt(s) remaining.",
                "INVALID_OTP", 400));
        }

        // Correct OTP — reset counters
        user.IsPhoneVerified = true;
        user.IsVerified = true;
        user.PhoneOtp = null;
        user.PhoneOtpExpiry = null;
        user.OtpFailedAttempts = 0;
        user.OtpLockoutEnd = null;
        await _context.SaveChangesAsync();

        return Ok(ApiResponse<object>.SuccessResponse(null!, "Phone verified successfully"));
    }

    // ── LOGIN ────────────────────────────────────────────────────────────────
    [HttpPost("login")]
    public async Task<ActionResult<ApiResponse<AuthResponse>>> Login(
        [FromBody] LoginRequest request)
    {
        var user = await _context.Users
            .Include(u => u.Firm)
            .FirstOrDefaultAsync(u => u.Email == request.Email);

        // Generic delay to prevent timing attacks
        if (user == null)
        {
            await Task.Delay(200);
            return Unauthorized(ApiResponse<AuthResponse>.ErrorResponse(
                "Invalid email or password", "INVALID_CREDENTIALS", 401));
        }

        // Check if account is locked out
        if (user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTime.UtcNow)
        {
            var remaining = (int)(user.LockoutEnd.Value - DateTime.UtcNow).TotalMinutes + 1;
            return Unauthorized(ApiResponse<AuthResponse>.ErrorResponse(
                $"Account locked. Try again in {remaining} minute(s).",
                "ACCOUNT_LOCKED", 401));
        }

        // Verify password
        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            // Increment failed attempts
            user.FailedLoginAttempts += 1;

            // Lock after 5 failed attempts — 15 minute lockout
            if (user.FailedLoginAttempts >= 5)
            {
                user.LockoutEnd = DateTime.UtcNow.AddMinutes(15);
                user.FailedLoginAttempts = 0;
                await _context.SaveChangesAsync();
                return Unauthorized(ApiResponse<AuthResponse>.ErrorResponse(
                    "Too many failed attempts. Account locked for 15 minutes.",
                    "ACCOUNT_LOCKED", 401));
            }

            await _context.SaveChangesAsync();
            return Unauthorized(ApiResponse<AuthResponse>.ErrorResponse(
                "Invalid email or password", "INVALID_CREDENTIALS", 401));
        }

        // Successful login — reset failed attempts and lockout
        user.FailedLoginAttempts = 0;
        user.LockoutEnd = null;

        // OTP verification temporarily bypassed — SMS provider not yet configured
        // TODO: Re-enable when MSG91 or Fast2SMS is integrated
        // if (!user.IsVerified)
        //     return Unauthorized(ApiResponse<AuthResponse>.ErrorResponse(
        //         "Please verify your phone number before logging in.",
        //         "PHONE_NOT_VERIFIED", 401));

        var accessToken = _tokenService.GenerateAccessToken(
            user.Id, user.FirmId, user.Email, user.Role.ToString(), user.Name);
        var refreshToken = _tokenService.GenerateRefreshToken();

        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiry = DateTime.UtcNow.AddDays(7);
        user.LastLogin = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return Ok(ApiResponse<AuthResponse>.SuccessResponse(new AuthResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresAt = DateTime.UtcNow.AddHours(24),
            User = MapUserToDto(user, user.Firm?.Name)
        }, "Login successful"));
    }

    // ── REFRESH TOKEN ────────────────────────────────────────────────────────
    [HttpPost("refresh")]
    public async Task<ActionResult<ApiResponse<AuthResponse>>> RefreshToken(
        [FromBody] RefreshTokenRequest request)
    {
        var user = await _context.Users
            .Include(u => u.Firm)
            .FirstOrDefaultAsync(u => u.RefreshToken == request.RefreshToken);

        if (user == null || user.RefreshTokenExpiry < DateTime.UtcNow)
            return Unauthorized(ApiResponse<AuthResponse>.ErrorResponse(
                "Invalid or expired refresh token", "INVALID_REFRESH_TOKEN", 401));

        var accessToken = _tokenService.GenerateAccessToken(
            user.Id, user.FirmId, user.Email, user.Role.ToString(), user.Name);
        var newRefreshToken = _tokenService.GenerateRefreshToken();

        user.RefreshToken = newRefreshToken;
        user.RefreshTokenExpiry = DateTime.UtcNow.AddDays(7);
        await _context.SaveChangesAsync();

        return Ok(ApiResponse<AuthResponse>.SuccessResponse(new AuthResponse
        {
            AccessToken = accessToken,
            RefreshToken = newRefreshToken,
            ExpiresAt = DateTime.UtcNow.AddHours(24),
            User = MapUserToDto(user, user.Firm?.Name)
        }, "Token refreshed successfully"));
    }

    // ── LOGOUT ───────────────────────────────────────────────────────────────
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

    // ── FORGOT PASSWORD ──────────────────────────────────────────────────────
    [HttpPost("forgot-password")]
    public async Task<ActionResult<ApiResponse<object>>> ForgotPassword(
        [FromBody] ForgotPasswordRequest request)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Email == request.Email);

        if (user != null)
        {
            var resetToken = Guid.NewGuid().ToString();
            user.PasswordResetToken = resetToken;
            user.PasswordResetTokenExpiry = DateTime.UtcNow.AddHours(1);
            await _context.SaveChangesAsync();

            try
            {
                await _emailService.SendPasswordResetEmailAsync(
                    user.Email, user.Name, resetToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send password reset email to {Email}",
                    user.Email);
            }
        }

        // Always return success (security best practice — don't reveal if email exists)
        return Ok(ApiResponse<object>.SuccessResponse(null!,
            "If an account exists with this email, a password reset link has been sent."));
    }

    // ── RESET PASSWORD ───────────────────────────────────────────────────────
    [HttpPost("reset-password")]
    public async Task<ActionResult<ApiResponse<object>>> ResetPassword(
        [FromBody] ResetPasswordRequest request)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u =>
            u.PasswordResetToken == request.Token &&
            u.PasswordResetTokenExpiry > DateTime.UtcNow);

        if (user == null)
            return BadRequest(ApiResponse<object>.ErrorResponse(
                "Invalid or expired reset token", "INVALID_TOKEN", 400));

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword, 12);
        user.PasswordResetToken = null;
        user.PasswordResetTokenExpiry = null;
        user.RefreshToken = null;
        user.RefreshTokenExpiry = null;
        await _context.SaveChangesAsync();

        return Ok(ApiResponse<object>.SuccessResponse(null!, "Password reset successfully"));
    }

    // ── INVITE USER ──────────────────────────────────────────────────────────
    [Authorize(Policy = "FirmAdmin")]
    [HttpPost("invite")]
    public async Task<ActionResult<ApiResponse<object>>> InviteUser([FromBody] InviteUserRequest request)
    {
        var firmId = _tenantService.GetCurrentFirmId();
        var currentUserId = _tenantService.GetCurrentUserId();

        if (await _context.Users.AnyAsync(u => u.Email == request.Email))
            return BadRequest(ApiResponse<object>.ErrorResponse("Email already registered", "EMAIL_EXISTS", 400));

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

    // ── ACCEPT INVITE ────────────────────────────────────────────────────────
    [HttpPost("accept-invite")]
    public async Task<ActionResult<ApiResponse<AuthResponse>>> AcceptInvite([FromBody] AcceptInviteRequest request)
    {
        var user = await _context.Users
            .Include(u => u.Firm)
            .FirstOrDefaultAsync(u =>
                u.InviteToken == request.Token &&
                u.InviteTokenExpiry > DateTime.UtcNow);

        if (user == null)
            return BadRequest(ApiResponse<AuthResponse>.ErrorResponse("Invalid or expired invitation token", "INVALID_TOKEN", 400));

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
            ExpiresAt = DateTime.UtcNow.AddHours(24),
            User = MapUserToDto(user, user.Firm?.Name)
        }, "Account activated successfully"));
    }

    // ── HELPERS ──────────────────────────────────────────────────────────────
    private async Task SendPhoneOtpInternalAsync(User user)
    {
        var otp = RandomNumberGenerator.GetInt32(100000, 1000000)
            .ToString();
        user.PhoneOtp = otp;
        user.PhoneOtpExpiry = DateTime.UtcNow.AddMinutes(10);
        // Reset brute force counters on fresh OTP send
        user.OtpFailedAttempts = 0;
        user.OtpLockoutEnd = null;
        await _context.SaveChangesAsync();

        // TODO: Integrate with SMS provider (Twilio / MSG91 / Fast2SMS)
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
            IsPhoneVerified = user.IsPhoneVerified,
            Phone = user.Phone,
            BarEnrollmentNumber = user.BarEnrollmentNumber,
            CourtType = user.CourtType,
            State = user.State,
            City = user.City,
            LastLogin = user.LastLogin,
            CreatedAt = user.CreatedAt
        };
    }
}
