using LexCore.Application.DTOs.Notifications;
using LexCore.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LexCore.API.Controllers;

[ApiController]
[Route("api/notifications")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly INotificationRepository _repo;
    private readonly ITenantService _tenantService;

    public NotificationsController(
        INotificationRepository repo,
        ITenantService tenantService)
    {
        _repo = repo;
        _tenantService = tenantService;
    }

    // GET /api/notifications?page=1&pageSize=20
    // Returns paginated notifications for the logged-in lawyer
    [HttpGet]
    public async Task<ActionResult<NotificationListResponse>> GetNotifications(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var lawyerId = _tenantService.GetCurrentUserId();
        var notifications = await _repo.GetByLawyerAsync(lawyerId, page, pageSize);
        var unreadCount = await _repo.GetUnreadCountAsync(lawyerId);
        var totalCount = await _repo.GetTotalCountAsync(lawyerId);

        var dtos = notifications.Select(n => new NotificationDto
        {
            Id = n.Id,
            Title = n.Title,
            Body = n.Body,
            Type = n.Type.ToString(),
            Channel = n.Channel.ToString(),
            DeliveryStatus = n.DeliveryStatus.ToString(),
            IsRead = n.IsRead,
            SentAt = n.SentAt,
            CreatedAt = n.CreatedAt,
            CaseId = n.CaseId,
            CaseTitle = n.Case?.Title,
            FailureReason = n.FailureReason
        }).ToList();

        return Ok(new NotificationListResponse
        {
            Data = dtos,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            UnreadCount = unreadCount
        });
    }

    // GET /api/notifications/unread-count
    // Returns badge count — Flutter calls this on every app launch
    [HttpGet("unread-count")]
    public async Task<ActionResult<UnreadCountDto>> GetUnreadCount()
    {
        var lawyerId = _tenantService.GetCurrentUserId();
        var count = await _repo.GetUnreadCountAsync(lawyerId);
        return Ok(new UnreadCountDto { Count = count });
    }

    // PUT /api/notifications/{id}/read
    // Marks a single notification as read
    [HttpPut("{id:guid}/read")]
    public async Task<IActionResult> MarkAsRead(Guid id)
    {
        var lawyerId = _tenantService.GetCurrentUserId();
        await _repo.MarkAsReadAsync(id, lawyerId);
        return NoContent();
    }

    // PUT /api/notifications/read-all
    // Marks all notifications as read (bulk)
    [HttpPut("read-all")]
    public async Task<IActionResult> MarkAllAsRead()
    {
        var lawyerId = _tenantService.GetCurrentUserId();
        await _repo.MarkAllAsReadAsync(lawyerId);
        return NoContent();
    }
}
