namespace LexCore.Application.Interfaces;

public interface IEmailService
{
    Task SendEmailAsync(string to, string subject, string body, bool isHtml = true);
    Task SendVerificationEmailAsync(string to, string name, string token);
    Task SendPasswordResetEmailAsync(string to, string name, string token);
    Task SendHearingReminderAsync(string to, string name, string caseTitle, DateTime hearingDate, TimeSpan hearingTime, string courtName);
    Task SendInvoiceEmailAsync(string to, string name, string invoiceNumber, decimal amount, DateTime dueDate, byte[] pdfAttachment);
}
