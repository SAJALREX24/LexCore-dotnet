using LexCore.Application.Interfaces;
using LexCore.Domain.Entities;
using LexCore.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LexCore.Infrastructure.Repositories;

public class NotificationRepository : INotificationRepository
{
    private readonly AppDbContext _context;
    private readonly ILogger<NotificationRepository> _logger;

    public NotificationRepository(AppDbContext context, ILogger<NotificationRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<IEnumerable<Notification>> GetByLawyerAsync(Guid lawyerId, int page, int pageSize)
    {
        return await _context.Notifications
            .Include(n => n.Case)
            .Where(n => n.LawyerId == lawyerId && n.DeletedAt == null)
            .OrderByDescending(n => n.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task<int> GetUnreadCountAsync(Guid lawyerId)
    {
        return await _context.Notifications
            .CountAsync(n => n.LawyerId == lawyerId && !n.IsRead && n.DeletedAt == null);
    }

    public async Task<int> GetTotalCountAsync(Guid lawyerId)
    {
        return await _context.Notifications
            .CountAsync(n => n.LawyerId == lawyerId && n.DeletedAt == null);
    }

    public async Task MarkAsReadAsync(Guid notificationId, Guid lawyerId)
    {
        var notification = await _context.Notifications
            .FirstOrDefaultAsync(n => n.Id == notificationId && n.LawyerId == lawyerId);

        if (notification == null)
            return;

        notification.IsRead = true;
        await _context.SaveChangesAsync();
    }

    public async Task MarkAllAsReadAsync(Guid lawyerId)
    {
        await _context.Notifications
            .Where(n => n.LawyerId == lawyerId && !n.IsRead && n.DeletedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true));
    }

    public async Task AddAsync(Notification notification)
    {
        await _context.Notifications.AddAsync(notification);
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }

    public async Task<string?> GetLawyerFcmTokenAsync(Guid lawyerId)
    {
        return await _context.Users
            .Where(u => u.Id == lawyerId)
            .Select(u => u.FcmToken)
            .FirstOrDefaultAsync();
    }
}
