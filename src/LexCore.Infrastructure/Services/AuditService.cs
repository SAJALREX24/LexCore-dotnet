using LexCore.Application.Interfaces;
using LexCore.Domain.Entities;
using LexCore.Infrastructure.Data;

namespace LexCore.Infrastructure.Services;

public class AuditService : IAuditService
{
    private readonly AppDbContext _context;
    private readonly ITenantService _tenantService;

    public AuditService(AppDbContext context, ITenantService tenantService)
    {
        _context = context;
        _tenantService = tenantService;
    }

    public async Task LogAsync(string action, string? entityType = null, Guid? entityId = null, string? oldValues = null, string? newValues = null, string? ipAddress = null)
    {
        var auditLog = new AuditLog
        {
            FirmId = _tenantService.GetCurrentFirmId(),
            UserId = _tenantService.GetCurrentUserId() != Guid.Empty ? _tenantService.GetCurrentUserId() : null,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            OldValues = oldValues,
            NewValues = newValues,
            IpAddress = ipAddress,
            Timestamp = DateTime.UtcNow
        };

        await _context.AuditLogs.AddAsync(auditLog);
        await _context.SaveChangesAsync();
    }
}
