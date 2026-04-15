namespace LexCore.Application.Interfaces;

public interface IWhatsAppService
{
    /// <summary>Send WhatsApp via Fast2SMS. Skip silently if number is null/empty.</summary>
    Task SendAsync(string? phoneNumber, string message);
}
