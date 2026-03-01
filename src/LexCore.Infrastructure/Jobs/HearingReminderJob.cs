using Hangfire;
using LexCore.Application.Interfaces;
using LexCore.Domain.Entities;
using LexCore.Domain.Enums;
using LexCore.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LexCore.Infrastructure.Jobs;

public class HearingReminderJob
{
    private readonly AppDbContext _context;
    private readonly IEmailService _emailService;
    private readonly ILogger<HearingReminderJob> _logger;

    public HearingReminderJob(AppDbContext context, IEmailService emailService, ILogger<HearingReminderJob> logger)
    {
        _context = context;
        _emailService = emailService;
        _logger = logger;
    }

    public async Task SendReminder(Guid hearingId)
    {
        try
        {
            var hearing = await _context.Hearings
                .Include(h => h.Case)
                    .ThenInclude(c => c!.CaseLawyers)
                        .ThenInclude(cl => cl.Lawyer)
                .Include(h => h.Case)
                    .ThenInclude(c => c!.CaseClients)
                        .ThenInclude(cc => cc.Client)
                .FirstOrDefaultAsync(h => h.Id == hearingId && h.DeletedAt == null);

            if (hearing == null)
            {
                _logger.LogWarning("Hearing {HearingId} not found for reminder", hearingId);
                return;
            }

            if (hearing.ReminderSent || hearing.Status != HearingStatus.Scheduled)
            {
                _logger.LogInformation("Reminder already sent or hearing not scheduled: {HearingId}", hearingId);
                return;
            }

            var caseInfo = hearing.Case!;

            // Send to all assigned lawyers
            foreach (var caseLawyer in caseInfo.CaseLawyers.Where(cl => cl.DeletedAt == null))
            {
                var lawyer = caseLawyer.Lawyer!;
                await _emailService.SendHearingReminderAsync(
                    lawyer.Email,
                    lawyer.Name,
                    caseInfo.Title,
                    hearing.HearingDate,
                    hearing.HearingTime,
                    hearing.CourtName ?? "Court"
                );

                // Create notification
                await CreateNotification(hearing.FirmId!.Value, lawyer.Id, caseInfo.Title, hearing);
            }

            // Send to all assigned clients (without internal notes)
            foreach (var caseClient in caseInfo.CaseClients.Where(cc => cc.DeletedAt == null))
            {
                var client = caseClient.Client!;
                await _emailService.SendHearingReminderAsync(
                    client.Email,
                    client.Name,
                    caseInfo.Title,
                    hearing.HearingDate,
                    hearing.HearingTime,
                    hearing.CourtName ?? "Court"
                );

                await CreateNotification(hearing.FirmId!.Value, client.Id, caseInfo.Title, hearing);
            }

            // Mark reminder as sent
            hearing.ReminderSent = true;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Hearing reminder sent successfully for {HearingId}", hearingId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send hearing reminder for {HearingId}", hearingId);
            throw;
        }
    }

    private async Task CreateNotification(Guid firmId, Guid userId, string caseTitle, Hearing hearing)
    {
        var notification = new Notification
        {
            FirmId = firmId,
            UserId = userId,
            Title = "Hearing Reminder",
            Body = $"You have a hearing for '{caseTitle}' tomorrow at {DateTime.Today.Add(hearing.HearingTime):hh:mm tt} at {hearing.CourtName ?? "Court"}",
            Type = NotificationType.HearingReminder,
            IsRead = false
        };

        await _context.Notifications.AddAsync(notification);
    }

    public static void ScheduleReminder(Guid hearingId, DateTime hearingDateTime)
    {
        var reminderTime = hearingDateTime.AddHours(-24);
        if (reminderTime > DateTime.UtcNow)
        {
            BackgroundJob.Schedule<HearingReminderJob>(
                job => job.SendReminder(hearingId),
                reminderTime);
        }
    }
}
