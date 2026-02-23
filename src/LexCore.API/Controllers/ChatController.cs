using LexCore.Application.DTOs;
using LexCore.Application.DTOs.Chat;
using LexCore.Application.Interfaces;
using LexCore.Domain.Entities;
using LexCore.Domain.Enums;
using LexCore.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LexCore.API.Controllers;

[ApiController]
[Route("api/chat")]
[Authorize]
public class ChatController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ITenantService _tenantService;

    public ChatController(AppDbContext context, ITenantService tenantService)
    {
        _context = context;
        _tenantService = tenantService;
    }

    [HttpPost("{caseId:guid}")]
    public async Task<ActionResult<ApiResponse<ChatMessageDto>>> SendMessage(Guid caseId, [FromBody] SendMessageRequest request)
    {
        var firmId = _tenantService.GetCurrentFirmId();
        var userId = _tenantService.GetCurrentUserId();
        var role = _tenantService.GetCurrentUserRole();

        var caseEntity = await _context.Cases
            .Include(c => c.CaseLawyers)
            .Include(c => c.CaseClients)
            .FirstOrDefaultAsync(c => c.Id == caseId && c.FirmId == firmId);

        if (caseEntity == null)
        {
            return NotFound(ApiResponse<ChatMessageDto>.ErrorResponse("Case not found", "CASE_NOT_FOUND", 404));
        }

        if (role == UserRole.Lawyer.ToString() && !caseEntity.CaseLawyers.Any(cl => cl.LawyerId == userId && cl.DeletedAt == null))
        {
            return Forbid();
        }

        if (role == UserRole.Client.ToString())
        {
            if (!caseEntity.CaseClients.Any(cc => cc.ClientId == userId && cc.DeletedAt == null))
            {
                return Forbid();
            }
            if (request.IsInternal)
            {
                return BadRequest(ApiResponse<ChatMessageDto>.ErrorResponse("Clients cannot send internal messages", "FORBIDDEN", 400));
            }
        }

        var chat = new Chat
        {
            FirmId = firmId,
            CaseId = caseId,
            SenderId = userId,
            Message = request.Message,
            IsInternal = request.IsInternal,
            SentAt = DateTime.UtcNow
        };

        await _context.Chats.AddAsync(chat);
        await _context.SaveChangesAsync();

        var sender = await _context.Users.FindAsync(userId);

        return Ok(ApiResponse<ChatMessageDto>.SuccessResponse(new ChatMessageDto
        {
            Id = chat.Id,
            CaseId = chat.CaseId,
            SenderId = chat.SenderId,
            SenderName = sender?.Name ?? "",
            SenderRole = role,
            Message = chat.Message,
            IsInternal = chat.IsInternal,
            SentAt = chat.SentAt
        }, "Message sent successfully"));
    }

    [HttpGet("{caseId:guid}")]
    public async Task<ActionResult<ApiResponse<List<ChatMessageDto>>>> GetMessages(Guid caseId, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        var firmId = _tenantService.GetCurrentFirmId();
        var userId = _tenantService.GetCurrentUserId();
        var role = _tenantService.GetCurrentUserRole();

        var caseEntity = await _context.Cases
            .Include(c => c.CaseLawyers)
            .Include(c => c.CaseClients)
            .FirstOrDefaultAsync(c => c.Id == caseId && c.FirmId == firmId);

        if (caseEntity == null)
        {
            return NotFound(ApiResponse<List<ChatMessageDto>>.ErrorResponse("Case not found", "CASE_NOT_FOUND", 404));
        }

        if (role == UserRole.Lawyer.ToString() && !caseEntity.CaseLawyers.Any(cl => cl.LawyerId == userId && cl.DeletedAt == null))
        {
            return Forbid();
        }

        if (role == UserRole.Client.ToString() && !caseEntity.CaseClients.Any(cc => cc.ClientId == userId && cc.DeletedAt == null))
        {
            return Forbid();
        }

        var query = _context.Chats
            .Include(c => c.Sender)
            .Where(c => c.CaseId == caseId && c.FirmId == firmId);

        if (role == UserRole.Client.ToString())
        {
            query = query.Where(c => !c.IsInternal);
        }

        var messages = await query
            .OrderByDescending(c => c.SentAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new ChatMessageDto
            {
                Id = c.Id,
                CaseId = c.CaseId,
                SenderId = c.SenderId,
                SenderName = c.Sender!.Name,
                SenderRole = c.Sender.Role.ToString(),
                Message = c.Message,
                IsInternal = c.IsInternal,
                SentAt = c.SentAt
            })
            .ToListAsync();

        messages.Reverse();

        return Ok(ApiResponse<List<ChatMessageDto>>.SuccessResponse(messages));
    }
}
