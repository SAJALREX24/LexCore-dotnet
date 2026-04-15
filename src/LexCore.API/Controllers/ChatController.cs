using LexCore.Application.DTOs;
using LexCore.Application.DTOs.Chat;
using LexCore.Application.Interfaces;
using LexCore.Domain.Entities;
using LexCore.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LexCore.API.Controllers;

[ApiController]
[Route("api/chat")]
[Authorize(Policy = "Lawyer")]
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
        var userId = _tenantService.GetCurrentUserId();

        var caseExists = await _context.Cases.AnyAsync(c =>
            c.Id == caseId &&
            c.CaseLawyers.Any(cl => cl.LawyerId == userId && cl.DeletedAt == null));

        if (!caseExists)
            return NotFound(ApiResponse<ChatMessageDto>.ErrorResponse("Case not found", "CASE_NOT_FOUND", 404));

        var chat = new Chat
        {
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
            SenderRole = sender?.Role.ToString() ?? "",
            Message = chat.Message,
            IsInternal = chat.IsInternal,
            SentAt = chat.SentAt
        }, "Message sent successfully"));
    }

    [HttpGet("{caseId:guid}")]
    public async Task<ActionResult<ApiResponse<List<ChatMessageDto>>>> GetMessages(Guid caseId, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        var userId = _tenantService.GetCurrentUserId();

        var caseExists = await _context.Cases.AnyAsync(c =>
            c.Id == caseId &&
            c.CaseLawyers.Any(cl => cl.LawyerId == userId && cl.DeletedAt == null));

        if (!caseExists)
            return NotFound(ApiResponse<List<ChatMessageDto>>.ErrorResponse("Case not found", "CASE_NOT_FOUND", 404));

        var messages = await _context.Chats
            .Include(c => c.Sender)
            .Where(c => c.CaseId == caseId)
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
