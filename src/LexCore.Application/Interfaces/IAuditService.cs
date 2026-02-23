namespace LexCore.Application.Interfaces;

public interface IAuditService
{
    Task LogAsync(string action, string? entityType = null, Guid? entityId = null, string? oldValues = null, string? newValues = null, string? ipAddress = null);
}
