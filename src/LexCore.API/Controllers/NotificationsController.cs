using LexCore.Application.DTOs;
using LexCore.Application.DTOs.Notifications;
using LexCore.Application.Interfaces;
using LexCore.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LexCore.API.Controllers;

[ApiController]
[Route("api/notifications")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ITenantService _tenantService;

    public NotificationsController(AppDbContext context, ITenantService tenantService)
    {
        _context = context;
        _tenantService = tenantService;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<NotificationDto>>>> GetNotifications([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] bool unreadOnly = false)
    {
        var firmId = _tenantService.GetCurrentFirmId();
        var userId = _tenantService.GetCurrentUserId();

        var query = _context.Notifications
            .Where(n => n.FirmId == firmId && n.UserId == userId);

        if (unreadOnly)
        {
            query = query.Where(n => !n.IsRead);
        }

        var notifications = await query
            .OrderByDescending(n => n.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(n => new NotificationDto
            {
                Id = n.Id,
                Title = n.Title,
                Body = n.Body,
                Type = n.Type.ToString(),
                IsRead = n.IsRead,
                CreatedAt = n.CreatedAt
            })
            .ToListAsync();

        return Ok(ApiResponse<List<NotificationDto>>.SuccessResponse(notifications));
    }

    [HttpPatch("{id:guid}/read")]
    public async Task<ActionResult<ApiResponse<object>>> MarkAsRead(Guid id)
    {
        var firmId = _tenantService.GetCurrentFirmId();
        var userId = _tenantService.GetCurrentUserId();

        var notification = await _context.Notifications
            .FirstOrDefaultAsync(n => n.Id == id && n.FirmId == firmId && n.UserId == userId);

        if (notification == null)
        {
            return NotFound(ApiResponse<object>.ErrorResponse("Notification not found", "NOT_FOUND", 404));
        }

        notification.IsRead = true;
        await _context.SaveChangesAsync();

        return Ok(ApiResponse<object>.SuccessResponse(null!, "Notification marked as read"));
    }

    [HttpPatch("read-all")]
    public async Task<ActionResult<ApiResponse<object>>> MarkAllAsRead()
    {
        var firmId = _tenantService.GetCurrentFirmId();
        var userId = _tenantService.GetCurrentUserId();

        await _context.Notifications
            .Where(n => n.FirmId == firmId && n.UserId == userId && !n.IsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true));

        return Ok(ApiResponse<object>.SuccessResponse(null!, "All notifications marked as read"));
    }
}
