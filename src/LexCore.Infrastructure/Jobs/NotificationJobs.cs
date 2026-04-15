using Hangfire;
using LexCore.Application.Interfaces;
using LexCore.Domain.Enums;
using LexCore.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LexCore.Infrastructure.Jobs;

public class NotificationJobs
{
    private readonly AppDbContext _context;
    private readonly INotificationService _notificationService;
    private readonly ILogger<NotificationJobs> _logger;
    private const int BatchSize = 100;

    public NotificationJobs(
        AppDbContext context,
        INotificationService notificationService,
        ILogger<NotificationJobs> logger)
    {
        _context = context;
        _notificationService = notificationService;
        _logger = logger;
    }

    // Runs every night at 8 PM IST
    // Reminds lawyers about tomorrow's hearings + sends WhatsApp to clients
    [DisableConcurrentExecution(timeoutInSeconds: 300)]
    public async Task SendEveningHearingReminders()
    {
        var tomorrow = DateTime.UtcNow.Date.AddDays(1);
        int page = 0, processedCount = 0;

        while (true)
        {
            var batch = await _context.Hearings
                .Include(h => h.Case)
                .Where(h => h.HearingDate.Date == tomorrow
                         && h.Status == HearingStatus.Scheduled
                         && h.DeletedAt == null)
                .Skip(page * BatchSize)
                .Take(BatchSize)
                .ToListAsync();

            if (batch.Count == 0)
                break;

            foreach (var hearing in batch)
            {
                try
                {
                    var caseLawyer = await _context.CaseLawyers
                        .FirstOrDefaultAsync(cl => cl.CaseId == hearing.CaseId && cl.DeletedAt == null);

                    if (caseLawyer == null)
                        continue;

                    var hearingTime = DateTime.Today.Add(hearing.HearingTime);

                    await _notificationService.NotifyLawyerAsync(
                        lawyerId: caseLawyer.LawyerId,
                        title: "Hearing Tomorrow ⚖️",
                        body: $"{hearing.Case!.Title} — {hearing.CourtName ?? "Court"} at {hearingTime:hh:mm tt}",
                        type: NotificationType.HearingReminder,
                        caseId: hearing.CaseId,
                        hearingId: hearing.Id);

                    if (hearing.Case.NotifyClientOnHearing == true
                        && !string.IsNullOrWhiteSpace(hearing.Case.ClientWhatsApp))
                    {
                        var msg = $"Dear {hearing.Case.ClientName ?? "Sir/Madam"}, " +
                                  $"your hearing for '{hearing.Case.Title}' is tomorrow " +
                                  $"at {hearingTime:hh:mm tt}, {hearing.CourtName ?? "Court"}. " +
                                  "Please be present. — Your Lawyer";

                        await _notificationService.NotifyClientWhatsAppAsync(hearing.Case.ClientWhatsApp, msg);
                    }

                    processedCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "SendEveningHearingReminders failed for HearingId {HearingId}", hearing.Id);
                }
            }

            page++;
        }

        _logger.LogInformation("SendEveningHearingReminders completed. Processed {Count} hearings.", processedCount);
    }

    // Runs every morning at 7 AM IST
    // Sends one summary push per lawyer listing today's hearings
    [DisableConcurrentExecution(timeoutInSeconds: 300)]
    public async Task SendMorningHearingList()
    {
        var today = DateTime.UtcNow.Date;
        var lawyerHearings = new Dictionary<Guid, List<string>>();
        int page = 0;

        while (true)
        {
            var batch = await _context.Hearings
                .Include(h => h.Case)
                .Where(h => h.HearingDate.Date == today
                         && h.Status == HearingStatus.Scheduled
                         && h.DeletedAt == null)
                .OrderBy(h => h.HearingDate)
                .Skip(page * BatchSize)
                .Take(BatchSize)
                .ToListAsync();

            if (batch.Count == 0)
                break;

            foreach (var hearing in batch)
            {
                var caseLawyer = await _context.CaseLawyers
                    .FirstOrDefaultAsync(cl => cl.CaseId == hearing.CaseId && cl.DeletedAt == null);

                if (caseLawyer == null)
                    continue;

                var lawyerId = caseLawyer.LawyerId;
                var hearingTime = DateTime.Today.Add(hearing.HearingTime);

                if (!lawyerHearings.ContainsKey(lawyerId))
                    lawyerHearings[lawyerId] = new List<string>();

                lawyerHearings[lawyerId].Add($"{hearingTime:hh:mm tt} — {hearing.Case!.Title}");
            }

            page++;
        }

        foreach (var (lawyerId, list) in lawyerHearings)
        {
            try
            {
                var body = list.Count == 1
                    ? $"1 hearing today: {list[0]}"
                    : $"{list.Count} hearings today. First: {list[0]}";

                await _notificationService.NotifyLawyerAsync(
                    lawyerId: lawyerId,
                    title: $"Today's Hearings ⚖️ ({list.Count})",
                    body: body,
                    type: NotificationType.HearingToday);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SendMorningHearingList failed for LawyerId {LawyerId}", lawyerId);
            }
        }

        _logger.LogInformation("SendMorningHearingList completed. Notified {Count} lawyers.", lawyerHearings.Count);
    }

    // Runs every day at 9 AM IST
    // Alerts lawyer when a case's limitation date is 30, 7, or 1 day away
    [DisableConcurrentExecution(timeoutInSeconds: 300)]
    public async Task SendLimitationAlerts()
    {
        var today = DateTime.UtcNow.Date;
        int page = 0, alertsSent = 0;

        while (true)
        {
            var batch = await _context.Cases
                .Where(c => c.LimitationDate != null
                         && c.Status == CaseStatus.Active
                         && c.DeletedAt == null)
                .Skip(page * BatchSize)
                .Take(BatchSize)
                .ToListAsync();

            if (batch.Count == 0)
                break;

            foreach (var c in batch)
            {
                try
                {
                    var daysRemaining = (c.LimitationDate!.Value.Date - today).Days;

                    if (daysRemaining > 30 || daysRemaining < 0)
                        continue;

                    var caseLawyer = await _context.CaseLawyers
                        .FirstOrDefaultAsync(cl => cl.CaseId == c.Id && cl.DeletedAt == null);

                    if (caseLawyer == null)
                        continue;

                    if (daysRemaining == 30 && !c.LimitationAlertSent30)
                    {
                        await _notificationService.NotifyLawyerAsync(
                            lawyerId: caseLawyer.LawyerId,
                            title: "⚠️ Limitation in 30 Days",
                            body: $"{c.Title} — limitation date is {c.LimitationDate:dd MMM yyyy}. 30 days remaining. Plan your action.",
                            type: NotificationType.LimitationAlert,
                            caseId: c.Id);
                        c.LimitationAlertSent30 = true;
                        alertsSent++;
                    }
                    else if (daysRemaining == 7 && !c.LimitationAlertSent7)
                    {
                        await _notificationService.NotifyLawyerAsync(
                            lawyerId: caseLawyer.LawyerId,
                            title: "🚨 URGENT: Limitation in 7 Days",
                            body: $"{c.Title} — expires {c.LimitationDate:dd MMM yyyy}. File immediately!",
                            type: NotificationType.LimitationAlert,
                            caseId: c.Id);
                        c.LimitationAlertSent7 = true;
                        alertsSent++;
                    }
                    else if (daysRemaining == 1 && !c.LimitationAlertSent1)
                    {
                        await _notificationService.NotifyLawyerAsync(
                            lawyerId: caseLawyer.LawyerId,
                            title: "🔴 CRITICAL: Limitation TOMORROW",
                            body: $"{c.Title} — expires TOMORROW {c.LimitationDate:dd MMM yyyy}. Act now!",
                            type: NotificationType.LimitationAlert,
                            caseId: c.Id);
                        c.LimitationAlertSent1 = true;
                        alertsSent++;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "SendLimitationAlerts failed for CaseId {CaseId}", c.Id);
                }
            }

            await _context.SaveChangesAsync();
            page++;
        }

        _logger.LogInformation("SendLimitationAlerts completed. Sent {Count} alerts.", alertsSent);
    }

    // Runs every day at 10 AM IST
    // Alerts lawyer about unpaid invoices that are past due date
    [DisableConcurrentExecution(timeoutInSeconds: 300)]
    public async Task SendOverdueInvoiceAlerts()
    {
        var today = DateTime.UtcNow.Date;
        int page = 0, alertsSent = 0;

        while (true)
        {
            var batch = await _context.Invoices
                .Include(i => i.Case)
                .Where(i => i.Status != InvoiceStatus.Paid
                         && i.Status != InvoiceStatus.Cancelled
                         && i.DueDate != null
                         && i.DueDate.Value.Date < today
                         && i.DeletedAt == null)
                .Skip(page * BatchSize)
                .Take(BatchSize)
                .ToListAsync();

            if (batch.Count == 0)
                break;

            foreach (var invoice in batch)
            {
                try
                {
                    if (invoice.Case == null)
                        continue;

                    var caseLawyer = await _context.CaseLawyers
                        .FirstOrDefaultAsync(cl => cl.CaseId == invoice.CaseId && cl.DeletedAt == null);

                    if (caseLawyer == null)
                        continue;

                    var clientName = invoice.Case.ClientName ?? invoice.Case.Title ?? "Unknown client";
                    var daysOverdue = (today - invoice.DueDate!.Value.Date).Days;

                    // Mark invoice as Overdue in the database
                    if (invoice.Status != InvoiceStatus.Overdue && invoice.Status != InvoiceStatus.PartiallyPaid)
                    {
                        invoice.Status = InvoiceStatus.Overdue;
                    }

                    await _notificationService.NotifyLawyerAsync(
                        lawyerId: caseLawyer.LawyerId,
                        title: "💰 Overdue Invoice",
                        body: $"Invoice #{invoice.InvoiceNumber} for {clientName} is {daysOverdue} day(s) overdue (due {invoice.DueDate:dd MMM yyyy}).",
                        type: NotificationType.InvoiceOverdue,
                        caseId: invoice.CaseId);

                    alertsSent++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "SendOverdueInvoiceAlerts failed for InvoiceId {InvoiceId}", invoice.Id);
                }
            }

            await _context.SaveChangesAsync();
            page++;
        }

        _logger.LogInformation("SendOverdueInvoiceAlerts completed. Sent {Count} alerts.", alertsSent);
    }
}
