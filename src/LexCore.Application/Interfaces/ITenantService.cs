using LexCore.Domain.Entities;

namespace LexCore.Application.Interfaces;

public interface ITenantService
{
    Guid GetCurrentFirmId();
    Guid GetCurrentUserId();
    string GetCurrentUserRole();
    void SetTenantContext(Guid firmId, Guid userId, string role);
}
