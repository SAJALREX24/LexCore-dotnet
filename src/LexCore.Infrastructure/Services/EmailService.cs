using LexCore.Application.Interfaces;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;

namespace LexCore.Infrastructure.Services;

public class EmailService : IEmailService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SendEmailAsync(string to, string subject, string body, bool isHtml = true)
    {
        try
        {
            var email = new MimeMessage();
            email.From.Add(new MailboxAddress(
                _configuration["Email:FromName"] ?? "LexCore",
                _configuration["Email:FromAddress"]));
            email.To.Add(MailboxAddress.Parse(to));
            email.Subject = subject;

            var builder = new BodyBuilder();
            if (isHtml)
                builder.HtmlBody = body;
            else
                builder.TextBody = body;

            email.Body = builder.ToMessageBody();

            using var smtp = new SmtpClient();
            await smtp.ConnectAsync(
                _configuration["Email:SmtpHost"],
                int.Parse(_configuration["Email:SmtpPort"] ?? "587"),
                SecureSocketOptions.StartTls);

            await smtp.AuthenticateAsync(
                _configuration["Email:SmtpUser"],
                _configuration["Email:SmtpPassword"]);

            await smtp.SendAsync(email);
            await smtp.DisconnectAsync(true);

            _logger.LogInformation("Email sent successfully to {To}", to);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {To}", to);
            throw;
        }
    }

    public async Task SendVerificationEmailAsync(string to, string name, string token)
    {
        var baseUrl = _configuration["App:BaseUrl"];
        var verifyUrl = $"{baseUrl}/api/auth/verify-email?token={token}";

        var body = $@"
        <html>
        <body style='font-family: Arial, sans-serif;'>
            <h2>Welcome to LexCore, {name}!</h2>
            <p>Please verify your email address by clicking the link below:</p>
            <p><a href='{verifyUrl}' style='background-color: #4CAF50; color: white; padding: 10px 20px; text-decoration: none; border-radius: 5px;'>Verify Email</a></p>
            <p>Or copy and paste this link in your browser:</p>
            <p>{verifyUrl}</p>
            <p>This link will expire in 24 hours.</p>
            <br/>
            <p>Best regards,<br/>The LexCore Team</p>
        </body>
        </html>";

        await SendEmailAsync(to, "Verify Your LexCore Account", body);
    }

    public async Task SendPasswordResetEmailAsync(string to, string name, string token)
    {
        var baseUrl = _configuration["App:BaseUrl"];
        var resetUrl = $"{baseUrl}/reset-password?token={token}";

        var body = $@"
        <html>
        <body style='font-family: Arial, sans-serif;'>
            <h2>Password Reset Request</h2>
            <p>Hi {name},</p>
            <p>We received a request to reset your password. Click the link below to set a new password:</p>
            <p><a href='{resetUrl}' style='background-color: #2196F3; color: white; padding: 10px 20px; text-decoration: none; border-radius: 5px;'>Reset Password</a></p>
            <p>Or copy and paste this link in your browser:</p>
            <p>{resetUrl}</p>
            <p>This link will expire in 1 hour.</p>
            <p>If you didn't request this, please ignore this email.</p>
            <br/>
            <p>Best regards,<br/>The LexCore Team</p>
        </body>
        </html>";

        await SendEmailAsync(to, "Reset Your LexCore Password", body);
    }

    public async Task SendInviteEmailAsync(string to, string firmName, string inviterName, string token, string role)
    {
        var baseUrl = _configuration["App:BaseUrl"];
        var inviteUrl = $"{baseUrl}/accept-invite?token={token}";

        var body = $@"
        <html>
        <body style='font-family: Arial, sans-serif;'>
            <h2>You're Invited to Join {firmName} on LexCore!</h2>
            <p>{inviterName} has invited you to join their law firm as a {role}.</p>
            <p>Click the link below to accept the invitation and set up your account:</p>
            <p><a href='{inviteUrl}' style='background-color: #9C27B0; color: white; padding: 10px 20px; text-decoration: none; border-radius: 5px;'>Accept Invitation</a></p>
            <p>Or copy and paste this link in your browser:</p>
            <p>{inviteUrl}</p>
            <p>This invitation will expire in 7 days.</p>
            <br/>
            <p>Best regards,<br/>The LexCore Team</p>
        </body>
        </html>";

        await SendEmailAsync(to, $"Invitation to Join {firmName} on LexCore", body);
    }

    public async Task SendHearingReminderAsync(string to, string name, string caseTitle, DateTime hearingDate, TimeSpan hearingTime, string courtName)
    {
        var body = $@"
        <html>
        <body style='font-family: Arial, sans-serif;'>
            <h2>Hearing Reminder</h2>
            <p>Hi {name},</p>
            <p>This is a reminder for your upcoming hearing:</p>
            <table style='border-collapse: collapse; width: 100%; max-width: 500px;'>
                <tr>
                    <td style='padding: 8px; border: 1px solid #ddd;'><strong>Case:</strong></td>
                    <td style='padding: 8px; border: 1px solid #ddd;'>{caseTitle}</td>
                </tr>
                <tr>
                    <td style='padding: 8px; border: 1px solid #ddd;'><strong>Date:</strong></td>
                    <td style='padding: 8px; border: 1px solid #ddd;'>{hearingDate:dddd, MMMM dd, yyyy}</td>
                </tr>
                <tr>
                    <td style='padding: 8px; border: 1px solid #ddd;'><strong>Time:</strong></td>
                    <td style='padding: 8px; border: 1px solid #ddd;'>{DateTime.Today.Add(hearingTime):hh:mm tt}</td>
                </tr>
                <tr>
                    <td style='padding: 8px; border: 1px solid #ddd;'><strong>Court:</strong></td>
                    <td style='padding: 8px; border: 1px solid #ddd;'>{courtName}</td>
                </tr>
            </table>
            <p>Please ensure you are prepared and arrive on time.</p>
            <br/>
            <p>Best regards,<br/>The LexCore Team</p>
        </body>
        </html>";

        await SendEmailAsync(to, $"Hearing Reminder: {caseTitle}", body);
    }

    public async Task SendInvoiceEmailAsync(string to, string name, string invoiceNumber, decimal amount, DateTime dueDate, byte[] pdfAttachment)
    {
        try
        {
            var email = new MimeMessage();
            email.From.Add(new MailboxAddress(
                _configuration["Email:FromName"] ?? "LexCore",
                _configuration["Email:FromAddress"]));
            email.To.Add(MailboxAddress.Parse(to));
            email.Subject = $"Invoice {invoiceNumber} from LexCore";

            var builder = new BodyBuilder();
            builder.HtmlBody = $@"
            <html>
            <body style='font-family: Arial, sans-serif;'>
                <h2>Invoice {invoiceNumber}</h2>
                <p>Dear {name},</p>
                <p>Please find attached your invoice with the following details:</p>
                <table style='border-collapse: collapse;'>
                    <tr>
                        <td style='padding: 8px;'><strong>Invoice Number:</strong></td>
                        <td style='padding: 8px;'>{invoiceNumber}</td>
                    </tr>
                    <tr>
                        <td style='padding: 8px;'><strong>Amount:</strong></td>
                        <td style='padding: 8px;'>₹{amount:N2}</td>
                    </tr>
                    <tr>
                        <td style='padding: 8px;'><strong>Due Date:</strong></td>
                        <td style='padding: 8px;'>{dueDate:MMMM dd, yyyy}</td>
                    </tr>
                </table>
                <p>Please make the payment by the due date to avoid any late fees.</p>
                <br/>
                <p>Best regards,<br/>The LexCore Team</p>
            </body>
            </html>";

            builder.Attachments.Add($"Invoice_{invoiceNumber}.pdf", pdfAttachment, new ContentType("application", "pdf"));
            email.Body = builder.ToMessageBody();

            using var smtp = new SmtpClient();
            await smtp.ConnectAsync(
                _configuration["Email:SmtpHost"],
                int.Parse(_configuration["Email:SmtpPort"] ?? "587"),
                SecureSocketOptions.StartTls);

            await smtp.AuthenticateAsync(
                _configuration["Email:SmtpUser"],
                _configuration["Email:SmtpPassword"]);

            await smtp.SendAsync(email);
            await smtp.DisconnectAsync(true);

            _logger.LogInformation("Invoice email sent to {To}", to);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send invoice email to {To}", to);
            throw;
        }
    }
}
