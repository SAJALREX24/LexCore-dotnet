using System.ComponentModel.DataAnnotations;

namespace LexCore.Application.DTOs.Auth;

public class RegisterLawyerRequest
{
    [Required]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [Phone]
    public string Phone { get; set; } = string.Empty;

    [Required]
    [MinLength(8)]
    public string Password { get; set; } = string.Empty;

    public string? BarEnrollmentNumber { get; set; }
    public string? CourtType { get; set; }
    public string? State { get; set; }
    public string? City { get; set; }
}

public class SendOtpRequest
{
    [Required]
    [Phone]
    public string Phone { get; set; } = string.Empty;
}

public class VerifyOtpRequest
{
    [Required]
    [Phone]
    public string Phone { get; set; } = string.Empty;

    [Required]
    [StringLength(6, MinimumLength = 6)]
    public string Otp { get; set; } = string.Empty;
}

public class LoginRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}

public class RefreshTokenRequest
{
    [Required]
    public string RefreshToken { get; set; } = string.Empty;
}

public class ForgotPasswordRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;
}

public class ResetPasswordRequest
{
    [Required]
    public string Token { get; set; } = string.Empty;

    [Required]
    [MinLength(8)]
    public string NewPassword { get; set; } = string.Empty;
}

public class AuthResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public UserDto User { get; set; } = new();
}

public class UserDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public bool IsVerified { get; set; }
    public bool IsPhoneVerified { get; set; }
    public string? Phone { get; set; }
    public string? BarEnrollmentNumber { get; set; }
    public string? CourtType { get; set; }
    public string? State { get; set; }
    public string? City { get; set; }
    public DateTime? LastLogin { get; set; }
    public DateTime CreatedAt { get; set; }
}
