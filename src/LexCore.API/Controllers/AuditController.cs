using LexCore.Application.DTOs;
using LexCore.Application.DTOs.Audit;
using LexCore.Application.Interfaces;
using LexCore.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LexCore.API.Controllers;

[ApiController]
[Route("api/audit")]
[Authorize(Policy = "FirmAdmin")]
public class AuditController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ITenantService _tenantService;

    public AuditController(AppDbContext context, ITenantService tenantService)
    {
        _context = context;
        _tenantService = tenantService;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResponse<AuditLogDto>>> GetAuditLogs([FromQuery] AuditFilterRequest filter)
    {
        var firmId = _tenantService.GetCurrentFirmId();

        var query = _context.AuditLogs
            .Include(a => a.User)
            .Where(a => a.FirmId == firmId);

        if (filter.UserId.HasValue)
            query = query.Where(a => a.UserId == filter.UserId.Value);

        if (!string.IsNullOrEmpty(filter.EntityType))
            query = query.Where(a => a.EntityType == filter.EntityType);

        if (filter.FromDate.HasValue)
            query = query.Where(a => a.Timestamp >= filter.FromDate.Value);

        if (filter.ToDate.HasValue)
            query = query.Where(a => a.Timestamp <= filter.ToDate.Value);

        var totalCount = await query.CountAsync();

        var logs = await query
            .OrderByDescending(a => a.Timestamp)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(a => new AuditLogDto
            {
                Id = a.Id,
                UserId = a.UserId,
                UserName = a.User != null ? a.User.Name : null,
                Action = a.Action,
                EntityType = a.EntityType,
                EntityId = a.EntityId,
                Timestamp = a.Timestamp,
                IpAddress = a.IpAddress
            })
            .ToListAsync();

        return Ok(new PagedResponse<AuditLogDto>
        {
            Data = logs,
            Page = filter.Page,
            PageSize = filter.PageSize,
            TotalCount = totalCount
        });
    }
}
