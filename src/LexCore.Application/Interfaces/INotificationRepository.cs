using LexCore.Domain.Entities;

namespace LexCore.Application.Interfaces;

public interface INotificationRepository
{
    Task<IEnumerable<Notification>> GetByLawyerAsync(
        Guid lawyerId, int page, int pageSize);

    Task<int> GetUnreadCountAsync(Guid lawyerId);

    Task<int> GetTotalCountAsync(Guid lawyerId);

    Task MarkAsReadAsync(Guid notificationId, Guid lawyerId);

    Task MarkAllAsReadAsync(Guid lawyerId);

    Task AddAsync(Notification notification);

    Task SaveChangesAsync();

    Task<string?> GetLawyerFcmTokenAsync(Guid lawyerId);
}
