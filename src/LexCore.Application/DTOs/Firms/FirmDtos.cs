using LexCore.Domain.Enums;

namespace LexCore.Application.DTOs.Firms;

public class FirmDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string SubscriptionStatus { get; set; } = string.Empty;
    public string Plan { get; set; } = string.Empty;
    public string? GstNumber { get; set; }
    public string? Address { get; set; }
    public string? LogoUrl { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class UpdateFirmRequest
{
    public string? Name { get; set; }
    public string? GstNumber { get; set; }
    public string? Address { get; set; }
}
